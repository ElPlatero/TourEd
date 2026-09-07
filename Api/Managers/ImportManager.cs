using System.Globalization;
using System.Runtime.Serialization;
using System.Text.Json;
using Api.Repositories;
using Microsoft.Extensions.Options;
using TourEd.Lib.Abstractions;
using TourEd.Lib.Abstractions.Interfaces;
using TourEd.Lib.Abstractions.Interfaces.Services;
using TourEd.Lib.Abstractions.Models;
using TourEd.Lib.Abstractions.Options;
using TourEd.Lib.Extensions;

namespace Api.Managers;

public class ImportManager : IImportManager
{
    private static readonly HashSet<int> TouringenNaturalTreasureAreaIds = [102, 103, 104, 105, 106, 107, 108, 109];
    private readonly Func<User?> _getCurrentUser;
    private readonly IHtmlParsingService _htmlParser;
    private readonly IHarzerWandernadelImportService _harzerWandernadelImporter;
    private readonly ITouringenStampingPointImportService _touringenStampingPointImporter;
    private readonly IImportService<HikingTour> _hikingToursImporter;
    private readonly TouredRepository _repository;
    private readonly Func<IUnitOfWork> _createUnitOfWork;
    private readonly TouringenWebsiteConfiguration _configuration;

    public ImportManager(IHttpContextAccessor httpContextAccessor, IHtmlParsingService htmlParser, IHarzerWandernadelImportService harzerWandernadelImporter, ITouringenStampingPointImportService touringenStampingPointImporter, IOptions<TouringenWebsiteConfiguration> options, IImportService<HikingTour> hikingToursImporter, TouredRepository repository, Func<IUnitOfWork> createUnitOfWork)
    {
        _getCurrentUser = () => httpContextAccessor.HttpContext?.User.GetUser();
        _htmlParser = htmlParser;
        _harzerWandernadelImporter = harzerWandernadelImporter;
        _touringenStampingPointImporter = touringenStampingPointImporter;
        _hikingToursImporter = hikingToursImporter;
        _repository = repository;
        _createUnitOfWork = createUnitOfWork;
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

        var snapshot = await stampingPointSnapshotTask;
        var standardImportData = importData
            .Where(area => !TouringenNaturalTreasureAreaIds.Contains(area.Id))
            .ToArray();

        var hikingTours = _hikingToursImporter.Import(standardImportData).ToArray();
        TouredRepository.ValidateStampingPointSourceImport(StampingProvider.TouringenId, snapshot);
        var standardNumbers = snapshot.Points
            .Where(point => point.SeriesId == StampingSeries.TouringenStandardId && point.Number.HasValue)
            .Select(point => point.Number!.Value).ToHashSet();
        var numbersByExternalId = standardImportData
            .SelectMany(area => area.Touren.SelectMany(tour => tour.StampPoints))
            .Union(standardImportData.SelectMany(area => area.OrphanedStampPoints))
            .DistinctBy(point => point.Id)
            .ToDictionary(point => point.Id, point => point.StampPointNumber);
        _ = hikingTours.ToDictionary(tour => tour.Id);
        if (numbersByExternalId.Values.Any(number => !standardNumbers.Contains(number)) ||
            hikingTours.SelectMany(tour => tour.StampingPoints)
                .Any(point => !numbersByExternalId.ContainsKey(point.StampingPointId)))
        {
            throw new InvalidDataException("Tour relationships must reference imported standard stamping points.");
        }

        // All external data and relationships have been parsed and validated.
        // Only resolving generated IDs and persisting the complete import need a transaction.
        using var unitOfWork = _createUnitOfWork();
        var savedStampingPoints = await _repository.SaveStampingPointSourceImportAsync(
            StampingProvider.TouringenId, snapshot, hikingTours.Length, cancellationToken);
        var stampingPointIdsByNumber = savedStampingPoints
            .Where(point => point.SeriesId == StampingSeries.TouringenStandardId && point.Number.HasValue)
            .ToDictionary(point => point.Number!.Value, point => point.Id);

        foreach (var hikingTour in hikingTours)
        {
            hikingTour.StampingPoints = hikingTour.StampingPoints.Select(point => new SortedStampingPoint(point.Position)
            {
                StampingPointId = stampingPointIdsByNumber[numbersByExternalId[point.StampingPointId]],
                Tour = hikingTour
            }).ToList();
        }

        await _repository.SaveHikingToursAsync(hikingTours);
        await unitOfWork.CommitAsync();
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
        TouredRepository.ValidateStampingPointSourceImport(StampingProvider.HarzerWandernadelId, snapshot);
        using var unitOfWork = _createUnitOfWork();
        await _repository.SaveStampingPointSourceImportAsync(
            StampingProvider.HarzerWandernadelId,
            snapshot,
            cancellationToken: cancellationToken);
        await unitOfWork.CommitAsync();
    }

    public async Task<UserDataImportResult> ImportUserDataAsync(Stream stream)
    {
        var user = _getCurrentUser() ?? throw new NotSupportedException("This operation needs authorization.");
        using var reader = new StreamReader(stream);
        List<(int Line, int Number, DateTime? Visited, bool HasTime)> visits = [];
        List<UserDataImportError> errors = [];
        HashSet<int> numbers = [];
        var lineNumber = 0;
        while (await reader.ReadLineAsync() is { } line)
        {
            lineNumber++;
            var fields = line.Split(';');
            if (fields.Length != 3 || fields[0].Length is < 1 or > 3 ||
                fields[0].Any(c => c is < '0' or > '9') ||
                !int.TryParse(fields[0], NumberStyles.None, CultureInfo.InvariantCulture, out var number) || number == 0)
            {
                errors.Add(new(lineNumber, "Expected number (1–999);date (dd.MM.yyyy or empty);time (HH:mm or empty)."));
                continue;
            }
            if (!numbers.Add(number))
            {
                errors.Add(new(lineNumber, "Duplicate stamping point number."));
                continue;
            }
            DateTime? visited = null;
            if (fields[1].Length > 0)
            {
                if (!DateTime.TryParseExact(fields[1], "dd.MM.yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                {
                    errors.Add(new(lineNumber, "Invalid date; expected dd.MM.yyyy."));
                    continue;
                }
                visited = date;
            }
            var hasTime = fields[2].Length > 0;
            if (hasTime)
            {
                if (!visited.HasValue || !TimeOnly.TryParseExact(fields[2], "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var time))
                {
                    errors.Add(new(lineNumber, "Time requires a valid date and HH:mm (00:00–23:59)."));
                    continue;
                }
                visited = visited.Value.Add(time.ToTimeSpan());
            }
            visits.Add((lineNumber, number, visited, hasTime));
        }
        if (lineNumber == 0) errors.Add(new(null, "The file must contain at least one visit."));
        if (errors.Count > 0) return new(0, 0, errors.Count, errors);

        // Parsing finishes before opening the transaction; entitlement checks and all writes stay together.
        using var unitOfWork = _createUnitOfWork();
        var providerFilter = await _repository.GetStampingProviderFilterAsync(userId: user.Id);
        var stampingPointsMap = (await _repository.GetStampingPointsAsync(
                providerFilter: providerFilter,
                seriesSlug: StampingSeries.TouringenStandardSlug,
                stampingPointsNr: visits.Select(p => p.Number).ToArray()))
            .Select(p => p.Point)
            .Where(point => point.Number.HasValue)
            .ToDictionary(point => point.Number!.Value);
        List<UserVisit> importedVisits = [];
        foreach (var visit in visits)
        {
            if (!stampingPointsMap.TryGetValue(visit.Number, out var stampingPoint))
            {
                errors.Add(new(visit.Line, "Unknown stamping point number in the entitled default provider's standard series."));
                continue;
            }
            importedVisits.Add(new UserVisit
            {
                StampingPointId = stampingPoint.Id,
                UserId = user.Id,
                Visited = visit.Visited,
                HasVisitedTime = visit.HasTime
            });
        }
        if (errors.Count > 0) return new(0, 0, errors.Count, errors);
        var imported = await _repository.SaveUserDataAsync(importedVisits.ToArray());
        await unitOfWork.CommitAsync();
        return new(imported, visits.Count - imported, 0, []);
    }
}
