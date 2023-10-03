using System.Text;

namespace TourEd.Tests.Fixtures;

public class XmlImportTestsFixture : IDisposable
{
    public async Task<Stream> GetTestCaseStreamAsync()
    {
        var stream = new MemoryStream();
        var writer = new StreamWriter(stream);
        await writer.WriteAsync("""
                                <?xml version="1.0" encoding="UTF-8" standalone="no"?>
                                <gpx xmlns="http://www.topografix.com/GPX/1/0" version="1.0" creator="Creator">
                                   <wpt lat="50.95751" lon="11.01032">
                                       <ele>224</ele>
                                       <name>Stempelstelle Nr.216 Domblick</name>
                                       <desc>Stempelstelle Nr.216 Domblick</desc>
                                   </wpt>
                                   <wpt lat="50.94283" lon="11.00882">
                                       <ele>313</ele>
                                       <name>Stempelstelle Nr.215 NaturErlebnisGarten Fuchsfarm</name>
                                       <desc>Stempelstelle Nr.215 NaturErlebnisGarten Fuchsfarm</desc>
                                   </wpt>
                                   <wpt lat="50.9568" lon="11.01436">
                                       <ele>258</ele>
                                       <name>Stempelstelle Nr.217 Waldspielplatz im Steiger</name>
                                       <desc>Stempelstelle Nr.217 Waldspielplatz im Steiger</desc>
                                   </wpt>
                                   <wpt lat="51.01072" lon="10.91045">
                                       <ele>272</ele>
                                       <name>Stempelstelle Nr.218 Grundmühle</name>
                                       <desc>Stempelstelle Nr.218 Grundmühle</desc>
                                   </wpt>
                                </gpx>
                                """);
        await writer.FlushAsync();
        stream.Position = 0;
        return stream;
    }

    public void Dispose() { }
}
