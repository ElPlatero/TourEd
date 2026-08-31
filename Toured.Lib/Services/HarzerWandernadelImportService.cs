using System.Globalization;
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
    private const int SeasonalStampingPointNumber = 69;
    private const string Attribution = "© OpenStreetMap contributors";
    private const string LicenseName = "Open Data Commons Open Database License (ODbL) 1.0";
    private static readonly Uri LicenseUri = new("https://opendatacommons.org/licenses/odbl/1-0/");
    private readonly HttpClient _client;
    private readonly HarzerWandernadelConfiguration _configuration;

    public HarzerWandernadelImportService(
        HttpClient client,
        HarzerWandernadelConfiguration configuration)
    {
        _client = client;
        _configuration = configuration;
    }

    public async Task<StampingPointSourceSnapshot> DownloadStampingPointsAsync(
        CancellationToken cancellationToken = default)
    {
        ValidateConfiguration();
        var document = await DownloadRelationAsync(cancellationToken);
        var relation = document.Root?.Elements("relation").SingleOrDefault(element =>
            ParseIdentifier(element.Attribute("id")?.Value, "relation") == _configuration.RelationId)
            ?? throw new InvalidDataException($"OSM relation {_configuration.RelationId} is missing from the response.");

        var revision = RequiredAttribute(relation, "version", "relation");
        var sourceUpdatedAt = ParseTimestamp(RequiredAttribute(relation, "timestamp", "relation"));
        ValidateRelation(relation);

        var nodesById = document.Root!.Elements("node").ToDictionary(
            node => ParseIdentifier(node.Attribute("id")?.Value, "node"));
        var candidatesByNumber = new Dictionary<int, List<StampingPoint>>();

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
                number is < 1 or > ExpectedStampingPointCount)
            {
                throw new InvalidDataException($"OSM node {nodeId} has invalid HWN reference '{refValue}'.");
            }

            if (number == SeasonalStampingPointNumber && !IsSummerLocation(tags))
            {
                continue;
            }

            var rawName = RequiredTag(tags, "name", nodeId);
            var name = NamePrefixRegex().Replace(rawName, string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new InvalidDataException($"OSM node {nodeId} has no usable HWN name.");
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
                StampingProvider.HarzerWandernadelId,
                $"osm-node-{nodeId}")
            {
                SeriesId = StampingSeries.HarzerWandernadelStandardId
            };
            GetOrAdd(candidatesByNumber, number).Add(point);
        }

        ValidateCompleteNumberSet(candidatesByNumber);
        var points = candidatesByNumber
            .OrderBy(entry => entry.Key)
            .Select(entry => entry.Value.Single())
            .ToArray();

        return new StampingPointSourceSnapshot(
            points,
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
            throw new InvalidOperationException("HWN OSM relation id must be positive.");
        }

        ValidateHttpsUri(_configuration.RelationApiUri, "api.openstreetmap.org", "relation API");
        ValidateHttpsUri(_configuration.RelationPublicUri, "www.openstreetmap.org", "public relation");
        if (_configuration.MaxDownloadBytes is < 1024 or > 20 * 1024 * 1024)
        {
            throw new InvalidOperationException("HWN MaxDownloadBytes must be between 1 KiB and 20 MiB.");
        }
    }

    private static void ValidateHttpsUri(Uri? uri, string expectedHost, string description)
    {
        if (uri is not { IsAbsoluteUri: true } ||
            !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(uri.Host, expectedHost, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"HWN OSM {description} URL must use HTTPS on {expectedHost}.");
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
            throw new InvalidDataException("The OSM relation response exceeds the configured size limit.");
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
                throw new InvalidDataException("The OSM relation response exceeds the configured size limit.");
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

    private static void ValidateRelation(XElement relation)
    {
        var tags = relation.Elements("tag").ToDictionary(
            tag => RequiredAttribute(tag, "k", "relation tag"),
            tag => RequiredAttribute(tag, "v", "relation tag"),
            StringComparer.Ordinal);
        if (!string.Equals(tags.GetValueOrDefault("name"), "HWN Stempelstellen", StringComparison.Ordinal) ||
            !string.Equals(tags.GetValueOrDefault("operator"), "Harzer Wandernadel", StringComparison.Ordinal))
        {
            throw new InvalidDataException("The configured OSM relation is not the Harzer Wandernadel relation.");
        }
    }

    private static void ValidateNodeTags(long nodeId, IReadOnlyDictionary<string, string> tags)
    {
        if (!string.Equals(tags.GetValueOrDefault("checkpoint"), "hiking", StringComparison.Ordinal) ||
            !string.Equals(tags.GetValueOrDefault("checkpoint:type"), "stamp", StringComparison.Ordinal) ||
            !string.Equals(tags.GetValueOrDefault("operator"), "Harzer Wandernadel", StringComparison.Ordinal))
        {
            throw new InvalidDataException($"OSM node {nodeId} is not a Harzer Wandernadel hiking stamp checkpoint.");
        }
    }

    private static bool IsSummerLocation(IReadOnlyDictionary<string, string> tags)
        => tags.TryGetValue("seasonal", out var seasonal) && seasonal
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Contains("summer", StringComparer.OrdinalIgnoreCase);

    private static string RequiredTag(IReadOnlyDictionary<string, string> tags, string key, long nodeId)
        => tags.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : throw new InvalidDataException($"OSM node {nodeId} is missing required tag '{key}'.");

    private static string RequiredAttribute(XElement element, string name, string description)
        => !string.IsNullOrWhiteSpace(element.Attribute(name)?.Value)
            ? element.Attribute(name)!.Value
            : throw new InvalidDataException($"OSM {description} is missing attribute '{name}'.");

    private static long ParseIdentifier(string? value, string description)
        => long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var identifier) && identifier > 0
            ? identifier
            : throw new InvalidDataException($"OSM {description} has invalid identifier '{value}'.");

    private static DateTime ParseTimestamp(string value)
        => DateTime.TryParseExact(
            value,
            "yyyy-MM-dd'T'HH:mm:ss'Z'",
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out var timestamp)
            ? timestamp
            : throw new InvalidDataException($"OSM relation has invalid timestamp '{value}'.");

    private static decimal ParseCoordinate(string? value, string coordinateName, long nodeId)
        => decimal.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var coordinate)
            ? coordinate
            : throw new InvalidDataException($"OSM node {nodeId} has invalid {coordinateName} '{value}'.");

    private static List<TValue> GetOrAdd<TKey, TValue>(IDictionary<TKey, List<TValue>> dictionary, TKey key)
        where TKey : notnull
    {
        if (!dictionary.TryGetValue(key, out var values))
        {
            values = [];
            dictionary.Add(key, values);
        }
        return values;
    }

    private static void ValidateCompleteNumberSet(IReadOnlyDictionary<int, List<StampingPoint>> candidatesByNumber)
    {
        var missing = Enumerable.Range(1, ExpectedStampingPointCount)
            .Where(number => !candidatesByNumber.ContainsKey(number))
            .ToArray();
        var duplicates = candidatesByNumber
            .Where(entry => entry.Value.Count != 1)
            .Select(entry => entry.Key)
            .OrderBy(number => number)
            .ToArray();
        if (missing.Length > 0 || duplicates.Length > 0)
        {
            throw new InvalidDataException(
                $"The HWN OSM relation must provide each summer location from 1 through {ExpectedStampingPointCount} exactly once " +
                $"(missing: {string.Join(", ", missing)}; duplicate: {string.Join(", ", duplicates)}).");
        }
    }

    [GeneratedRegex("^HWN\\s+(?<number>\\d{3})$", RegexOptions.IgnoreCase)]
    private static partial Regex ReferenceRegex();

    [GeneratedRegex("^HWN\\s+\\d{3}\\s*[-–—]\\s*", RegexOptions.IgnoreCase)]
    private static partial Regex NamePrefixRegex();
}
