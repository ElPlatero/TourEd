using System.IO.Compression;
using System.Net;
using System.Text;
using TourEd.Lib.Abstractions.Models;
using TourEd.Lib.Abstractions.Options;
using TourEd.Lib.Services;

namespace TourEd.Tests;

public sealed class TouringenStampingPointImportServiceTests
{
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
    public async Task OfficialArchiveShapesProduceThreeDistinctNumberNamespaces()
    {
        var archives = CreateArchives(NaturalTreasures);
        var service = CreateService(archives);

        var snapshot = await service.DownloadStampingPointsAsync();

        Assert.Equal(451, snapshot.Points.Count);
        AssertSeries(snapshot, StampingSeries.TouringenStandardId, 430);
        AssertSeries(snapshot, StampingSeries.TouringenNaturalTreasuresId, 8);
        AssertSeries(snapshot, StampingSeries.TouringenRhoenFamilyTrailsId, 13);
        var numberOnePoints = snapshot.Points.Where(point => point.Number == 1).ToArray();
        Assert.Equal(3, numberOnePoints.Length);
        Assert.Equal(3, numberOnePoints.Select(point => point.SeriesId).Distinct().Count());
        Assert.Equal("Urwaldpfad Leutenberg", numberOnePoints.Single(point => point.SeriesId == StampingSeries.TouringenNaturalTreasuresId).Name);
    }

    [Fact]
    public async Task UnknownNaturalTreasureFailsClosed()
    {
        var names = NaturalTreasures.ToArray();
        names[0] = "Unexpected new treasure";
        var service = CreateService(CreateArchives(names));

        var exception = await Assert.ThrowsAsync<InvalidDataException>(() => service.DownloadStampingPointsAsync());

        Assert.Contains("explicit source correction map", exception.Message, StringComparison.Ordinal);
    }

    private static void AssertSeries(TouringenStampingPointSnapshot snapshot, int seriesId, int count)
    {
        var points = snapshot.Points.Where(point => point.SeriesId == seriesId).OrderBy(point => point.Number).ToArray();
        Assert.Equal(count, points.Length);
        Assert.Equal(Enumerable.Range(1, count), points.Select(point => point.Number!.Value));
    }

    private static TouringenStampingPointImportService CreateService(IReadOnlyDictionary<string, byte[]> archives)
    {
        var configuration = new TouringenWebsiteConfiguration
        {
            StempelstellenUri = new Uri("https://www.touringen.de/stempelstellen"),
            StandardGpxUri = new Uri("https://www.touringen.de/standard.zip"),
            NaturalTreasuresGpxUri = new Uri("https://www.touringen.de/natural.zip"),
            RhoenFamilyTrailsGpxUri = new Uri("https://www.touringen.de/rhoen.zip"),
            MaxDownloadBytes = 5 * 1024 * 1024
        };
        return new TouringenStampingPointImportService(
            new HttpClient(new ArchiveHandler(archives)),
            configuration);
    }

    private static IReadOnlyDictionary<string, byte[]> CreateArchives(IReadOnlyList<string> naturalTreasures)
        => new Dictionary<string, byte[]>
        {
            ["/standard.zip"] = CreateArchive(Enumerable.Range(1, 430).Select(number =>
                ($"Touringen Stempelstellen Nr. {number} Standard point {number}.gpx", $"Standard point {number}"))),
            ["/natural.zip"] = CreateArchive(naturalTreasures.Select(name =>
                ($"Touringen Sonderstempel {name}.gpx", $"Touringen Sonderstempel {name}"))),
            ["/rhoen.zip"] = CreateArchive(Enumerable.Range(1, 13).Select(number =>
                ($"{number:00}_Rhoen_point_{number}.gpx", $"Rhön point {number}")))
        };

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

    private sealed class ArchiveHandler(IReadOnlyDictionary<string, byte[]> archives) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri?.AbsolutePath ?? string.Empty;
            return Task.FromResult(archives.TryGetValue(path, out var archive)
                ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(archive) }
                : new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }
}
