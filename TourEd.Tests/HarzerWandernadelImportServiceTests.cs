using System.Globalization;
using System.Net;
using System.Text;
using System.Xml;
using TourEd.Lib.Abstractions.Models;
using TourEd.Lib.Abstractions.Options;
using TourEd.Lib.Services;

namespace TourEd.Tests;

public sealed class HarzerWandernadelImportServiceTests
{
    private const long RelationId = 148007;
    private static readonly Uri RelationApiUri = new("https://api.openstreetmap.org/api/0.6/relation/148007/full");
    private static readonly Uri RelationPublicUri = new("https://www.openstreetmap.org/relation/148007");

    [Fact]
    public async Task DownloadsCompleteOsmRelationAndSelectsSummerLocationFor69()
    {
        var handler = new HwnHttpMessageHandler(CreateOsm(222, includeWinter69: true));
        var service = CreateService(handler);

        var snapshot = await service.DownloadStampingPointsAsync();

        Assert.Equal(222, snapshot.Points.Count);
        var first = snapshot.Points[0];
        Assert.Equal(StampingProvider.HarzerWandernadelId, first.ProviderId);
        Assert.Equal(1, first.Number);
        Assert.Equal(1, first.Code);
        Assert.Equal("osm-node-1001", first.ExternalId);
        Assert.Equal(10.001m, first.Longitude);
        Assert.Equal(51.001m, first.Latitude);
        Assert.Equal("Punkt 1", first.Name);
        var point69 = snapshot.Points.Single(point => point.Number == 69);
        Assert.Equal("Sommerpunkt 69", point69.Name);
        Assert.Equal("osm-node-1069", point69.ExternalId);
        Assert.Equal(RelationPublicUri, snapshot.SourceUri);
        Assert.Equal("© OpenStreetMap contributors", snapshot.Attribution);
        Assert.Equal("Open Data Commons Open Database License (ODbL) 1.0", snapshot.LicenseName);
        Assert.Equal("44", snapshot.Revision);
        Assert.Equal(new DateTime(2026, 3, 9, 22, 17, 30, DateTimeKind.Utc), snapshot.SourceUpdatedAt);
        Assert.Equal([RelationApiUri], handler.RequestedUris);
    }

    [Fact]
    public async Task RejectsIncompleteOsmRelationBeforeReturningAnyPoints()
    {
        var service = CreateService(new HwnHttpMessageHandler(CreateOsm(221, includeWinter69: true)));

        var exception = await Assert.ThrowsAsync<InvalidDataException>(
            () => service.DownloadStampingPointsAsync());

        Assert.Contains("missing: 222", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RejectsSeasonalPointWithoutExactlyOneSummerLocation()
    {
        var service = CreateService(new HwnHttpMessageHandler(CreateOsm(222, includeWinter69: false, make69WinterOnly: true)));

        var exception = await Assert.ThrowsAsync<InvalidDataException>(
            () => service.DownloadStampingPointsAsync());

        Assert.Contains("missing: 69", exception.Message, StringComparison.Ordinal);
    }

    private static HarzerWandernadelImportService CreateService(HttpMessageHandler handler)
        => new(
            new HttpClient(handler),
            new HarzerWandernadelConfiguration
            {
                RelationId = RelationId,
                RelationApiUri = RelationApiUri,
                RelationPublicUri = RelationPublicUri,
                MaxDownloadBytes = 2 * 1024 * 1024
            });

    private static byte[] CreateOsm(
        int pointCount,
        bool includeWinter69,
        bool make69WinterOnly = false)
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
            WriteNode(
                writer,
                1000 + number,
                number,
                number == 69 ? "Sommerpunkt 69" : $"Punkt {number}",
                number == 69 ? make69WinterOnly ? "winter" : "spring;summer;autumn" : null);
        }
        if (includeWinter69 && pointCount >= 69)
        {
            WriteNode(writer, 9069, 69, "Winterpunkt 69", "winter");
        }

        writer.WriteStartElement("relation");
        writer.WriteAttributeString("id", RelationId.ToString(CultureInfo.InvariantCulture));
        writer.WriteAttributeString("version", "44");
        writer.WriteAttributeString("timestamp", "2026-03-09T22:17:30Z");
        for (var number = 1; number <= pointCount; number++)
        {
            WriteMember(writer, 1000 + number);
        }
        if (includeWinter69 && pointCount >= 69)
        {
            WriteMember(writer, 9069);
        }
        WriteTag(writer, "name", "HWN Stempelstellen");
        WriteTag(writer, "operator", "Harzer Wandernadel");
        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.Flush();
        return stream.ToArray();
    }

    private static void WriteNode(
        XmlWriter writer,
        long nodeId,
        int number,
        string name,
        string? seasonal)
    {
        writer.WriteStartElement("node");
        writer.WriteAttributeString("id", nodeId.ToString(CultureInfo.InvariantCulture));
        writer.WriteAttributeString("lat", (51m + number / 1000m).ToString(CultureInfo.InvariantCulture));
        writer.WriteAttributeString("lon", (10m + number / 1000m).ToString(CultureInfo.InvariantCulture));
        WriteTag(writer, "checkpoint", "hiking");
        WriteTag(writer, "checkpoint:type", "stamp");
        WriteTag(writer, "operator", "Harzer Wandernadel");
        WriteTag(writer, "ref", $"HWN {number:D3}");
        WriteTag(writer, "name", $"HWN {number:D3} - {name}");
        if (seasonal is not null)
        {
            WriteTag(writer, "seasonal", seasonal);
        }
        writer.WriteEndElement();
    }

    private static void WriteMember(XmlWriter writer, long nodeId)
    {
        writer.WriteStartElement("member");
        writer.WriteAttributeString("type", "node");
        writer.WriteAttributeString("ref", nodeId.ToString(CultureInfo.InvariantCulture));
        writer.WriteEndElement();
    }

    private static void WriteTag(XmlWriter writer, string key, string value)
    {
        writer.WriteStartElement("tag");
        writer.WriteAttributeString("k", key);
        writer.WriteAttributeString("v", value);
        writer.WriteEndElement();
    }

    private sealed class HwnHttpMessageHandler(byte[] relation) : HttpMessageHandler
    {
        public List<Uri> RequestedUris { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var uri = request.RequestUri ?? throw new InvalidOperationException("Request URI is missing.");
            RequestedUris.Add(uri);
            if (uri != RelationApiUri)
            {
                throw new InvalidOperationException($"Unexpected request URI: {uri}");
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(relation)
            });
        }
    }
}
