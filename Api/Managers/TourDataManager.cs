using System.Globalization;
using System.Text;
using Api.Dto;
using Api.Repositories;
using TourEd.Lib.Abstractions.Exceptions;
using TourEd.Lib.Abstractions.Models;

namespace Api.Managers;

public class TourDataManager
{
    private readonly StampingPointRepository _points;
    private readonly UserVisitRepository _visits;
    private readonly StampingProviderRepository _providers;
    private readonly StampingProviderManager _providerManager;

    public TourDataManager(
        StampingPointRepository points,
        UserVisitRepository visits,
        StampingProviderRepository providers,
        StampingProviderManager providerManager)
    {
        _points = points;
        _visits = visits;
        _providers = providers;
        _providerManager = providerManager;
    }

    public async Task<List<(StampingPoint Point, List<HikingTour>? Tours, UserVisit? Visit)>> GetStampingPointsAsync(string? providerSlug = null, int? currentUserId = null, (Position, decimal)? geoFilter = null, (int UserId, bool ExcludeVisited)? userFilter = null)
    {
        var providerFilter = await _providerManager.ResolveFilterAsync(providerSlug, currentUserId);
        return await _points.GetStampingPointsAsync(geoFilter: geoFilter, providerFilter: providerFilter, userId: currentUserId, excludeVisited: userFilter?.ExcludeVisited);
    }

    public Task<List<(HikingTour Tour, List<StampingPoint> Points)>> GetHikingToursAsync(
        int currentUserId,
        (Position Centre, decimal Range)? distance = null)
    {
        return _points.GetHikingToursAsync(distance, currentUserId);
    }

    public async Task<(StampingPoint StampingPoint, UserVisit? UserVisit)> GetVisitAsync(User currentUser, int stampingPointNumber, string? providerSlug = null, string? seriesSlug = null)
    {
        var providerFilter = await _providerManager.ResolveFilterAsync(providerSlug, currentUser.Id);
        var stampingPoint = await _points.GetStampingPointAsync(stampingPointNumber, providerFilter, seriesSlug);
        var userVisit = await _visits.GetUserVisitOrDefaultAsync(currentUser, stampingPoint.Id);
        return (stampingPoint, userVisit);
    }

    public async Task<(StampingPoint StampingPoint, UserVisit? UserVisit)> GetVisitByIdAsync(User currentUser, int stampingPointId, string? providerSlug = null)
    {
        var providerFilter = await _providerManager.ResolveFilterAsync(providerSlug, currentUser.Id);
        var stampingPoint = await _points.GetStampingPointByIdAsync(stampingPointId, providerFilter);
        var userVisit = await _visits.GetUserVisitOrDefaultAsync(currentUser, stampingPoint.Id);
        return (stampingPoint, userVisit);
    }

    public async Task AddVisitAsync(User currentUser, int stampingPointNumber, DateOnly? visitedOn, TimeOnly? visitedAt, string? providerSlug = null, string? seriesSlug = null)
    {
        var providerFilter = await _providerManager.ResolveFilterAsync(providerSlug, currentUser.Id);
        var stampingPoint = await _points.GetStampingPointAsync(stampingPointNumber, providerFilter, seriesSlug);
        await _visits.AddUserVisitAsync(currentUser, stampingPoint.Id, CreateVisited(visitedOn, visitedAt), visitedAt.HasValue);
    }

    public async Task AddVisitByIdAsync(User currentUser, int stampingPointId, DateOnly? visitedOn, TimeOnly? visitedAt, string? providerSlug = null)
    {
        var providerFilter = await _providerManager.ResolveFilterAsync(providerSlug, currentUser.Id);
        var stampingPoint = await _points.GetStampingPointByIdAsync(stampingPointId, providerFilter);
        await _visits.AddUserVisitAsync(currentUser, stampingPoint.Id, CreateVisited(visitedOn, visitedAt), visitedAt.HasValue);
    }

    public async Task UpdateVisitAsync(User currentUser, int stampingPointNumber, DateOnly? visitedOn, TimeOnly? visitedAt, string? providerSlug = null, string? seriesSlug = null)
    {
        var providerFilter = await _providerManager.ResolveFilterAsync(providerSlug, currentUser.Id);
        var stampingPoint = await _points.GetStampingPointAsync(stampingPointNumber, providerFilter, seriesSlug);
        await _visits.UpdateUserVisitAsync(currentUser, stampingPoint.Id, CreateVisited(visitedOn, visitedAt), visitedAt.HasValue);
    }

    public async Task UpdateVisitByIdAsync(User currentUser, int stampingPointId, DateOnly? visitedOn, TimeOnly? visitedAt, string? providerSlug = null)
    {
        var providerFilter = await _providerManager.ResolveFilterAsync(providerSlug, currentUser.Id);
        var stampingPoint = await _points.GetStampingPointByIdAsync(stampingPointId, providerFilter);
        await _visits.UpdateUserVisitAsync(currentUser, stampingPoint.Id, CreateVisited(visitedOn, visitedAt), visitedAt.HasValue);
    }

    public async Task DeleteVisitAsync(User currentUser, int stampingPointNumber, string? providerSlug = null, string? seriesSlug = null)
    {
        var providerFilter = await _providerManager.ResolveFilterAsync(providerSlug, currentUser.Id);
        var stampingPoint = await _points.GetStampingPointAsync(stampingPointNumber, providerFilter, seriesSlug);
        await _visits.DeleteUserVisitAsync(currentUser, stampingPoint.Id);
    }

    public async Task DeleteVisitByIdAsync(User currentUser, int stampingPointId, string? providerSlug = null)
    {
        var providerFilter = await _providerManager.ResolveFilterAsync(providerSlug, currentUser.Id);
        var stampingPoint = await _points.GetStampingPointByIdAsync(stampingPointId, providerFilter);
        await _visits.DeleteUserVisitAsync(currentUser, stampingPoint.Id);
    }

    public async Task<SynchronizeVisitResult> SynchronizeVisitByIdAsync(
        User currentUser,
        int stampingPointId,
        VisitStateValue expected,
        VisitStateValue desired,
        string? providerSlug = null)
    {
        var providerFilter = await _providerManager.ResolveFilterAsync(providerSlug, currentUser.Id);
        var stampingPoint = await _points.GetStampingPointByIdAsync(stampingPointId, providerFilter);
        var (visit, isConflict) = await _visits.SynchronizeUserVisitAsync(
            currentUser,
            stampingPoint.Id,
            expected,
            desired);
        return new SynchronizeVisitResult(stampingPoint, visit, isConflict);
    }

    public async Task<AdminSavePointsResponseDto> SaveAdminStampingPointsAsync(
        IReadOnlyList<AdminStampingPointRequestDto> requests,
        CancellationToken cancellationToken = default)
    {
        if (requests == null || requests.Count == 0)
        {
            throw new RequestValidationException("At least one stamping point must be provided.");
        }

        var providers = await _providers.GetProvidersAsync(cancellationToken);
        var providersBySlug = providers.ToDictionary(p => p.Slug.ToLowerInvariant());

        var allSeries = await _providers.GetSeriesAsync(cancellationToken);
        var seriesByProviderAndSlug = allSeries.ToDictionary(s => (s.ProviderId, s.Slug.ToLowerInvariant()));

        var pointsToSave = new List<StampingPoint>(requests.Count);

        foreach (var request in requests)
        {
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                throw new RequestValidationException("Stamping point name is required.");
            }

            if (request.Latitude is < -90m or > 90m)
            {
                throw new RequestValidationException($"Invalid latitude '{request.Latitude}'. Must be between -90 and 90.");
            }

            if (request.Longitude is < -180m or > 180m)
            {
                throw new RequestValidationException($"Invalid longitude '{request.Longitude}'. Must be between -180 and 180.");
            }

            if (request.Number.HasValue && request.Number.Value < 1)
            {
                throw new RequestValidationException($"Invalid stamping point number '{request.Number}'. Must be positive.");
            }

            if (request.ValidFrom.HasValue && request.ValidUntil.HasValue && request.ValidFrom.Value > request.ValidUntil.Value)
            {
                throw new RequestValidationException($"ValidFrom '{request.ValidFrom}' cannot be after ValidUntil '{request.ValidUntil}'.");
            }

            var providerSlug = string.IsNullOrWhiteSpace(request.Provider)
                ? StampingProvider.TouringenSlug
                : request.Provider.Trim().ToLowerInvariant();

            if (!providersBySlug.TryGetValue(providerSlug, out var provider))
            {
                throw new RequestValidationException($"Unknown stamping provider '{request.Provider}'.");
            }

            var seriesSlug = string.IsNullOrWhiteSpace(request.Series)
                ? StampingSeries.TouringenStandardSlug
                : request.Series.Trim().ToLowerInvariant();

            if (!seriesByProviderAndSlug.TryGetValue((provider.Id, seriesSlug), out var series))
            {
                throw new RequestValidationException($"Unknown stamping series '{request.Series}' for provider '{provider.Slug}'.");
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

        var savedPoints = await _points.SaveStampingPointsAsync(pointsToSave.ToArray());

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
