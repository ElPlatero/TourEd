using System.Globalization;
using System.IO.Compression;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using TourEd.Lib.Abstractions.Interfaces.Services;
using TourEd.Lib.Abstractions.Models;
using TourEd.Lib.Abstractions.Options;

namespace TourEd.Lib.Services;

public sealed partial class HarzerWandernadelImportService : IHarzerWandernadelImportService
{
    private const int ExpectedStampingPointCount = 222;
    private readonly HttpClient _client;
    private readonly HarzerWandernadelConfiguration _configuration;

    public HarzerWandernadelImportService(
        HttpClient client,
        HarzerWandernadelConfiguration configuration)
    {
        _client = client;
        _configuration = configuration;
    }

    public async Task<IReadOnlyList<StampingPoint>> DownloadStampingPointsAsync(
        CancellationToken cancellationToken = default)
    {
        ValidateConfiguration();
        var downloadPage = await DownloadTextAsync(_configuration.DownloadPageUri, cancellationToken);
        var overviewPage = await DownloadTextAsync(_configuration.OverviewUri, cancellationToken);
        var archiveUri = GetArchiveUri(downloadPage);
        var archive = await DownloadBytesAsync(archiveUri, cancellationToken);
        var overviewNames = ParseOverviewNames(overviewPage);
        var points = ParseGpxArchive(archive, overviewNames);
        ValidateCompleteNumberSet(points.Select(point => point.Number), "GPX archive");
        return points.OrderBy(point => point.Number).ToArray();
    }

    private void ValidateConfiguration()
    {
        if (_configuration.DownloadPageUri is not { IsAbsoluteUri: true } ||
            _configuration.OverviewUri is not { IsAbsoluteUri: true })
        {
            throw new InvalidOperationException("HWN download and overview URLs must be absolute.");
        }

        if (_configuration.MaxDownloadBytes is < 1024 or > 20 * 1024 * 1024)
        {
            throw new InvalidOperationException("HWN MaxDownloadBytes must be between 1 KiB and 20 MiB.");
        }
    }

    private async Task<string> DownloadTextAsync(Uri uri, CancellationToken cancellationToken)
        => Encoding.UTF8.GetString(await DownloadBytesAsync(uri, cancellationToken));

    private async Task<byte[]> DownloadBytesAsync(Uri uri, CancellationToken cancellationToken)
    {
        using var response = await _client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength > _configuration.MaxDownloadBytes)
        {
            throw new InvalidDataException($"HWN response from {uri.Host} exceeds the configured size limit.");
        }

        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var destination = new MemoryStream();
        var buffer = new byte[81920];
        while (true)
        {
            var bytesRead = await source.ReadAsync(buffer, cancellationToken);
            if (bytesRead == 0)
            {
                break;
            }

            if (destination.Length + bytesRead > _configuration.MaxDownloadBytes)
            {
                throw new InvalidDataException($"HWN response from {uri.Host} exceeds the configured size limit.");
            }
            await destination.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
        }
        return destination.ToArray();
    }

    private Uri GetArchiveUri(string downloadPage)
    {
        var match = GpxArchiveLinkRegex().Match(downloadPage);
        if (!match.Success)
        {
            throw new InvalidDataException("The HWN download page does not contain a GPX ZIP link.");
        }

        var href = WebUtility.HtmlDecode(match.Groups["href"].Value);
        var archiveUri = new Uri(_configuration.DownloadPageUri, href);
        if (!string.Equals(archiveUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
            !(string.Equals(archiveUri.Host, "harzer-wandernadel.de", StringComparison.OrdinalIgnoreCase) ||
              archiveUri.Host.EndsWith(".harzer-wandernadel.de", StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidDataException("The HWN GPX ZIP link points to an unexpected host.");
        }
        return archiveUri;
    }

    private static IReadOnlyDictionary<int, string> ParseOverviewNames(string overviewPage)
    {
        var names = new Dictionary<int, string>();
        foreach (Match match in OverviewRowRegex().Matches(overviewPage))
        {
            var number = int.Parse(match.Groups["number"].Value, CultureInfo.InvariantCulture);
            var name = NormalizeName(match.Groups["name"].Value);
            if (string.IsNullOrWhiteSpace(name) || !names.TryAdd(number, name))
            {
                throw new InvalidDataException($"The HWN overview contains an invalid or duplicate entry for number {number}.");
            }
        }

        ValidateCompleteNumberSet(names.Keys, "overview table");
        return names;
    }

    private IReadOnlyList<StampingPoint> ParseGpxArchive(
        byte[] archiveBytes,
        IReadOnlyDictionary<int, string> overviewNames)
    {
        using var archiveStream = new MemoryStream(archiveBytes, writable: false);
        using var archive = new ZipArchive(archiveStream, ZipArchiveMode.Read, leaveOpen: false);
        var gpxEntries = archive.Entries.Where(entry =>
            string.Equals(Path.GetExtension(entry.Name), ".gpx", StringComparison.OrdinalIgnoreCase)).ToArray();
        if (gpxEntries is not [var gpxEntry] || gpxEntry.Length > _configuration.MaxDownloadBytes)
        {
            throw new InvalidDataException("The HWN ZIP archive must contain exactly one size-limited GPX file.");
        }

        var settings = new XmlReaderSettings
        {
            Async = false,
            DtdProcessing = DtdProcessing.Prohibit,
            MaxCharactersInDocument = _configuration.MaxDownloadBytes,
            XmlResolver = null
        };
        using var entryStream = gpxEntry.Open();
        using var reader = XmlReader.Create(entryStream, settings);
        var document = XDocument.Load(reader, LoadOptions.None);
        var points = new List<StampingPoint>();
        foreach (var waypoint in document.Descendants().Where(element => element.Name.LocalName == "wpt"))
        {
            var rawName = ChildValue(waypoint, "name");
            var match = GpxPointNameRegex().Match(rawName);
            if (!match.Success)
            {
                throw new InvalidDataException($"Invalid HWN waypoint name: {rawName}");
            }

            var number = int.Parse(match.Groups["number"].Value, CultureInfo.InvariantCulture);
            var longitude = ParseCoordinate(waypoint.Attribute("lon")?.Value, "longitude", number);
            var latitude = ParseCoordinate(waypoint.Attribute("lat")?.Value, "latitude", number);
            var fallbackName = FirstNonEmpty(
                ChildValue(waypoint, "desc"),
                ChildValue(waypoint, "cmt"),
                match.Groups["name"].Value);
            var displayName = overviewNames.TryGetValue(number, out var currentName)
                ? currentName
                : NormalizeName(fallbackName);

            points.Add(new StampingPoint(
                default,
                displayName,
                longitude,
                latitude,
                number,
                number,
                StampingProvider.HarzerWandernadelId,
                $"HWN{number:D3}"));
        }
        return points;
    }

    private static decimal ParseCoordinate(string? value, string coordinateName, int number)
    {
        if (!decimal.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var coordinate))
        {
            throw new InvalidDataException($"HWN {number} has an invalid {coordinateName}.");
        }
        return coordinate;
    }

    private static string ChildValue(XElement parent, string localName)
        => parent.Elements().FirstOrDefault(element => element.Name.LocalName == localName)?.Value ?? string.Empty;

    private static string FirstNonEmpty(params string[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;

    private static string NormalizeName(string value)
    {
        var withoutTags = HtmlTagRegex().Replace(value, " ");
        var decoded = WebUtility.HtmlDecode(withoutTags).Replace("\u00ad", string.Empty).Replace('\u00a0', ' ');
        return WhitespaceRegex().Replace(decoded, " ").Trim();
    }

    private static void ValidateCompleteNumberSet(IEnumerable<int> numbers, string source)
    {
        var actual = numbers.OrderBy(number => number).ToArray();
        var expected = Enumerable.Range(1, ExpectedStampingPointCount).ToArray();
        if (!actual.SequenceEqual(expected))
        {
            throw new InvalidDataException($"The HWN {source} must contain each number from 1 through {ExpectedStampingPointCount} exactly once.");
        }
    }

    [GeneratedRegex("href\\s*=\\s*[\"'](?<href>[^\"']*GPX-Daten-Stempelstellen[^\"']*\\.zip(?:\\?[^\"']*)?)[\"']", RegexOptions.IgnoreCase)]
    private static partial Regex GpxArchiveLinkRegex();

    [GeneratedRegex("<tr[^>]*>\\s*<td[^>]*>\\s*(?<number>\\d{1,3})\\s*</td>\\s*<td[^>]*>(?<name>.*?)</td>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex OverviewRowRegex();

    [GeneratedRegex("^HWN(?<number>\\d{3})\\s+(?<name>.+)$", RegexOptions.IgnoreCase)]
    private static partial Regex GpxPointNameRegex();

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex HtmlTagRegex();

    [GeneratedRegex("\\s+")]
    private static partial Regex WhitespaceRegex();
}
