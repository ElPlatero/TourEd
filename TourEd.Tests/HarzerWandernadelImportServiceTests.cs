using System.Globalization;
using System.IO.Compression;
using System.Net;
using System.Text;
using System.Xml;
using TourEd.Lib.Abstractions.Models;
using TourEd.Lib.Abstractions.Options;
using TourEd.Lib.Services;

namespace TourEd.Tests;

public sealed class HarzerWandernadelImportServiceTests
{
    private static readonly Uri DownloadPageUri = new("https://www.harzer-wandernadel.de/stempelstellen/gps-download/");
    private static readonly Uri OverviewUri = new("https://www.harzer-wandernadel.de/stempelstellen/uebersichtstabelle/");
    private static readonly Uri ArchiveUri = new("https://www.harzer-wandernadel.de/files/GPX-Daten-Stempelstellen.zip");

    [Fact]
    public async Task DownloadsCurrentNamesAndCoordinatesForCompleteRegularNumberSet()
    {
        var handler = new HwnHttpMessageHandler(CreateArchive(222));
        var service = CreateService(handler);

        var points = await service.DownloadStampingPointsAsync();

        Assert.Equal(222, points.Count);
        var first = points[0];
        Assert.Equal(StampingProvider.HarzerWandernadelId, first.ProviderId);
        Assert.Equal(1, first.Number);
        Assert.Equal(1, first.Code);
        Assert.Equal("HWN001", first.ExternalId);
        Assert.Equal(10.001m, first.Longitude);
        Assert.Equal(51.001m, first.Latitude);
        Assert.Equal("Aktueller Name 45", points[44].Name);
        Assert.Equal([DownloadPageUri, OverviewUri, ArchiveUri], handler.RequestedUris);
    }

    [Fact]
    public async Task RejectsIncompleteGpxBeforeReturningAnyPoints()
    {
        var service = CreateService(new HwnHttpMessageHandler(CreateArchive(221)));

        var exception = await Assert.ThrowsAsync<InvalidDataException>(
            () => service.DownloadStampingPointsAsync());

        Assert.Contains("each number from 1 through 222", exception.Message, StringComparison.Ordinal);
    }

    private static HarzerWandernadelImportService CreateService(HttpMessageHandler handler)
        => new(
            new HttpClient(handler),
            new HarzerWandernadelConfiguration
            {
                DownloadPageUri = DownloadPageUri,
                OverviewUri = OverviewUri,
                MaxDownloadBytes = 2 * 1024 * 1024
            });

    private static byte[] CreateArchive(int pointCount)
    {
        using var archiveStream = new MemoryStream();
        using (var archive = new ZipArchive(archiveStream, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = archive.CreateEntry("HWN.gpx");
            using var entryStream = entry.Open();
            using var writer = XmlWriter.Create(entryStream, new XmlWriterSettings
            {
                Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                Indent = false
            });
            writer.WriteStartElement("gpx", "http://www.topografix.com/GPX/1/1");
            for (var number = 1; number <= pointCount; number++)
            {
                writer.WriteStartElement("wpt", "http://www.topografix.com/GPX/1/1");
                writer.WriteAttributeString("lat", (51m + number / 1000m).ToString(CultureInfo.InvariantCulture));
                writer.WriteAttributeString("lon", (10m + number / 1000m).ToString(CultureInfo.InvariantCulture));
                writer.WriteElementString("name", "http://www.topografix.com/GPX/1/1", $"HWN{number:D3} GPX Name {number}");
                writer.WriteElementString("desc", "http://www.topografix.com/GPX/1/1", $"GPX Description {number}");
                writer.WriteEndElement();
            }
            writer.WriteEndElement();
        }
        return archiveStream.ToArray();
    }

    private sealed class HwnHttpMessageHandler(byte[] archive) : HttpMessageHandler
    {
        public List<Uri> RequestedUris { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var uri = request.RequestUri ?? throw new InvalidOperationException("Request URI is missing.");
            RequestedUris.Add(uri);
            HttpContent content = uri == DownloadPageUri
                ? new StringContent($"<a href=\"{ArchiveUri}\">GPX</a>", Encoding.UTF8, "text/html")
                : uri == OverviewUri
                    ? new StringContent(CreateOverview(), Encoding.UTF8, "text/html")
                    : uri == ArchiveUri
                        ? new ByteArrayContent(archive)
                        : throw new InvalidOperationException($"Unexpected request URI: {uri}");
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
        }

        private static string CreateOverview()
        {
            var rows = new StringBuilder("<table><tbody>");
            for (var number = 1; number <= 222; number++)
            {
                var name = number == 45 ? "Aktu&shy;eller&nbsp;Name 45" : $"Aktueller Name {number}";
                rows.Append(CultureInfo.InvariantCulture, $"<tr><td>{number}</td><td><a>{name}</a></td><td>Ort</td></tr>");
            }
            return rows.Append("</tbody></table>").ToString();
        }
    }
}
