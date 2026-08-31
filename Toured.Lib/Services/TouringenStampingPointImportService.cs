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
    private const int ExpectedStandardPointCount = 430;
    private const int ExpectedNaturalTreasuresCount = 8;
    private const int ExpectedRhoenCount = 13;
    private const string Attribution = "© OpenStreetMap contributors";
    private const string LicenseName = "Open Data Commons Open Database License (ODbL) 1.0";
    private static readonly Uri LicenseUri = new("https://opendatacommons.org/licenses/odbl/1-0/");

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

    public async Task<StampingPointSourceSnapshot> DownloadStampingPointsAsync(CancellationToken cancellationToken = default)
    {
        ValidateConfiguration();
        var relationTask = DownloadRelationAsync(cancellationToken);
        var naturalTreasuresTask = DownloadArchiveAsync(_configuration.NaturalTreasuresGpxUri, cancellationToken);
        var rhoenTask = DownloadArchiveAsync(_configuration.RhoenFamilyTrailsGpxUri, cancellationToken);
        await Task.WhenAll(relationTask, naturalTreasuresTask, rhoenTask);

        var (standardPoints, revision, sourceUpdatedAt) = ParseRelation(await relationTask);
        var naturalTreasures = ParseArchive(
            await naturalTreasuresTask,
            StampingSeries.TouringenNaturalTreasuresId,
            StampingSeries.TouringenNaturalTreasuresSlug,
            ParseNaturalTreasureIdentity,
            ExpectedNaturalTreasuresCount);
        var rhoen = ParseArchive(
            await rhoenTask,
            StampingSeries.TouringenRhoenFamilyTrailsId,
            StampingSeries.TouringenRhoenFamilyTrailsSlug,
            ParseRhoenIdentity,
            ExpectedRhoenCount);

        return new StampingPointSourceSnapshot(
            [.. standardPoints, .. naturalTreasures, .. rhoen],
            _configuration.RelationPublicUri,
            Attribution,
            LicenseName,
            LicenseUri,
            revision,
            sourceUpdatedAt);
    }

    private void ValidateConfiguration()
    {
        if (_configuration.RelationId <= 0)
        {
            throw new InvalidOperationException("Touringen OSM relation id must be positive.");
        }

        ValidateOsmUri(_configuration.RelationApiUri, "api.openstreetmap.org", "relation API");
        ValidateOsmUri(_configuration.RelationPublicUri, "www.openstreetmap.org", "public relation");
        ValidateTouringenUri(_configuration.NaturalTreasuresGpxUri, "natural treasures GPX archive");
        ValidateTouringenUri(_configuration.RhoenFamilyTrailsGpxUri, "Rhön family trails GPX archive");
        if (_configuration.MaxDownloadBytes is < 1024 or > 20 * 1024 * 1024)
        {
            throw new InvalidOperationException("Touringen MaxDownloadBytes must be between 1 KiB and 20 MiB.");
        }
    }

    private static void ValidateOsmUri(Uri? uri, string expectedHost, string description)
    {
        if (uri is not { IsAbsoluteUri: true } ||
            !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(uri.Host, expectedHost, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Touringen OSM {description} URL must use HTTPS on {expectedHost}.");
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

    private async Task<XDocument> DownloadRelationAsync(CancellationToken cancellationToken)
    {
        using var response = await _client.GetAsync(
            _configuration.RelationApiUri,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength > _configuration.MaxDownloadBytes)
        {
            throw new InvalidDataException("The Touringen OSM relation response exceeds the configured size limit.");
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
                throw new InvalidDataException("The Touringen OSM relation response exceeds the configured size limit.");
            }
            await destination.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
        }

        destination.Position = 0;
        var settings = new XmlReaderSettings
        {
            Async = false,
            DtdProcessing = DtdProcessing.Prohibit,
            MaxCharactersInDocument = _configuration.MaxDownloadBytes,
            XmlResolver = null
        };
        using var reader = XmlReader.Create(destination, settings);
        return XDocument.Load(reader, LoadOptions.None);
    }

    private (IReadOnlyList<StampingPoint> Points, string Revision, DateTime SourceUpdatedAt) ParseRelation(XDocument document)
    {
        var relation = document.Root?.Elements("relation").SingleOrDefault(element =>
            ParseIdentifier(element.Attribute("id")?.Value, "relation") == _configuration.RelationId)
            ?? throw new InvalidDataException($"OSM relation {_configuration.RelationId} is missing from the response.");

        var revision = RequiredAttribute(relation, "version", "relation");
        var sourceUpdatedAt = ParseTimestamp(RequiredAttribute(relation, "timestamp", "relation"));
        ValidateRelation(relation);

        var nodesById = document.Root!.Elements("node").ToDictionary(
            node => ParseIdentifier(node.Attribute("id")?.Value, "node"));
        var candidatesByNumber = new Dictionary<int, StampingPoint>();

        foreach (var member in relation.Elements("member")
                     .Where(member => string.Equals(member.Attribute("type")?.Value, "node", StringComparison.Ordinal)))
        {
            var nodeId = ParseIdentifier(member.Attribute("ref")?.Value, "node reference");
            if (!nodesById.TryGetValue(nodeId, out var node))
            {
                throw new InvalidDataException($"OSM relation member node {nodeId} is missing from the response.");
            }

            var tags = node.Elements("tag").ToDictionary(
                tag => RequiredAttribute(tag, "k", $"node {nodeId} tag"),
                tag => RequiredAttribute(tag, "v", $"node {nodeId} tag"),
                StringComparer.Ordinal);
            ValidateNodeTags(nodeId, tags);

            var refValue = RequiredTag(tags, "ref", nodeId);
            var refMatch = ReferenceRegex().Match(refValue);
            if (!refMatch.Success ||
                !int.TryParse(refMatch.Groups["number"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var number) ||
                number is < 1 or > ExpectedStandardPointCount)
            {
                throw new InvalidDataException($"OSM node {nodeId} has invalid Touringen reference '{refValue}'.");
            }

            if (candidatesByNumber.ContainsKey(number))
            {
                throw new InvalidDataException($"Touringen standard points contain duplicate number {number}.");
            }

            var rawName = RequiredTag(tags, "name", nodeId);
            var name = rawName.Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new InvalidDataException($"OSM node {nodeId} has no usable Touringen name.");
            }

            var longitude = ParseCoordinate(node.Attribute("lon")?.Value, "longitude", nodeId);
            var latitude = ParseCoordinate(node.Attribute("lat")?.Value, "latitude", nodeId);
            var point = new StampingPoint(
                default,
                name,
                longitude,
                latitude,
                number,
                number,
                StampingProvider.TouringenId,
                $"osm-node-{nodeId}")
            {
                SeriesId = StampingSeries.TouringenStandardId
            };
            candidatesByNumber[number] = point;
        }

        if (candidatesByNumber.Count != ExpectedStandardPointCount ||
            !candidatesByNumber.Keys.OrderBy(n => n).SequenceEqual(Enumerable.Range(1, ExpectedStandardPointCount)))
        {
            throw new InvalidDataException($"Touringen standard series must contain every number from 1 through {ExpectedStandardPointCount} exactly once.");
        }

        var points = candidatesByNumber.OrderBy(e => e.Key).Select(e => e.Value).ToArray();
        return (points, revision, sourceUpdatedAt);
    }

    private static void ValidateRelation(XElement relation)
    {
        var tags = relation.Elements("tag").ToDictionary(
            tag => RequiredAttribute(tag, "k", "relation tag"),
            tag => RequiredAttribute(tag, "v", "relation tag"),
            StringComparer.Ordinal);
        if (!string.Equals(tags.GetValueOrDefault("name"), "Stempelstellen Touringen", StringComparison.Ordinal) ||
            !string.Equals(tags.GetValueOrDefault("operator"), "Touringen", StringComparison.Ordinal))
        {
            throw new InvalidDataException("The configured OSM relation is not the Touringen relation.");
        }
    }

    private static void ValidateNodeTags(long nodeId, IReadOnlyDictionary<string, string> tags)
    {
        if (!string.Equals(tags.GetValueOrDefault("checkpoint"), "hiking", StringComparison.Ordinal) ||
            !string.Equals(tags.GetValueOrDefault("checkpoint:type"), "stamp", StringComparison.Ordinal) ||
            !string.Equals(tags.GetValueOrDefault("operator"), "Touringen", StringComparison.Ordinal))
        {
            throw new InvalidDataException($"OSM node {nodeId} is not a Touringen hiking stamp checkpoint.");
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

    private static decimal ParseCoordinate(string? value, string coordinateName, object source)
        => decimal.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var coordinate)
            ? coordinate
            : throw new InvalidDataException($"Touringen {source} has invalid {coordinateName} '{value}'.");

    private static long ParseIdentifier(string? value, string description)
        => long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var id) && id > 0
            ? id
            : throw new InvalidDataException($"The OSM {description} identifier '{value}' is invalid.");

    private static string RequiredAttribute(XElement element, string attributeName, string description)
        => element.Attribute(attributeName)?.Value
           ?? throw new InvalidDataException($"The OSM {description} is missing required attribute '{attributeName}'.");

    private static string RequiredTag(IReadOnlyDictionary<string, string> tags, string key, long nodeId)
        => tags.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : throw new InvalidDataException($"OSM node {nodeId} is missing required tag '{key}'.");

    private static DateTime ParseTimestamp(string value)
        => DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var timestamp)
            ? timestamp
            : throw new InvalidDataException($"The OSM timestamp '{value}' is invalid.");

    [GeneratedRegex(@"^(?:Touringen\s+)?(?<number>\d+)$", RegexOptions.IgnoreCase)]
    private static partial Regex ReferenceRegex();

    [GeneratedRegex(@"^Touringen Sonderstempel\s+", RegexOptions.IgnoreCase)]
    private static partial Regex NaturalTreasurePrefixRegex();

    [GeneratedRegex(@"^(?<number>\d{2})_.+\.gpx$", RegexOptions.IgnoreCase)]
    private static partial Regex RhoenFileNameRegex();
}
