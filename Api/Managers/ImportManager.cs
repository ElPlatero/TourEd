using System.Globalization;
using System.Runtime.Serialization;
using System.Text.Json;
using Api.Dto;
using Api.Entities;
using Api.Imports;
using Api.Options;
using Api.Repositories;
using Microsoft.Extensions.Options;

namespace Api.Managers;

public class ImportManager : IImportManager
{
    private static readonly HashSet<int> TouringenNaturalTreasureAreaIds = [102, 103, 104, 105, 106, 107, 108, 109];
    private readonly IHtmlParsingService _htmlParser;
    private readonly IHarzerWandernadelImportService _harzerWandernadelImporter;
    private readonly ITouringenStampingPointImportService _touringenStampingPointImporter;
    private readonly IImportService<HikingTour> _hikingToursImporter;
    private readonly StampingPointRepository _points;
    private readonly UserVisitRepository _visits;
    private readonly StampingProviderRepository _providers;
    private readonly IUnitOfWorkFactory _unitOfWorkFactory;
    private readonly StampingProviderManager _providerManager;
    private readonly TouringenWebsiteConfiguration _configuration;

    public ImportManager(IHtmlParsingService htmlParser, IHarzerWandernadelImportService harzerWandernadelImporter, ITouringenStampingPointImportService touringenStampingPointImporter, IOptions<TouringenWebsiteConfiguration> options, IImportService<HikingTour> hikingToursImporter, StampingPointRepository points, UserVisitRepository visits, StampingProviderRepository providers, IUnitOfWorkFactory unitOfWorkFactory, StampingProviderManager providerManager)
    {
        _htmlParser = htmlParser;
        _harzerWandernadelImporter = harzerWandernadelImporter;
        _touringenStampingPointImporter = touringenStampingPointImporter;
        _hikingToursImporter = hikingToursImporter;
        _points = points;
        _visits = visits;
        _providers = providers;
        _unitOfWorkFactory = unitOfWorkFactory;
        _providerManager = providerManager;
        _configuration = options.Value;
    }

    public async Task ImportTouringenDataAsync(CancellationToken cancellationToken = default)
    {
        var rawDataTask = _htmlParser.GetRawDmoStringAsync(_configuration.StempelstellenUri, cancellationToken);
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
        ValidateSourceImport(StampingProvider.TouringenId, snapshot);
        var standardNumbers = snapshot.Points
            .Where(point => point.SeriesId == StampingSeries.TouringenStandardId && point.Number.HasValue)
            .Select(point => point.Number!.Value).ToHashSet();
        var numbersByExternalId = standardImportData
            .SelectMany(area => area.Touren.SelectMany(tour => tour.StampPoints))
            .Union(standardImportData.SelectMany(area => area.OrphanedStampPoints))
            .DistinctBy(point => point.Id)
            .ToDictionary(point => point.Id, point => point.StampPointNumber);
        var duplicateTourId = hikingTours.GroupBy(tour => tour.Id).FirstOrDefault(group => group.Count() > 1)?.Key;
        if (duplicateTourId is not null)
        {
            throw new InvalidDataException($"The Touringen tour source contains tour {duplicateTourId} more than once.");
        }
        if (numbersByExternalId.Values.Any(number => !standardNumbers.Contains(number)) ||
            hikingTours.SelectMany(tour => tour.StampingPoints)
                .Any(point => !numbersByExternalId.ContainsKey(point.StampingPointId)))
        {
            throw new InvalidDataException("Tour relationships must reference imported standard stamping points.");
        }

        // All external data and relationships have been parsed and validated.
        // Only resolving generated IDs and persisting the complete import need a transaction.
        await using var unitOfWork = await _unitOfWorkFactory.BeginAsync(cancellationToken);
        var savedStampingPoints = await SaveSourceImportAsync(
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

        await _points.SaveHikingToursAsync(hikingTours, cancellationToken);
        await unitOfWork.CommitAsync(cancellationToken);
    }

    public async Task ImportHarzerWandernadelDataAsync(CancellationToken cancellationToken = default)
    {
        var snapshot = await _harzerWandernadelImporter.DownloadStampingPointsAsync(cancellationToken);
        var standardSeries = await _providers.GetSeriesAsync(StampingSeries.HarzerWandernadelStandardId, cancellationToken);
        var expectedCount = standardSeries.ExpectedPointCount
            ?? throw new InvalidOperationException("The HWN standard series has no expected point count.");
        if (snapshot.Points.Count != expectedCount ||
            !snapshot.Points.Where(point => point.Number.HasValue).Select(point => point.Number!.Value).OrderBy(number => number)
                .SequenceEqual(Enumerable.Range(1, expectedCount)))
        {
            throw new InvalidDataException($"The HWN import must contain every regular number from 1 through {expectedCount} exactly once.");
        }
        ValidateSourceImport(StampingProvider.HarzerWandernadelId, snapshot);
        await using var unitOfWork = await _unitOfWorkFactory.BeginAsync(cancellationToken);
        await SaveSourceImportAsync(
            StampingProvider.HarzerWandernadelId,
            snapshot,
            cancellationToken: cancellationToken);
        await unitOfWork.CommitAsync(cancellationToken);
    }

    public async Task<UserDataImportResult> ImportUserDataAsync(
        User user,
        Stream stream,
        CancellationToken cancellationToken = default)
    {
        using var reader = new StreamReader(stream);
        List<(int Line, int Number, DateTime? Visited, bool HasTime)> visits = [];
        List<UserDataImportError> errors = [];
        HashSet<int> numbers = [];
        var lineNumber = 0;
        while (await reader.ReadLineAsync(cancellationToken) is { } line)
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
        await using var unitOfWork = await _unitOfWorkFactory.BeginAsync(cancellationToken);
        var providerFilter = await _providerManager.ResolveFilterAsync(userId: user.Id, cancellationToken: cancellationToken);
        var stampingPointsMap = (await _points.GetStampingPointsAsync(
                providerFilter: providerFilter,
                seriesSlug: StampingSeries.DefaultSlug,
                stampingPointNumbers: visits.Select(p => p.Number).ToArray(),
                cancellationToken: cancellationToken))
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
        var imported = await _visits.SaveUserDataAsync(importedVisits, cancellationToken);
        await unitOfWork.CommitAsync(cancellationToken);
        return new(imported, visits.Count - imported, 0, []);
    }

    /// <summary>Persists a validated provider snapshot, its provenance and the import record.</summary>
    private async Task<IReadOnlyList<StampingPoint>> SaveSourceImportAsync(
        int providerId,
        StampingPointSourceSnapshot snapshot,
        int hikingToursCount = 0,
        CancellationToken cancellationToken = default)
    {
        ValidateSourceImport(providerId, snapshot);

        var savedPoints = await _points.SaveStampingPointsAsync(snapshot.Points.ToArray(), cancellationToken);
        var provider = await _providers.GetProviderForUpdateAsync(providerId, cancellationToken);
        provider.DataSourceUri = snapshot.SourceUri;
        provider.DataSourceAttribution = snapshot.Attribution;
        provider.DataLicenseName = snapshot.LicenseName;
        provider.DataLicenseUri = snapshot.LicenseUri;
        provider.DataSourceRevision = snapshot.Revision;
        provider.DataSourceUpdatedAt = snapshot.SourceUpdatedAt;
        provider.DataImportedAt = DateTime.UtcNow;
        provider.IsDataReady = true;
        _providers.AddImportRecord(snapshot.Points.Count, hikingToursCount);
        await _providers.SaveChangesAsync(cancellationToken);
        return savedPoints;
    }

    private static void ValidateSourceImport(int providerId, StampingPointSourceSnapshot snapshot)
    {
        if (snapshot.Points.Count == 0 || snapshot.Points.Any(point => point.ProviderId != providerId))
        {
            throw new InvalidOperationException("A provider source import must contain points for exactly that provider.");
        }
        var duplicateNumber = snapshot.Points
            .Where(point => point.Number.HasValue)
            .GroupBy(point => (point.SeriesId, point.Number))
            .FirstOrDefault(group => group.Count() > 1)?.Key;
        if (duplicateNumber is { } number)
        {
            throw new InvalidDataException($"The source import contains number {number.Number} of series {number.SeriesId} more than once.");
        }

        var duplicateExternalId = snapshot.Points
            .GroupBy(point => point.ExternalId)
            .FirstOrDefault(group => group.Count() > 1)?.Key;
        if (duplicateExternalId is not null)
        {
            throw new InvalidDataException($"The source import contains stamping point '{duplicateExternalId}' more than once.");
        }
    }
}
