using System.Globalization;
using System.Text;
using Api.Dto;
using Api.Repositories;
using TourEd.Lib.Abstractions.Models;

namespace Api.Managers;

public class TourDataManager
{
    private readonly TouredRepository _repository;

    public TourDataManager(TouredRepository repository)
    {
        _repository = repository;
    }

    public async Task<List<(StampingPoint Point, List<HikingTour>? Tours, UserVisit? Visit)>> GetStampingPointsAsync(string? providerSlug = null, int? currentUserId = null, (Position, decimal)? geoFilter = null, (int UserId, bool ExcludeVisited)? userFilter = null)
    {
        var providerFilter = await _repository.GetStampingProviderFilterAsync(providerSlug, currentUserId);
        return await _repository.GetStampingPointsAsync(geoFilter: geoFilter, providerFilter: providerFilter, userId: userFilter?.UserId, excludeVisited: userFilter?.ExcludeVisited);
    }

    public async Task<(StampingPoint Point, List<HikingTour>? Tours)?> GetStampingPointOrDefaultAsync(int stampingPointId)
    {
        var points = await _repository.GetStampingPointsAsync(stampingPointsNr: stampingPointId);
        if (points.Count == 0)
        {
            return null;
        }

        var (point, tours, _) = points.First();
        return (point, tours);
    }

    public Task<List<(HikingTour Tour, List<StampingPoint> Points)>> GetHikingToursAsync(
        int currentUserId,
        (Position Centre, decimal Range)? distance = null)
    {
        return _repository.GetHikingToursAsync(distance, currentUserId);
    }

    public async Task<(StampingPoint StampingPoint, UserVisit? UserVisit)> GetVisitAsync(User currentUser, int stampingPointNumber, string? providerSlug = null, string? seriesSlug = null)
    {
        var providerFilter = await _repository.GetStampingProviderFilterAsync(providerSlug, currentUser.Id);
        var stampingPoint = await _repository.GetStampingPointAsync(stampingPointNumber, providerFilter, seriesSlug);
        var userVisit = await _repository.GetUserVisitOrDefaultAsync(currentUser, stampingPoint.Id);
        return (stampingPoint, userVisit);
    }

    public async Task<(StampingPoint StampingPoint, UserVisit? UserVisit)> GetVisitByIdAsync(User currentUser, int stampingPointId, string? providerSlug = null)
    {
        var providerFilter = await _repository.GetStampingProviderFilterAsync(providerSlug, currentUser.Id);
        var stampingPoint = await _repository.GetStampingPointByIdAsync(stampingPointId, providerFilter);
        var userVisit = await _repository.GetUserVisitOrDefaultAsync(currentUser, stampingPoint.Id);
        return (stampingPoint, userVisit);
    }

    public async Task AddVisitAsync(User currentUser, int stampingPointNumber, DateOnly? visitedOn, TimeOnly? visitedAt, string? providerSlug = null, string? seriesSlug = null)
    {
        var providerFilter = await _repository.GetStampingProviderFilterAsync(providerSlug, currentUser.Id);
        var stampingPoint = await _repository.GetStampingPointAsync(stampingPointNumber, providerFilter, seriesSlug);
        await _repository.AddUserVisitAsync(currentUser, stampingPoint.Id, CreateVisited(visitedOn, visitedAt), visitedAt.HasValue);
    }

    public async Task AddVisitByIdAsync(User currentUser, int stampingPointId, DateOnly? visitedOn, TimeOnly? visitedAt, string? providerSlug = null)
    {
        var providerFilter = await _repository.GetStampingProviderFilterAsync(providerSlug, currentUser.Id);
        var stampingPoint = await _repository.GetStampingPointByIdAsync(stampingPointId, providerFilter);
        await _repository.AddUserVisitAsync(currentUser, stampingPoint.Id, CreateVisited(visitedOn, visitedAt), visitedAt.HasValue);
    }

    public async Task UpdateVisitAsync(User currentUser, int stampingPointNumber, DateOnly? visitedOn, TimeOnly? visitedAt, string? providerSlug = null, string? seriesSlug = null)
    {
        var providerFilter = await _repository.GetStampingProviderFilterAsync(providerSlug, currentUser.Id);
        var stampingPoint = await _repository.GetStampingPointAsync(stampingPointNumber, providerFilter, seriesSlug);
        await _repository.UpdateUserVisitAsync(currentUser, stampingPoint.Id, CreateVisited(visitedOn, visitedAt), visitedAt.HasValue);
    }

    public async Task UpdateVisitByIdAsync(User currentUser, int stampingPointId, DateOnly? visitedOn, TimeOnly? visitedAt, string? providerSlug = null)
    {
        var providerFilter = await _repository.GetStampingProviderFilterAsync(providerSlug, currentUser.Id);
        var stampingPoint = await _repository.GetStampingPointByIdAsync(stampingPointId, providerFilter);
        await _repository.UpdateUserVisitAsync(currentUser, stampingPoint.Id, CreateVisited(visitedOn, visitedAt), visitedAt.HasValue);
    }

    public async Task DeleteVisitAsync(User currentUser, int stampingPointNumber, string? providerSlug = null, string? seriesSlug = null)
    {
        var providerFilter = await _repository.GetStampingProviderFilterAsync(providerSlug, currentUser.Id);
        var stampingPoint = await _repository.GetStampingPointAsync(stampingPointNumber, providerFilter, seriesSlug);
        await _repository.DeleteUserVisitAsync(currentUser, stampingPoint.Id);
    }

    public async Task DeleteVisitByIdAsync(User currentUser, int stampingPointId, string? providerSlug = null)
    {
        var providerFilter = await _repository.GetStampingProviderFilterAsync(providerSlug, currentUser.Id);
        var stampingPoint = await _repository.GetStampingPointByIdAsync(stampingPointId, providerFilter);
        await _repository.DeleteUserVisitAsync(currentUser, stampingPoint.Id);
    }

    public async Task<AdminSavePointsResponseDto> SaveAdminStampingPointsAsync(
        IReadOnlyList<AdminStampingPointRequestDto> requests,
        CancellationToken cancellationToken = default)
    {
        if (requests == null || requests.Count == 0)
        {
            throw new InvalidDataException("At least one stamping point must be provided.");
        }

        var providers = await _repository.GetStampingProvidersAsync(includeRestrictedProviders: true);
        var providersBySlug = providers.ToDictionary(p => p.Slug.ToLowerInvariant());

        var allSeries = await _repository.GetAllStampingSeriesAsync(cancellationToken);
        var seriesByProviderAndSlug = allSeries.ToDictionary(s => (s.ProviderId, s.Slug.ToLowerInvariant()));

        var pointsToSave = new List<StampingPoint>(requests.Count);

        foreach (var request in requests)
        {
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                throw new InvalidDataException("Stamping point name is required.");
            }

            if (request.Latitude is < -90m or > 90m)
            {
                throw new InvalidDataException($"Invalid latitude '{request.Latitude}'. Must be between -90 and 90.");
            }

            if (request.Longitude is < -180m or > 180m)
            {
                throw new InvalidDataException($"Invalid longitude '{request.Longitude}'. Must be between -180 and 180.");
            }

            if (request.Number.HasValue && request.Number.Value < 1)
            {
                throw new InvalidDataException($"Invalid stamping point number '{request.Number}'. Must be positive.");
            }

            if (request.ValidFrom.HasValue && request.ValidUntil.HasValue && request.ValidFrom.Value > request.ValidUntil.Value)
            {
                throw new InvalidDataException($"ValidFrom '{request.ValidFrom}' cannot be after ValidUntil '{request.ValidUntil}'.");
            }

            var providerSlug = string.IsNullOrWhiteSpace(request.Provider)
                ? StampingProvider.TouringenSlug
                : request.Provider.Trim().ToLowerInvariant();

            if (!providersBySlug.TryGetValue(providerSlug, out var provider))
            {
                throw new InvalidDataException($"Unknown stamping provider '{request.Provider}'.");
            }

            var seriesSlug = string.IsNullOrWhiteSpace(request.Series)
                ? StampingSeries.TouringenStandardSlug
                : request.Series.Trim().ToLowerInvariant();

            if (!seriesByProviderAndSlug.TryGetValue((provider.Id, seriesSlug), out var series))
            {
                throw new InvalidDataException($"Unknown stamping series '{request.Series}' for provider '{provider.Slug}'.");
            }

            string externalId;
            if (!string.IsNullOrWhiteSpace(request.ExternalId))
            {
                externalId = request.ExternalId.Trim();
            }
            else if (request.Number.HasValue)
            {
                externalId = $"{series.Slug}-{request.Number.Value.ToString(CultureInfo.InvariantCulture)}";
            }
            else
            {
                externalId = $"{series.Slug}-{Slugify(request.Name)}";
            }

            var point = new StampingPoint(
                0,
                request.Name.Trim(),
                request.Longitude,
                request.Latitude,
                request.Number,
                0,
                provider.Id,
                externalId)
            {
                SeriesId = series.Id,
                ValidFrom = request.ValidFrom,
                ValidUntil = request.ValidUntil
            };

            pointsToSave.Add(point);
        }

        var savedPoints = await _repository.SaveStampingPointsAsync(pointsToSave.ToArray());

        var providersById = providers.ToDictionary(p => p.Id);
        var seriesById = allSeries.ToDictionary(s => s.Id);

        var resultPoints = savedPoints.Select(p => new AdminStampingPointResponseDto(
            p.Id,
            providersById[p.ProviderId].Slug,
            seriesById[p.SeriesId].Slug,
            p.Number,
            p.Name,
            p.Latitude,
            p.Longitude,
            p.ExternalId,
            p.ValidFrom,
            p.ValidUntil
        )).ToList();

        return new AdminSavePointsResponseDto(resultPoints.Count, resultPoints);
    }

    private static string Slugify(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "point";
        var normalized = text.Trim().ToLowerInvariant()
            .Replace("ä", "ae")
            .Replace("ö", "oe")
            .Replace("ü", "ue")
            .Replace("ß", "ss");
        var sb = new StringBuilder();
        var previousDash = false;
        foreach (var c in normalized)
        {
            if (char.IsAsciiLetterOrDigit(c))
            {
                sb.Append(c);
                previousDash = false;
            }
            else if (!previousDash)
            {
                sb.Append('-');
                previousDash = true;
            }
        }
        var result = sb.ToString().Trim('-');
        return string.IsNullOrEmpty(result) ? "point" : result;
    }

    private static DateTime? CreateVisited(DateOnly? visitedOn, TimeOnly? visitedAt)
        => visitedOn?.ToDateTime(visitedAt ?? TimeOnly.MinValue);
}
