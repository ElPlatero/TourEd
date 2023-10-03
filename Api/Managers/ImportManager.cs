using System.Globalization;
using System.Runtime.Serialization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Api.Repositories;
using Microsoft.Extensions.Options;
using TourEd.Lib.Abstractions.Interfaces;
using TourEd.Lib.Abstractions.Interfaces.Services;
using TourEd.Lib.Abstractions.Models;
using TourEd.Lib.Abstractions.Options;
using TourEd.Lib.Extensions;

namespace Api.Managers;

public partial class ImportManager : IImportManager
{
    private readonly Func<User?> _getCurrentUser;
    private readonly IHtmlParsingService _htmlParser;
    private readonly IImportService<StampingPoint> _stampingPointsImporter;
    private readonly IImportService<HikingTour> _hikingToursImporter;
    private readonly TouredRepository _repository;
    private readonly TouringenWebsiteConfiguration _configuration;

    public ImportManager(IHttpContextAccessor httpContextAccessor, IHtmlParsingService htmlParser, IOptions<TouringenWebsiteConfiguration> options, IImportService<StampingPoint> stampingPointsImporter, IImportService<HikingTour> hikingToursImporter, TouredRepository repository)
    {
        _getCurrentUser = () => httpContextAccessor.HttpContext?.User.GetUser();
        _htmlParser = htmlParser;
        _stampingPointsImporter = stampingPointsImporter;
        _hikingToursImporter = hikingToursImporter;
        _repository = repository;
        _configuration = options.Value;
    }

    public async Task ImportTouringenDataAsync()
    {
        var rawData = await _htmlParser.GetRawDmoStringAsync(_configuration.StempelstellenUri);
        if (string.IsNullOrWhiteSpace(rawData))
        {
            throw new SerializationException("no data");
        }

        var importData = JsonSerializer.Deserialize<RawArea[]>(rawData);
        if (importData == null)
        {
            throw new SerializationException("no data");
        }

        var stampingPoints = _stampingPointsImporter.Import(importData).ToDictionary(p => p.Id);
        await _repository.SaveStampingPointsAsync(stampingPoints.Values.ToArray());

        var hikingTours = _hikingToursImporter.Import(importData).ToArray();
        await _repository.SaveHikingToursAsync(hikingTours);

        await _repository.SaveImportAsync(stampingPoints.Count, hikingTours.Length);
    }

    public async Task ImportUserDataAsync(Stream stream)
    {
        var user = _getCurrentUser() ?? throw new NotSupportedException("This operation needs authorization.");
        using var reader = new StreamReader(stream);
        List<(int StampingPointNumber, DateTime? Visited)> visits = new();
        while (await reader.ReadLineAsync() is { } line)
        {
            var match = ParseUserDataImportRegex().Match(line);
            if (!match.Success) continue;
            
            visits.Add((Convert.ToInt32(match.Groups[1].Value), GetDateTime(match)));
        }

        var stampingPointsMap = (await _repository.GetStampingPointsAsync(stampingPointsNr: visits.Select(p => p.StampingPointNumber).ToArray())).Select(p => p.Point).GroupBy(p => p.Number).ToDictionary(p => p.Key, p => p.ToList());
        List<UserVisit> importedVisits = new();
        foreach (var visit in visits)
        {
            if (!stampingPointsMap.TryGetValue(visit.StampingPointNumber, out var stampingPoints)) continue;
            importedVisits.AddRange(stampingPoints.Select(p => new UserVisit
            {
                StampingPointId = p.Id,
                UserId = user.Id,
                Visited = visit.Visited
            }));
        }

        await _repository.SaveUserDataAsync(importedVisits.ToArray());
        return;

        static DateTime? GetDateTime(Match m)
        {
            if (m.Groups is [_, _, { Value: { Length: > 0 } }, { Value: { Length: > 0} }])
            {
                return DateTime.ParseExact(m.Groups[2].Value, "dd.MM.yyyy", CultureInfo.InvariantCulture).Add(TimeSpan.Parse(m.Groups[3].Value));
            }

            return null;
        }
    }

    [GeneratedRegex("(\\d{1,3});(\\d{2}\\.\\d{2}\\.\\d{4})?;(\\d{2}:\\d{2})?")]
    private static partial Regex ParseUserDataImportRegex();
}
