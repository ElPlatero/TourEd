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
    private static readonly HashSet<int> TouringenNaturalTreasureAreaIds = [102, 103, 104, 105, 106, 107, 108, 109];
    private readonly Func<User?> _getCurrentUser;
    private readonly IHtmlParsingService _htmlParser;
    private readonly IHarzerWandernadelImportService _harzerWandernadelImporter;
    private readonly ITouringenStampingPointImportService _touringenStampingPointImporter;
    private readonly IImportService<HikingTour> _hikingToursImporter;
    private readonly TouredRepository _repository;
    private readonly TouringenWebsiteConfiguration _configuration;

    public ImportManager(IHttpContextAccessor httpContextAccessor, IHtmlParsingService htmlParser, IHarzerWandernadelImportService harzerWandernadelImporter, ITouringenStampingPointImportService touringenStampingPointImporter, IOptions<TouringenWebsiteConfiguration> options, IImportService<HikingTour> hikingToursImporter, TouredRepository repository)
    {
        _getCurrentUser = () => httpContextAccessor.HttpContext?.User.GetUser();
        _htmlParser = htmlParser;
        _harzerWandernadelImporter = harzerWandernadelImporter;
        _touringenStampingPointImporter = touringenStampingPointImporter;
        _hikingToursImporter = hikingToursImporter;
        _repository = repository;
        _configuration = options.Value;
    }

    public async Task ImportTouringenDataAsync(CancellationToken cancellationToken = default)
    {
        var rawDataTask = _htmlParser.GetRawDmoStringAsync(_configuration.StempelstellenUri);
        var stampingPointSnapshotTask = _touringenStampingPointImporter.DownloadStampingPointsAsync(cancellationToken);
        await Task.WhenAll(rawDataTask, stampingPointSnapshotTask);
        var rawData = await rawDataTask;
        if (string.IsNullOrWhiteSpace(rawData))
        {
            throw new SerializationException("no data");
        }

        var importData = JsonSerializer.Deserialize<RawArea[]>(rawData);
        if (importData == null)
        {
            throw new SerializationException("no data");
        }

        var stampingPoints = (await stampingPointSnapshotTask).Points.ToArray();
        var standardImportData = importData
            .Where(area => !TouringenNaturalTreasureAreaIds.Contains(area.Id))
            .ToArray();
        var ambiguousVisitedNumbers = await _repository.GetAmbiguousVisitedTouringenNumbersAsync(stampingPoints, cancellationToken);
        if (ambiguousVisitedNumbers.Count > 0)
        {
            throw new InvalidOperationException(
                $"Touringen points {string.Join(", ", ambiguousVisitedNumbers)} contain visits but differ from the canonical standard GPX data. Resolve whether those visits belong to the standard or Naturschätze series before importing.");
        }
        var savedStampingPoints = await _repository.SaveStampingPointsAsync(stampingPoints);
        var stampingPointIdsByNumber = savedStampingPoints
            .Where(p => p.SeriesId == StampingSeries.TouringenStandardId && p.Number.HasValue)
            .ToDictionary(p => p.Number!.Value, p => p.Id);
        var stampingPointIdsByExternalId = standardImportData
            .SelectMany(p => p.Touren.SelectMany(q => q.StampPoints))
            .Union(standardImportData.SelectMany(p => p.OrphanedStampPoints))
            .DistinctBy(p => p.Id)
            .ToDictionary(
                p => p.Id.ToString(CultureInfo.InvariantCulture),
                p => stampingPointIdsByNumber[p.StampPointNumber]);

        var hikingTours = _hikingToursImporter.Import(standardImportData).ToArray();
        foreach (var hikingTour in hikingTours)
        {
            hikingTour.StampingPoints = hikingTour.StampingPoints.Select(point => new SortedStampingPoint(point.Position)
            {
                StampingPointId = stampingPointIdsByExternalId[point.StampingPointId.ToString(CultureInfo.InvariantCulture)],
                Tour = hikingTour
            }).ToList();
        }

        await _repository.SaveHikingToursAsync(hikingTours);

        await _repository.SaveImportAsync(stampingPoints.Length, hikingTours.Length);
    }

    public async Task ImportHarzerWandernadelDataAsync(CancellationToken cancellationToken = default)
    {
        var snapshot = await _harzerWandernadelImporter.DownloadStampingPointsAsync(cancellationToken);
        var expectedNumbers = Enumerable.Range(1, 222);
        if (snapshot.Points.Count != 222 ||
            !snapshot.Points.Where(point => point.Number.HasValue).Select(point => point.Number!.Value).OrderBy(number => number).SequenceEqual(expectedNumbers))
        {
            throw new InvalidDataException("The HWN import must contain every regular number from 1 through 222 exactly once.");
        }
        await _repository.SaveStampingPointSourceImportAsync(
            StampingProvider.HarzerWandernadelId,
            snapshot,
            cancellationToken);
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

        var providerFilter = await _repository.GetStampingProviderFilterAsync(userId: user.Id);
        var stampingPointsMap = (await _repository.GetStampingPointsAsync(
                providerFilter: providerFilter,
                seriesSlug: StampingSeries.TouringenStandardSlug,
                stampingPointsNr: visits.Select(p => p.StampingPointNumber).ToArray()))
            .Select(p => p.Point)
            .Where(point => point.Number.HasValue)
            .ToDictionary(point => point.Number!.Value);
        List<UserVisit> importedVisits = new();
        foreach (var visit in visits)
        {
            if (!stampingPointsMap.TryGetValue(visit.StampingPointNumber, out var stampingPoint)) continue;
            importedVisits.Add(new UserVisit
            {
                StampingPointId = stampingPoint.Id,
                UserId = user.Id,
                Visited = visit.Visited,
                HasVisitedTime = visit.Visited.HasValue
            });
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
