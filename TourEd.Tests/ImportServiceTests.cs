using TourEd.Tests.Fixtures;
using Xunit.Abstractions;

namespace TourEd.Tests;

public class ImportServiceTests : IClassFixture<XmlImportTestsFixture>
{
    private readonly XmlImportTestsFixture _fixture;
    private readonly ITestOutputHelper _testOutputHelper;

    public ImportServiceTests(XmlImportTestsFixture fixture, ITestOutputHelper testOutputHelper)
    {
        _fixture = fixture;
        _testOutputHelper = testOutputHelper;
    }
   
}