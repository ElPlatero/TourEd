using System.Globalization;
using System.IO.Compression;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using TourEd.Lib.Abstractions.Interfaces.Services;
using TourEd.Lib.Abstractions.Models;
using TourEd.Lib.Abstractions.Options;

namespace TourEd.Lib.Services;

public sealed partial class TouringenStampingPointImportService : ITouringenStampingPointImportService
{
    private static readonly IReadOnlyDictionary<string, int> NaturalTreasureNumbers =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["Urwaldpfad Leutenberg"] = 1,
            ["Bronzeteufel"] = 2,
            ["Hünenburg"] = 3,
            ["Obstpavillon am Schlachtenberg"] = 4,
            ["Haus des Gastes"] = 5,
            ["12 Apostel"] = 6,
            ["Stutenhauswiese"] = 7,
            ["Ausblick Wenigentaft"] = 8
        };

    private readonly HttpClient _client;
    private readonly TouringenWebsiteConfiguration _configuration;

    public TouringenStampingPointImportService(HttpClient client, TouringenWebsiteConfiguration configuration)
    {
        _client = client;
        _configuration = configuration;
    }

    public async Task<TouringenStampingPointSnapshot> DownloadStampingPointsAsync(CancellationToken cancellationToken = default)
    {
        ValidateConfiguration();
        var standardTask = DownloadArchiveAsync(_configuration.StandardGpxUri, cancellationToken);
        var naturalTreasuresTask = DownloadArchiveAsync(_configuration.NaturalTreasuresGpxUri, cancellationToken);
        var rhoenTask = DownloadArchiveAsync(_configuration.RhoenFamilyTrailsGpxUri, cancellationToken);
        await Task.WhenAll(standardTask, naturalTreasuresTask, rhoenTask);

        var standard = ParseArchive(
            await standardTask,
            StampingSeries.TouringenStandardId,
            StampingSeries.TouringenStandardSlug,
            ParseStandardIdentity,
            430);
        var naturalTreasures = ParseArchive(
            await naturalTreasuresTask,
            StampingSeries.TouringenNaturalTreasuresId,
            StampingSeries.TouringenNaturalTreasuresSlug,
            ParseNaturalTreasureIdentity,
            8);
        var rhoen = ParseArchive(
            await rhoenTask,
            StampingSeries.TouringenRhoenFamilyTrailsId,
            StampingSeries.TouringenRhoenFamilyTrailsSlug,
            ParseRhoenIdentity,
            13);

        return new TouringenStampingPointSnapshot([.. standard, .. naturalTreasures, .. rhoen]);
    }

    private void ValidateConfiguration()
    {
        ValidateTouringenUri(_configuration.StandardGpxUri, "standard GPX archive");
        ValidateTouringenUri(_configuration.NaturalTreasuresGpxUri, "natural treasures GPX archive");
        ValidateTouringenUri(_configuration.RhoenFamilyTrailsGpxUri, "Rhön family trails GPX archive");
        if (_configuration.MaxDownloadBytes is < 1024 or > 20 * 1024 * 1024)
        {
            throw new InvalidOperationException("Touringen MaxDownloadBytes must be between 1 KiB and 20 MiB.");
        }
    }

    private static void ValidateTouringenUri(Uri? uri, string description)
    {
        if (uri is not { IsAbsoluteUri: true } ||
            !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(uri.Host, "www.touringen.de", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Touringen {description} URL must use HTTPS on www.touringen.de.");
        }
    }

    private async Task<byte[]> DownloadArchiveAsync(Uri uri, CancellationToken cancellationToken)
    {
        using var response = await _client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength > _configuration.MaxDownloadBytes)
        {
            throw new InvalidDataException($"Touringen archive '{uri}' exceeds the configured size limit.");
        }

        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var destination = new MemoryStream();
        var buffer = new byte[81920];
        while (true)
        {
            var bytesRead = await source.ReadAsync(buffer, cancellationToken);
            if (bytesRead == 0) break;
            if (destination.Length + bytesRead > _configuration.MaxDownloadBytes)
            {
                throw new InvalidDataException($"Touringen archive '{uri}' exceeds the configured size limit.");
            }
            await destination.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
        }
        return destination.ToArray();
    }

    private IReadOnlyList<StampingPoint> ParseArchive(
        byte[] archiveBytes,
        int seriesId,
        string seriesSlug,
        Func<string, XElement, (int Number, string Name)> parseIdentity,
        int expectedCount)
    {
        using var source = new MemoryStream(archiveBytes, writable: false);
        using var archive = new ZipArchive(source, ZipArchiveMode.Read);
        var gpxEntries = archive.Entries
            .Where(entry => entry.Name.EndsWith(".gpx", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (gpxEntries.Length != expectedCount)
        {
            throw new InvalidDataException($"Touringen series '{seriesSlug}' must contain exactly {expectedCount} GPX files, but contains {gpxEntries.Length}.");
        }

        var points = new List<StampingPoint>(expectedCount);
        long totalUncompressedBytes = 0;
        foreach (var entry in gpxEntries)
        {
            totalUncompressedBytes += entry.Length;
            if (entry.Length <= 0 || totalUncompressedBytes > _configuration.MaxDownloadBytes)
            {
                throw new InvalidDataException($"Touringen series '{seriesSlug}' contains invalid or excessive GPX data.");
            }

            using var entryStream = entry.Open();
            var settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                MaxCharactersInDocument = _configuration.MaxDownloadBytes,
                XmlResolver = null
            };
            using var reader = XmlReader.Create(entryStream, settings);
            var document = XDocument.Load(reader, LoadOptions.None);
            var root = document.Root ?? throw new InvalidDataException($"Touringen GPX file '{entry.FullName}' is empty.");
            XNamespace ns = root.Name.Namespace;
            var waypoint = root.Elements(ns + "wpt").SingleOrDefault()
                ?? throw new InvalidDataException($"Touringen GPX file '{entry.FullName}' must contain exactly one waypoint.");
            var (number, name) = parseIdentity(entry.Name, waypoint);
            var latitude = ParseCoordinate(waypoint.Attribute("lat")?.Value, "latitude", entry.FullName);
            var longitude = ParseCoordinate(waypoint.Attribute("lon")?.Value, "longitude", entry.FullName);
            points.Add(new StampingPoint(
                default,
                name,
                longitude,
                latitude,
                number,
                0,
                StampingProvider.TouringenId,
                $"{seriesSlug}-{number.ToString(CultureInfo.InvariantCulture)}")
            {
                SeriesId = seriesId
            });
        }

        var expectedNumbers = Enumerable.Range(1, expectedCount);
        if (!points.Select(point => point.Number!.Value).OrderBy(number => number).SequenceEqual(expectedNumbers))
        {
            throw new InvalidDataException($"Touringen series '{seriesSlug}' must contain every number from 1 through {expectedCount} exactly once.");
        }
        return points.OrderBy(point => point.Number).ToArray();
    }

    private static (int Number, string Name) ParseStandardIdentity(string fileName, XElement waypoint)
    {
        var match = StandardFileNameRegex().Match(fileName);
        if (!match.Success || !int.TryParse(match.Groups["number"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var number))
        {
            throw new InvalidDataException($"Touringen standard GPX filename '{fileName}' has no usable point number.");
        }
        return (number, RequireName(match.Groups["name"].Value, fileName));
    }

    private static (int Number, string Name) ParseNaturalTreasureIdentity(string fileName, XElement waypoint)
    {
        var rawName = WaypointName(waypoint);
        var name = NaturalTreasurePrefixRegex().Replace(rawName, string.Empty).Trim();
        if (!NaturalTreasureNumbers.TryGetValue(name, out var number))
        {
            throw new InvalidDataException($"Unknown Touringen natural treasure '{name}' in '{fileName}'. Update the explicit source correction map after verification.");
        }
        return (number, name);
    }

    private static (int Number, string Name) ParseRhoenIdentity(string fileName, XElement waypoint)
    {
        var match = RhoenFileNameRegex().Match(fileName);
        if (!match.Success || !int.TryParse(match.Groups["number"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var number))
        {
            throw new InvalidDataException($"Touringen Rhön GPX filename '{fileName}' has no usable point number.");
        }
        return (number, WaypointName(waypoint));
    }

    private static string WaypointName(XElement waypoint)
    {
        XNamespace ns = waypoint.Name.Namespace;
        return RequireName(waypoint.Element(ns + "name")?.Value, "waypoint");
    }

    private static string RequireName(string? value, string source)
        => !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : throw new InvalidDataException($"Touringen GPX '{source}' has no usable point name.");

    private static decimal ParseCoordinate(string? value, string coordinateName, string source)
        => decimal.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var coordinate)
            ? coordinate
            : throw new InvalidDataException($"Touringen GPX '{source}' has invalid {coordinateName} '{value}'.");

    [GeneratedRegex(@"^Touringen Stempelstellen Nr\. (?<number>\d+) (?<name>.+)\.gpx$", RegexOptions.IgnoreCase)]
    private static partial Regex StandardFileNameRegex();

    [GeneratedRegex(@"^Touringen Sonderstempel\s+", RegexOptions.IgnoreCase)]
    private static partial Regex NaturalTreasurePrefixRegex();

    [GeneratedRegex(@"^(?<number>\d{2})_.+\.gpx$", RegexOptions.IgnoreCase)]
    private static partial Regex RhoenFileNameRegex();
}
