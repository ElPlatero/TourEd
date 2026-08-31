using System.IO.Compression;
using System.Net;
using System.Text;
using System.Xml;
using TourEd.Lib.Abstractions.Models;
using TourEd.Lib.Abstractions.Options;
using TourEd.Lib.Services;

namespace TourEd.Tests;

public sealed class TouringenStampingPointImportServiceTests
{
    private const long RelationId = 14773147;
    private static readonly Uri RelationApiUri = new("https://api.openstreetmap.org/api/0.6/relation/14773147/full");
    private static readonly Uri RelationPublicUri = new("https://www.openstreetmap.org/relation/14773147");

    private static readonly string[] NaturalTreasures =
    [
        "Urwaldpfad Leutenberg",
        "Bronzeteufel",
        "Hünenburg",
        "Obstpavillon am Schlachtenberg",
        "Haus des Gastes",
        "12 Apostel",
        "Stutenhauswiese",
        "Ausblick Wenigentaft"
    ];

    [Fact]
    public async Task DownloadsCompleteOsmRelationAndOfficialGpxArchives()
    {
        var archives = CreateArchives(NaturalTreasures, standardCount: 430);
        var service = CreateService(archives);

        var snapshot = await service.DownloadStampingPointsAsync();

        Assert.Equal(451, snapshot.Points.Count);
        AssertSeries(snapshot, StampingSeries.TouringenStandardId, 430);
        AssertSeries(snapshot, StampingSeries.TouringenNaturalTreasuresId, 8);
        AssertSeries(snapshot, StampingSeries.TouringenRhoenFamilyTrailsId, 13);

        var numberOnePoints = snapshot.Points.Where(point => point.Number == 1).ToArray();
        Assert.Equal(3, numberOnePoints.Length);
        Assert.Equal(3, numberOnePoints.Select(point => point.SeriesId).Distinct().Count());

        var standardOne = numberOnePoints.Single(point => point.SeriesId == StampingSeries.TouringenStandardId);
        Assert.Equal("osm-node-1001", standardOne.ExternalId);
        Assert.Equal("Standard point 1", standardOne.Name);

        var naturalOne = numberOnePoints.Single(point => point.SeriesId == StampingSeries.TouringenNaturalTreasuresId);
        Assert.Equal("Urwaldpfad Leutenberg", naturalOne.Name);
        Assert.Equal("naturschaetze-1", naturalOne.ExternalId);

        Assert.Equal(RelationPublicUri, snapshot.SourceUri);
        Assert.Equal("© OpenStreetMap contributors", snapshot.Attribution);
        Assert.Equal("Open Data Commons Open Database License (ODbL) 1.0", snapshot.LicenseName);
        Assert.Equal("45", snapshot.Revision);
    }

    [Fact]
    public async Task RejectsIncompleteOsmRelation()
    {
        var archives = CreateArchives(NaturalTreasures, standardCount: 429);
        var service = CreateService(archives);

        var exception = await Assert.ThrowsAsync<InvalidDataException>(() => service.DownloadStampingPointsAsync());
        Assert.Contains("must contain every number from 1 through 430", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnknownNaturalTreasureFailsClosed()
    {
        var names = NaturalTreasures.ToArray();
        names[0] = "Unexpected new treasure";
        var service = CreateService(CreateArchives(names, standardCount: 430));

        var exception = await Assert.ThrowsAsync<InvalidDataException>(() => service.DownloadStampingPointsAsync());
        Assert.Contains("explicit source correction map", exception.Message, StringComparison.Ordinal);
    }

    private static void AssertSeries(StampingPointSourceSnapshot snapshot, int seriesId, int count)
    {
        var points = snapshot.Points.Where(point => point.SeriesId == seriesId).OrderBy(point => point.Number).ToArray();
        Assert.Equal(count, points.Length);
        Assert.Equal(Enumerable.Range(1, count), points.Select(point => point.Number!.Value));
    }

    private static TouringenStampingPointImportService CreateService(IReadOnlyDictionary<string, byte[]> endpoints)
    {
        var configuration = new TouringenWebsiteConfiguration
        {
            RelationId = RelationId,
            RelationApiUri = RelationApiUri,
            RelationPublicUri = RelationPublicUri,
            StempelstellenUri = new Uri("https://www.touringen.de/stempelstellen"),
            NaturalTreasuresGpxUri = new Uri("https://www.touringen.de/natural.zip"),
            RhoenFamilyTrailsGpxUri = new Uri("https://www.touringen.de/rhoen.zip"),
            MaxDownloadBytes = 5 * 1024 * 1024
        };
        return new TouringenStampingPointImportService(
            new HttpClient(new MockHttpHandler(endpoints)),
            configuration);
    }

    private static IReadOnlyDictionary<string, byte[]> CreateArchives(IReadOnlyList<string> naturalTreasures, int standardCount)
        => new Dictionary<string, byte[]>
        {
            [RelationApiUri.AbsoluteUri] = CreateOsm(standardCount),
            ["https://www.touringen.de/natural.zip"] = CreateArchive(naturalTreasures.Select(name =>
                ($"Touringen Sonderstempel {name}.gpx", $"Touringen Sonderstempel {name}"))),
            ["https://www.touringen.de/rhoen.zip"] = CreateArchive(Enumerable.Range(1, 13).Select(number =>
                ($"{number:00}_Rhoen_point_{number}.gpx", $"Rhön point {number}")))
        };

    private static byte[] CreateOsm(int pointCount)
    {
        using var stream = new MemoryStream();
        using var writer = XmlWriter.Create(stream, new XmlWriterSettings
        {
            Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            Indent = false
        });
        writer.WriteStartElement("osm");
        for (var number = 1; number <= pointCount; number++)
        {
            WriteNode(writer, 1000 + number, number, $"Standard point {number}");
        }
        writer.WriteStartElement("relation");
        writer.WriteAttributeString("id", RelationId.ToString());
        writer.WriteAttributeString("version", "45");
        writer.WriteAttributeString("timestamp", "2026-08-31T15:14:00Z");
        for (var number = 1; number <= pointCount; number++)
        {
            writer.WriteStartElement("member");
            writer.WriteAttributeString("type", "node");
            writer.WriteAttributeString("ref", (1000 + number).ToString());
            writer.WriteAttributeString("role", "");
            writer.WriteEndElement();
        }
        WriteTag(writer, "name", "Stempelstellen Touringen");
        WriteTag(writer, "operator", "Touringen");
        WriteTag(writer, "checkpoint", "hiking");
        WriteTag(writer, "checkpoint:type", "stamp");
        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.Flush();
        return stream.ToArray();
    }

    private static void WriteNode(XmlWriter writer, int nodeId, int number, string name)
    {
        writer.WriteStartElement("node");
        writer.WriteAttributeString("id", nodeId.ToString());
        writer.WriteAttributeString("lat", (50.0 + number * 0.001).ToString("F3", System.Globalization.CultureInfo.InvariantCulture));
        writer.WriteAttributeString("lon", (10.0 + number * 0.001).ToString("F3", System.Globalization.CultureInfo.InvariantCulture));
        WriteTag(writer, "checkpoint", "hiking");
        WriteTag(writer, "checkpoint:type", "stamp");
        WriteTag(writer, "operator", "Touringen");
        WriteTag(writer, "ref", number.ToString());
        WriteTag(writer, "name", name);
        writer.WriteEndElement();
    }

    private static void WriteTag(XmlWriter writer, string key, string value)
    {
        writer.WriteStartElement("tag");
        writer.WriteAttributeString("k", key);
        writer.WriteAttributeString("v", value);
        writer.WriteEndElement();
    }

    private static byte[] CreateArchive(IEnumerable<(string FileName, string PointName)> entries)
    {
        using var output = new MemoryStream();
        using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (fileName, pointName) in entries)
            {
                var entry = archive.CreateEntry(fileName);
                using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
                writer.Write($"<?xml version=\"1.0\" encoding=\"UTF-8\"?><gpx xmlns=\"http://www.topografix.com/GPX/1/1\"><wpt lat=\"50.1\" lon=\"11.2\"><name>{WebUtility.HtmlEncode(pointName)}</name></wpt></gpx>");
            }
        }
        return output.ToArray();
    }

    private sealed class MockHttpHandler(IReadOnlyDictionary<string, byte[]> responses) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var url = request.RequestUri!.AbsoluteUri;
            if (responses.TryGetValue(url, out var bytes))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(bytes)
                });
            }
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }
}
