using System.Drawing;
using Api.Dto;
using Api.Managers;
using Microsoft.EntityFrameworkCore;
using TourEd.Lib.Abstractions.Exceptions;
using TourEd.Lib.Abstractions.Interfaces.Services;
using TourEd.Lib.Abstractions.Models;

namespace Api.Repositories;

public class TouredRepository : IUserService
{
    private readonly DataContext _dbContext;

    public TouredRepository(DataContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<StampingProviderFilter> GetStampingProviderFilterAsync(string? providerSlug = null, int? userId = null)
    {
        if (string.Equals(providerSlug, "all", StringComparison.OrdinalIgnoreCase))
        {
            return userId is null ? StampingProviderFilter.Anonymous : StampingProviderFilter.ForUser(userId.Value);
        }

        if (!string.IsNullOrWhiteSpace(providerSlug))
        {
            var provider = await _dbContext.StampingProviders.AsNoTracking().FirstOrDefaultAsync(p => p.Slug.ToLower() == providerSlug.Trim().ToLowerInvariant());
            if (provider == null)
            {
                throw EntityNotFoundException.Create<StampingProvider>(providerSlug);
            }
            if (userId is null && !provider.IsAnonymousAccessAllowed)
            {
                throw new UnauthorizedAccessException("This stamping provider requires authentication.");
            }
            if (userId is not null && !await HasStampingProviderAccessAsync(userId.Value, provider.Id))
            {
                throw new UnauthorizedAccessException("This stamping provider is not enabled for the user.");
            }
            return userId is null
                ? StampingProviderFilter.Single(provider.Id)
                : StampingProviderFilter.SingleForUser(provider.Id, userId.Value);
        }

        if (userId != null)
        {
            var defaultProviderId = await _dbContext.Users.AsNoTracking()
                .Where(user => user.Id == userId.Value &&
                               user.DefaultStampingProviderId != null &&
                               user.StampingProviders.Any(access =>
                                   access.StampingProviderId == user.DefaultStampingProviderId))
                .Select(user => user.DefaultStampingProviderId)
                .SingleOrDefaultAsync();
            if (defaultProviderId is not null)
            {
                return StampingProviderFilter.SingleForUser(defaultProviderId.Value, userId.Value);
            }

            throw new UnauthorizedAccessException("The user has no enabled default stamping provider.");
        }

        var anonymousDefaultProvider = await _dbContext.StampingProviders.AsNoTracking()
            .SingleAsync(provider => provider.Id == StampingProvider.TouringenId);
        if (!anonymousDefaultProvider.IsAnonymousAccessAllowed)
        {
            throw new UnauthorizedAccessException("The default stamping provider requires authentication.");
        }
        return StampingProviderFilter.Single(anonymousDefaultProvider.Id);
    }

    public Task<List<StampingProvider>> GetStampingProvidersAsync(bool includeRestrictedProviders = true)
        => _dbContext.StampingProviders.AsNoTracking()
            .Where(provider => includeRestrictedProviders || provider.IsAnonymousAccessAllowed)
            .OrderBy(provider => provider.Name)
            .ThenBy(provider => provider.Slug)
            .ToListAsync();

    public Task<List<AdminProviderDto>> GetAdminProvidersAsync(CancellationToken cancellationToken = default)
        => _dbContext.StampingProviders.AsNoTracking()
            .OrderBy(provider => provider.Name)
            .ThenBy(provider => provider.Slug)
            .Select(provider => new AdminProviderDto(provider.Id, provider.Slug, provider.Name, provider.Abbreviation))
            .ToListAsync(cancellationToken);

    public Task<List<AdminUserDto>> GetAdminUsersAsync(CancellationToken cancellationToken = default)
        => _dbContext.Users.AsNoTracking()
            .OrderBy(user => user.Email)
            .ThenBy(user => user.Id)
            .Select(user => new AdminUserDto(
                user.Id,
                user.Email,
                user.GoogleSubject != null,
                user.DefaultStampingProvider == null ? null : user.DefaultStampingProvider.Slug,
                user.StampingProviders
                    .OrderBy(access => access.StampingProvider.Name)
                    .ThenBy(access => access.StampingProvider.Slug)
                    .Select(access => access.StampingProvider.Slug)
                    .ToList(),
                user.VisitedStampingPoints.Count))
            .ToListAsync(cancellationToken);

    public async Task<AdminUserDto?> UpdateAdminUserProvidersAsync(
        int userId,
        UpdateAdminUserProvidersRequestDto request,
        int actorUserId,
        CancellationToken cancellationToken = default)
    {
        var requestedSlugs = request.Providers
            .Select(slug => slug?.Trim().ToLowerInvariant())
            .Where(slug => !string.IsNullOrWhiteSpace(slug))
            .Cast<string>()
            .ToHashSet(StringComparer.Ordinal);
        if (requestedSlugs.Count != request.Providers.Count)
        {
            throw new InvalidDataException("Provider slugs must be non-empty and unique.");
        }

        var normalizedDefault = string.IsNullOrWhiteSpace(request.DefaultProvider)
            ? null
            : request.DefaultProvider.Trim().ToLowerInvariant();
        if (normalizedDefault is not null && !requestedSlugs.Contains(normalizedDefault))
        {
            throw new InvalidDataException("The default provider must be included in Providers.");
        }

        var providers = await _dbContext.StampingProviders
            .Where(provider => requestedSlugs.Contains(provider.Slug.ToLower()))
            .ToListAsync(cancellationToken);
        var foundSlugs = providers.Select(provider => provider.Slug.ToLowerInvariant()).ToHashSet(StringComparer.Ordinal);
        var unknownSlugs = requestedSlugs.Except(foundSlugs).Order().ToArray();
        if (unknownSlugs.Length > 0)
        {
            throw new InvalidDataException($"Unknown provider(s): {string.Join(", ", unknownSlugs)}.");
        }

        var user = await _dbContext.Users
            .Include(item => item.StampingProviders)
            .ThenInclude(access => access.StampingProvider)
            .SingleOrDefaultAsync(item => item.Id == userId, cancellationToken);
        if (user is null)
        {
            return null;
        }

        var existingBySlug = user.StampingProviders.ToDictionary(
            access => access.StampingProvider.Slug.ToLowerInvariant(),
            StringComparer.Ordinal);
        var removed = existingBySlug.Keys.Except(requestedSlugs).ToArray();
        var added = requestedSlugs.Except(existingBySlug.Keys).ToArray();
        var previousDefault = user.DefaultStampingProviderId;

        _dbContext.UserStampingProviders.RemoveRange(removed.Select(slug => existingBySlug[slug]));
        foreach (var slug in added)
        {
            user.StampingProviders.Add(new UserStampingProvider
            {
                UserId = user.Id,
                StampingProvider = providers.Single(provider =>
                    string.Equals(provider.Slug, slug, StringComparison.OrdinalIgnoreCase))
            });
        }

        user.DefaultStampingProviderId = normalizedDefault is null
            ? null
            : providers.Single(provider =>
                string.Equals(provider.Slug, normalizedDefault, StringComparison.OrdinalIgnoreCase)).Id;

        foreach (var slug in removed)
        {
            _dbContext.AdminAuditEntries.Add(CreateAudit(actorUserId, "provider.revoked", userId, slug));
        }
        foreach (var slug in added)
        {
            _dbContext.AdminAuditEntries.Add(CreateAudit(actorUserId, "provider.granted", userId, slug));
        }
        if (previousDefault != user.DefaultStampingProviderId)
        {
            _dbContext.AdminAuditEntries.Add(CreateAudit(actorUserId, "default-provider.changed", userId, normalizedDefault));
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return await _dbContext.Users.AsNoTracking()
            .Where(item => item.Id == userId)
            .Select(item => new AdminUserDto(
                item.Id,
                item.Email,
                item.GoogleSubject != null,
                item.DefaultStampingProvider == null ? null : item.DefaultStampingProvider.Slug,
                item.StampingProviders
                    .OrderBy(access => access.StampingProvider.Name)
                    .ThenBy(access => access.StampingProvider.Slug)
                    .Select(access => access.StampingProvider.Slug)
                    .ToList(),
                item.VisitedStampingPoints.Count))
            .SingleAsync(cancellationToken);
    }

    public async Task<bool> DeleteAdminUserAsync(
        int userId,
        int actorUserId,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        var user = await _dbContext.Users.SingleOrDefaultAsync(item => item.Id == userId, cancellationToken);
        if (user is null)
        {
            return false;
        }

        var normalizedEmail = user.Email.Trim().ToLowerInvariant();
        var registrationRequests = await _dbContext.RegistrationRequests
            .Where(request =>
                (user.GoogleSubject != null && request.GoogleSubject == user.GoogleSubject) ||
                request.Email.ToLower() == normalizedEmail)
            .ToListAsync(cancellationToken);

        _dbContext.RegistrationRequests.RemoveRange(registrationRequests);
        _dbContext.Users.Remove(user);
        _dbContext.AdminAuditEntries.Add(CreateAudit(actorUserId, "user.deleted", userId, null));
        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    private static AdminAuditEntry CreateAudit(
        int actorUserId,
        string action,
        int? targetUserId,
        string? providerSlug,
        int? registrationRequestId = null)
        => new()
        {
            CreatedAt = DateTime.UtcNow,
            ActorUserId = actorUserId,
            Action = action,
            TargetUserId = targetUserId,
            RegistrationRequestId = registrationRequestId,
            ProviderSlug = providerSlug
        };

    public Task<List<AdminAuditEntryDto>> GetAdminAuditEntriesAsync(
        int offset,
        int limit,
        CancellationToken cancellationToken = default)
        => _dbContext.AdminAuditEntries
            .AsNoTracking()
            .OrderByDescending(entry => entry.CreatedAt)
            .ThenByDescending(entry => entry.Id)
            .Skip(offset)
            .Take(limit)
            .Select(entry => new AdminAuditEntryDto(
                entry.Id,
                entry.CreatedAt,
                entry.ActorUserId,
                entry.Action,
                entry.TargetUserId,
                entry.RegistrationRequestId,
                entry.ProviderSlug))
            .ToListAsync(cancellationToken);

    public Task<List<StampingProvider>> GetStampingProvidersForUserAsync(
        int userId,
        CancellationToken cancellationToken = default)
        => _dbContext.UserStampingProviders.AsNoTracking()
            .Where(access => access.UserId == userId)
            .Select(access => access.StampingProvider)
            .OrderBy(provider => provider.Name)
            .ThenBy(provider => provider.Slug)
            .ToListAsync(cancellationToken);

    public Task<bool> HasStampingProviderAccessAsync(
        int userId,
        int providerId,
        CancellationToken cancellationToken = default)
        => _dbContext.UserStampingProviders.AsNoTracking().AnyAsync(
            access => access.UserId == userId && access.StampingProviderId == providerId,
            cancellationToken);

    public async Task<List<(StampingPoint Point, List<HikingTour>? Tours, UserVisit? visit)>> GetStampingPointsAsync(string? nameFilter = null, (Position Centre, decimal Radius)? geoFilter = null, StampingProviderFilter? providerFilter = null, string? seriesSlug = null, int? userId = null, bool? excludeVisited = null, params int[] stampingPointsNr)
    {
        IQueryable<StampingPoint> query = _dbContext.StampingPoints.AsNoTracking().Include(point => point.Series);
        if (providerFilter?.UserId is { } permittedUserId)
        {
            query = query.Where(point => _dbContext.UserStampingProviders.Any(access =>
                access.UserId == permittedUserId && access.StampingProviderId == point.ProviderId));
        }
        else if (providerFilter is { IsAnonymousOnly: true })
        {
            query = query.Where(point => point.Provider.IsAnonymousAccessAllowed);
        }
        if (providerFilter is { IncludesAllProviders: false, ProviderId: { } providerId })
        {
            query = query.Where(p => p.ProviderId == providerId);
        }
        if (!string.IsNullOrWhiteSpace(seriesSlug))
        {
            var normalizedSeriesSlug = seriesSlug.Trim().ToLowerInvariant();
            query = query.Where(point => point.Series.Slug.ToLower() == normalizedSeriesSlug);
        }

        if (!string.IsNullOrWhiteSpace(nameFilter))
        {
            query = query.Where(p => p.Name.ToLower().Contains(nameFilter.Trim().ToLowerInvariant()));
        }

        if (stampingPointsNr.Length > 0)
        {
            query = query.Where(p => p.Number.HasValue && stampingPointsNr.Contains(p.Number.Value));
        }

        var result = from point in query
            join rawTourPoint in _dbContext.StampingPointsInTours.Include(p => p.Tour).ThenInclude(p => p.StampingPoints).ThenInclude(p => p.StampingPoint) on point.Id equals rawTourPoint.StampingPointId into joinedTourPoints
            from tourPoint in joinedTourPoints.DefaultIfEmpty()
            group tourPoint by point into groupedTours
            select new { Point = groupedTours.Key, UserVisit = userId == null ? null : _dbContext.UserVisits.FirstOrDefault(p => p.StampingPointId == groupedTours.Key.Id && p.UserId == userId), Tours = groupedTours.Select(p => p.Tour).ToList() };

        if (excludeVisited != null && userId != null)
        {
            result = excludeVisited.Value 
                ? result.Where(p => _dbContext.UserVisits.Where(q => q.UserId == userId.Value).All(q => q.StampingPointId != p.Point.Id)) 
                : result.Where(p => _dbContext.UserVisits.Where(q => q.UserId == userId.Value).Any(q => q.StampingPointId == p.Point.Id));
        }
        
        var dto = await result.ToListAsync();
        if (geoFilter != null)
        {
            dto = dto.Where(p => Position.GetDistance(p.Point.Position, geoFilter.Value.Centre) < geoFilter.Value.Radius).ToList();
        }
        var providers = await GetStampingProvidersAsync(dto.Select(p => p.Point.ProviderId));
        var series = await GetStampingSeriesAsync(dto.Select(p => p.Point.SeriesId));
        return dto.Select(p =>
            (p.Point with { Provider = providers[p.Point.ProviderId], Series = series[p.Point.SeriesId] },
                p.Tours.Any(q => q != null) ? p.Tours : null,
                (UserVisit?) p.UserVisit)).ToList();
    }

    public async Task<List<(HikingTour Tour, List<StampingPoint> Points)>> GetHikingToursAsync(
        (Position Centre, decimal Range)? circularRange = null,
        int? userId = null,
        params StampingPoint[] stampingPoints)
    {
        var query = _dbContext.HikingTours.AsNoTracking();
        if (stampingPoints.Any())
        {
            var stampingPointIds = stampingPoints.Select(p => p.Id).Distinct().ToArray();
            query = query.Where(p => p.StampingPoints.Any(stampingPoint => stampingPointIds.Contains(stampingPoint.StampingPointId)));
        }

        var result = from tour in query
            join tourPoint in _dbContext.StampingPointsInTours.AsNoTracking() on tour.Id equals tourPoint.Tour.Id
            join point in _dbContext.StampingPoints.AsNoTracking()
                .Where(point => userId == null || _dbContext.UserStampingProviders.Any(access =>
                    access.UserId == userId.Value && access.StampingProviderId == point.ProviderId))
                on tourPoint.StampingPointId equals point.Id
            group point by tour into groupedStampingPoints
            select new { Tour = groupedStampingPoints.Key, Points = groupedStampingPoints.ToList() };

        var dto = await result.ToListAsync();
        if (circularRange != null)
        {
            dto = dto.Where(p => p.Points.Any(point => Position.GetDistance(point.Position, circularRange.Value.Centre) < circularRange.Value.Range)).ToList();
        }
        var providers = await GetStampingProvidersAsync(dto.SelectMany(p => p.Points).Select(p => p.ProviderId));
        var series = await GetStampingSeriesAsync(dto.SelectMany(p => p.Points).Select(p => p.SeriesId));
        return dto.Select(p =>
            (p.Tour, p.Points.Select(point => point with { Provider = providers[point.ProviderId], Series = series[point.SeriesId] }).ToList())).ToList();
    }

    private async Task<Dictionary<int, StampingProvider>> GetStampingProvidersAsync(IEnumerable<int> providerIds)
    {
        var ids = providerIds.Distinct().ToArray();
        return await _dbContext.StampingProviders.AsNoTracking()
            .Where(provider => ids.Contains(provider.Id))
            .ToDictionaryAsync(provider => provider.Id);
    }

    private async Task<Dictionary<int, StampingSeries>> GetStampingSeriesAsync(IEnumerable<int> seriesIds)
    {
        var ids = seriesIds.Distinct().ToArray();
        return await _dbContext.StampingSeries.AsNoTracking()
            .Where(series => ids.Contains(series.Id))
            .ToDictionaryAsync(series => series.Id);
    }

    public Task<List<StampingSeries>> GetAllStampingSeriesAsync(CancellationToken cancellationToken = default)
        => _dbContext.StampingSeries.AsNoTracking()
            .OrderBy(series => series.ProviderId)
            .ThenBy(series => series.Slug)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<StampingPoint>> SaveStampingPointsAsync(params StampingPoint[] points)
    {
        if (points.Length == 0)
        {
            return Array.Empty<StampingPoint>();
        }

        var importedNumberedPoints = points
            .Where(point => point.Number.HasValue)
            .ToDictionary(point => (point.SeriesId, Number: point.Number!.Value));
        var importedUnnumberedPoints = points
            .Where(point => !point.Number.HasValue)
            .ToDictionary(point => (point.ProviderId, point.ExternalId));
        _ = points.ToDictionary(point => (point.ProviderId, point.ExternalId));
        var seriesIds = points.Select(point => point.SeriesId).Distinct().ToArray();
        var providerIds = points.Select(point => point.ProviderId).Distinct().ToArray();
        var existingPoints = await _dbContext.StampingPoints.AsNoTracking()
            .Where(point => seriesIds.Contains(point.SeriesId) || providerIds.Contains(point.ProviderId))
            .ToArrayAsync();
        var existingNumberedPoints = existingPoints
            .Where(point => point.Number.HasValue)
            .ToDictionary(point => (point.SeriesId, Number: point.Number!.Value));
        var existingPointsByExternalId = existingPoints
            .ToDictionary(point => (point.ProviderId, point.ExternalId));
        var savedPoints = new List<StampingPoint>(points.Length);

        foreach (var (key, importedPoint) in importedNumberedPoints)
        {
            var pointToSave = (existingNumberedPoints.TryGetValue(key, out var existingPoint) ||
                               existingPointsByExternalId.TryGetValue((importedPoint.ProviderId, importedPoint.ExternalId), out existingPoint))
                ? importedPoint with { Id = existingPoint.Id }
                : importedPoint with { Id = default };

            await SavePointAsync(pointToSave);
        }

        foreach (var (key, importedPoint) in importedUnnumberedPoints)
        {
            var pointToSave = existingPointsByExternalId.TryGetValue(key, out var existingPoint)
                ? importedPoint with { Id = existingPoint.Id }
                : importedPoint with { Id = default };

            await SavePointAsync(pointToSave);
        }

        async Task SavePointAsync(StampingPoint pointToSave)
        {
            if (pointToSave.Id == default)
            {
                await _dbContext.AddAsync(pointToSave);
            }
            else
            {
                _dbContext.Update(pointToSave);
            }

            savedPoints.Add(pointToSave);
        }

        await _dbContext.SaveChangesAsync();
        savedPoints.ForEach(p => _dbContext.Entry(p).State = EntityState.Detached);
        return savedPoints;
    }

    public async Task<IReadOnlyList<StampingPoint>> SaveStampingPointSourceImportAsync(
        int providerId,
        StampingPointSourceSnapshot snapshot,
        int hikingToursCount = 0,
        CancellationToken cancellationToken = default)
    {
        if (snapshot.Points.Count == 0 || snapshot.Points.Any(point => point.ProviderId != providerId))
        {
            throw new InvalidOperationException("A provider source import must contain points for exactly that provider.");
        }

        var savedPoints = await SaveStampingPointsAsync(snapshot.Points.ToArray());
        var provider = await _dbContext.StampingProviders.SingleAsync(
            item => item.Id == providerId,
            cancellationToken);
        provider.DataSourceUri = snapshot.SourceUri;
        provider.DataSourceAttribution = snapshot.Attribution;
        provider.DataLicenseName = snapshot.LicenseName;
        provider.DataLicenseUri = snapshot.LicenseUri;
        provider.DataSourceRevision = snapshot.Revision;
        provider.DataSourceUpdatedAt = snapshot.SourceUpdatedAt;
        provider.DataImportedAt = DateTime.UtcNow;
        provider.IsAnonymousAccessAllowed = true;
        await _dbContext.AddAsync(
            new Import(default, default, snapshot.Points.Count, hikingToursCount),
            cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return savedPoints;
    }

    public async Task<(StampingProvider Provider, List<StampingPoint> Points)?> GetPublicProviderDataAsync(
        string providerSlug,
        int? userId = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedSlug = providerSlug.Trim().ToLowerInvariant();
        var provider = await _dbContext.StampingProviders.AsNoTracking().SingleOrDefaultAsync(
            item => item.Slug.ToLower() == normalizedSlug &&
                    item.IsAnonymousAccessAllowed &&
                    item.DataSourceUri != null &&
                    item.DataSourceAttribution != null &&
                    item.DataLicenseName != null &&
                    item.DataLicenseUri != null &&
                    item.DataSourceRevision != null &&
                    item.DataSourceUpdatedAt != null &&
                    item.DataImportedAt != null,
            cancellationToken);
        if (provider is null)
        {
            return null;
        }

        if (userId is not null && !await HasStampingProviderAccessAsync(userId.Value, provider.Id, cancellationToken))
        {
            throw new UnauthorizedAccessException("This stamping provider is not enabled for the user.");
        }

        var points = await _dbContext.StampingPoints.AsNoTracking()
            .Where(point => point.ProviderId == provider.Id)
            .OrderBy(point => point.Number)
            .ToListAsync(cancellationToken);
        return (provider, points);
    }

    public async Task SaveHikingToursAsync(params HikingTour[] tours)
    {
        List<HikingTour> updatedEntries = new();

        var updatedTours = tours.ToDictionary(p => p.Id);
        var allTours = await _dbContext.HikingTours.AsNoTracking().ToListAsync();

        foreach (var existingTour in allTours.Where(p => updatedTours.ContainsKey(p.Id)))
        {
            _dbContext.Update(updatedTours[existingTour.Id]);
            updatedEntries.Add(updatedTours[existingTour.Id]);
            updatedTours.Remove(existingTour.Id);
        }

        await _dbContext.AddRangeAsync(updatedTours.Values);
        updatedEntries.AddRange(updatedTours.Values);

        await _dbContext.SaveChangesAsync();
        updatedEntries.ForEach(p => _dbContext.Entry(p).State = EntityState.Detached);
    }

    public async Task SaveImportAsync(int stampingPointsCount, int hikingToursCount)
    {
        await _dbContext.AddAsync(new Import(default, default, stampingPointsCount, hikingToursCount));
        await _dbContext.SaveChangesAsync();
    }

    public async Task SaveUserDataAsync(params UserVisit[] visits)
    {
        if (!visits.Any()) return;
        if (visits.Select(p => p.UserId).Distinct().Count() > 1) throw new InvalidOperationException("Can only import one user at a time.");
        if (visits.GroupBy(p => p.StampingPointId).Any(p => p.Count() > 1)) throw new InvalidOperationException("Stamping points can only be visited once. Remove duplicate entries.");
        var updatedVisits = visits.ToDictionary(p => p.StampingPointId);
        List<UserVisit> updatedEntries = new();
        var allVisits = await _dbContext.UserVisits.AsNoTracking().Where(p => p.UserId == visits.First().UserId).ToListAsync();

        foreach (var existingEntry in allVisits.Where(p => updatedVisits.ContainsKey(p.StampingPointId)))
        {
            updatedVisits.Remove(existingEntry.StampingPointId);
        }

        await _dbContext.AddRangeAsync(updatedVisits.Values);
        updatedEntries.AddRange(updatedVisits.Values);

        await _dbContext.SaveChangesAsync();
        updatedEntries.ForEach(p => _dbContext.Entry(p).State = EntityState.Detached);
    }
    
    
    public Task<User?> GetUserOrDefaultAsync(string userEmail, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = userEmail.Trim().ToLowerInvariant();
        return _dbContext.Users.AsNoTracking()
            .FirstOrDefaultAsync(user => user.Email.ToLower() == normalizedEmail, cancellationToken);
    }

    public Task<User?> GetUserByIdOrDefaultAsync(int userId, CancellationToken cancellationToken = default)
        => _dbContext.Users.AsNoTracking().FirstOrDefaultAsync(user => user.Id == userId, cancellationToken);

    public Task<User?> GetUserByGoogleSubjectOrDefaultAsync(string googleSubject, CancellationToken cancellationToken = default)
        => _dbContext.Users.AsNoTracking().FirstOrDefaultAsync(p => p.GoogleSubject == googleSubject, cancellationToken);

    public async Task<bool> TryBindGoogleSubjectAsync(int userId, string googleSubject, CancellationToken cancellationToken = default)
    {
        var updatedUsers = await _dbContext.Users
            .Where(user => user.Id == userId &&
                           user.GoogleSubject == null &&
                           !_dbContext.Users.Any(other => other.GoogleSubject == googleSubject))
            .ExecuteUpdateAsync(
                properties => properties.SetProperty(user => user.GoogleSubject, googleSubject),
                cancellationToken);

        return updatedUsers == 1;
    }

    public async Task<RegistrationRequest?> GetRegistrationRequestByGoogleSubjectOrDefaultAsync(
        string googleSubject,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.RegistrationRequests
            .FirstOrDefaultAsync(r => r.GoogleSubject == googleSubject, cancellationToken);
    }

    public async Task<RegistrationRequest> RecordOrUpdateRegistrationRequestAsync(
        string googleSubject,
        string email,
        CancellationToken cancellationToken = default)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();

        var existing = await _dbContext.RegistrationRequests
            .FirstOrDefaultAsync(r => r.GoogleSubject == googleSubject, cancellationToken);

        if (existing is null)
        {
            var newRequest = new RegistrationRequest
            {
                GoogleSubject = googleSubject,
                Email = normalizedEmail,
                Status = RegistrationRequestStatus.Pending,
                CreatedAt = DateTime.UtcNow
            };
            _dbContext.RegistrationRequests.Add(newRequest);
            try
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
                return newRequest;
            }
            catch (DbUpdateException)
            {
                existing = await _dbContext.RegistrationRequests
                    .FirstOrDefaultAsync(r => r.GoogleSubject == googleSubject, cancellationToken);
                if (existing is null)
                {
                    throw;
                }
            }
        }

        if (existing.Status == RegistrationRequestStatus.Rejected)
        {
            return existing;
        }

        if (existing.Status == RegistrationRequestStatus.Pending)
        {
            if (!string.Equals(existing.Email, normalizedEmail, StringComparison.OrdinalIgnoreCase))
            {
                existing.Email = normalizedEmail;
                existing.UpdatedAt = DateTime.UtcNow;
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
            return existing;
        }

        existing.Status = RegistrationRequestStatus.Pending;
        existing.Email = normalizedEmail;
        existing.CreatedAt = DateTime.UtcNow;
        existing.UpdatedAt = DateTime.UtcNow;
        existing.DecidedAt = null;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return existing;
    }

    public async Task MarkRegistrationRequestApprovedAsync(
        string googleSubject,
        CancellationToken cancellationToken = default)
    {
        var request = await _dbContext.RegistrationRequests
            .FirstOrDefaultAsync(r => r.GoogleSubject == googleSubject, cancellationToken);
        if (request != null && request.Status != RegistrationRequestStatus.Approved)
        {
            request.Status = RegistrationRequestStatus.Approved;
            request.DecidedAt = DateTime.UtcNow;
            request.UpdatedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<List<AdminRegistrationRequestDto>> GetRegistrationRequestsAsync(
        string? status = null,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.RegistrationRequests.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(status))
        {
            if (Enum.TryParse<RegistrationRequestStatus>(status, ignoreCase: true, out var parsedStatus))
            {
                query = query.Where(r => r.Status == parsedStatus);
            }
            else
            {
                return [];
            }
        }

        var requests = await query
            .OrderBy(r => r.Status == RegistrationRequestStatus.Pending ? 0 : 1)
            .ThenBy(r => r.CreatedAt)
            .ToListAsync(cancellationToken);

        return requests.Select(r => new AdminRegistrationRequestDto(
            r.Id,
            r.GoogleSubject,
            r.Email,
            r.Status.ToString().ToLowerInvariant(),
            r.CreatedAt,
            r.DecidedAt)).ToList();
    }

    public async Task<AdminRegistrationRequestDto?> ApproveRegistrationRequestAsync(
        int id,
        int actorUserId,
        CancellationToken cancellationToken = default)
    {
        var request = await _dbContext.RegistrationRequests
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (request is null)
        {
            return null;
        }

        if (request.Status != RegistrationRequestStatus.Pending)
        {
            throw new RegistrationRequestAlreadyDecidedException(id);
        }

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        var existingUser = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.GoogleSubject == request.GoogleSubject, cancellationToken);

        if (existingUser is null)
        {
            var normalizedEmail = request.Email.Trim().ToLowerInvariant();
            existingUser = await _dbContext.Users
                .FirstOrDefaultAsync(u => u.Email.ToLower() == normalizedEmail, cancellationToken);

            if (existingUser is not null)
            {
                if (existingUser.GoogleSubject is null)
                {
                    existingUser.GoogleSubject = request.GoogleSubject;
                }
                else if (existingUser.GoogleSubject != request.GoogleSubject)
                {
                    throw new InvalidOperationException("User email is already bound to another Google subject.");
                }
            }
            else
            {
                existingUser = new User
                {
                    Email = request.Email.Trim().ToLowerInvariant(),
                    GoogleSubject = request.GoogleSubject,
                    DefaultStampingProviderId = null
                };
                _dbContext.Users.Add(existingUser);
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        request.Status = RegistrationRequestStatus.Approved;
        request.DecidedAt = DateTime.UtcNow;
        request.UpdatedAt = DateTime.UtcNow;

        _dbContext.AdminAuditEntries.Add(CreateAudit(
            actorUserId,
            "registration.approved",
            existingUser.Id,
            null,
            request.Id));

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new AdminRegistrationRequestDto(
            request.Id,
            request.GoogleSubject,
            request.Email,
            request.Status.ToString().ToLowerInvariant(),
            request.CreatedAt,
            request.DecidedAt);
    }

    public async Task<AdminRegistrationRequestDto?> RejectRegistrationRequestAsync(
        int id,
        int actorUserId,
        CancellationToken cancellationToken = default)
    {
        var request = await _dbContext.RegistrationRequests
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (request is null)
        {
            return null;
        }

        if (request.Status != RegistrationRequestStatus.Pending)
        {
            throw new RegistrationRequestAlreadyDecidedException(id);
        }

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        request.Status = RegistrationRequestStatus.Rejected;
        request.DecidedAt = DateTime.UtcNow;
        request.UpdatedAt = DateTime.UtcNow;

        _dbContext.AdminAuditEntries.Add(CreateAudit(
            actorUserId,
            "registration.rejected",
            null,
            null,
            request.Id));

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new AdminRegistrationRequestDto(
            request.Id,
            request.GoogleSubject,
            request.Email,
            request.Status.ToString().ToLowerInvariant(),
            request.CreatedAt,
            request.DecidedAt);
    }

    public async Task<int> CleanupExpiredRegistrationRequestsAsync(
        DateTime? utcNow = null,
        CancellationToken cancellationToken = default)
    {
        var now = utcNow ?? DateTime.UtcNow;
        var cutoff = now.Subtract(TimeSpan.FromDays(30));

        var deletedCount = await _dbContext.RegistrationRequests
            .Where(request =>
                (request.Status == RegistrationRequestStatus.Pending && request.CreatedAt < cutoff) ||
                (request.Status == RegistrationRequestStatus.Rejected && (request.DecidedAt ?? request.CreatedAt) < cutoff) ||
                (request.Status == RegistrationRequestStatus.Approved && (request.DecidedAt ?? request.CreatedAt) < cutoff))
            .ExecuteDeleteAsync(cancellationToken);

        return deletedCount;
    }

    public async Task<UserVisit?> GetUserVisitOrDefaultAsync(User currentUser, int stampingPointId) 
        => await _dbContext.UserVisits.FirstOrDefaultAsync(p => p.StampingPointId == stampingPointId && p.UserId == currentUser.Id);

    public async Task<StampingPoint> GetStampingPointAsync(int stampingPointNumber, StampingProviderFilter providerFilter, string? seriesSlug = null)
    {
        if (providerFilter.IncludesAllProviders)
        {
            throw new NotSupportedException("A single stamping point lookup requires one provider.");
        }

        var normalizedSeriesSlug = seriesSlug?.Trim().ToLowerInvariant();
        var query = _dbContext.StampingPoints.Include(p => p.Provider).Include(p => p.Series)
            .Where(p => p.Number == stampingPointNumber && p.ProviderId == providerFilter.ProviderId);
        if (!string.IsNullOrWhiteSpace(normalizedSeriesSlug))
        {
            query = query.Where(point => point.Series.Slug.ToLower() == normalizedSeriesSlug);
        }
        else
        {
            query = query.Where(point => point.Series.Slug == StampingSeries.TouringenStandardSlug);
        }

        return await query.FirstOrDefaultAsync()
               ?? throw EntityNotFoundException.Create<StampingPoint>(stampingPointNumber);
    }

    public async Task<StampingPoint> GetStampingPointByIdAsync(int stampingPointId, StampingProviderFilter providerFilter)
    {
        if (providerFilter.IncludesAllProviders)
        {
            throw new NotSupportedException("A single stamping point lookup requires one provider.");
        }

        return await _dbContext.StampingPoints.Include(point => point.Provider).Include(point => point.Series)
                   .FirstOrDefaultAsync(point => point.Id == stampingPointId && point.ProviderId == providerFilter.ProviderId)
               ?? throw EntityNotFoundException.Create<StampingPoint>(stampingPointId);
    }

    public async Task AddUserVisitAsync(User currentUser, int stampingPointId, DateTime? visited, bool hasVisitedTime)
    {
        var dto = await _dbContext.UserVisits.SingleOrDefaultAsync(p => p.UserId == currentUser.Id && p.StampingPointId == stampingPointId);
        if (dto == null)
        {
            dto = new UserVisit
            {
                StampingPointId = stampingPointId,
                UserId = currentUser.Id,
                Visited = visited,
                HasVisitedTime = hasVisitedTime
            };
            await _dbContext.AddAsync(dto);
            try
            {
                await _dbContext.SaveChangesAsync();
            }
            catch (DbUpdateException exception)
            {
                throw new InvalidOperationException("This stamping point has already been visited.", exception);
            }
        }
        else
        {
            throw new InvalidOperationException("This stamping point has already been visited.");
        }
    }

    public async Task UpdateUserVisitAsync(User currentUser, int stampingPointId, DateTime? visited, bool hasVisitedTime)
    {
        var userVisit = await _dbContext.UserVisits.SingleOrDefaultAsync(visit =>
            visit.UserId == currentUser.Id && visit.StampingPointId == stampingPointId)
            ?? throw EntityNotFoundException.Create<UserVisit>(stampingPointId);
        userVisit.Visited = visited;
        userVisit.HasVisitedTime = hasVisitedTime;
        await _dbContext.SaveChangesAsync();
    }

    public async Task DeleteUserVisitAsync(User currentUser, int stampingPointId)
    {
        var userVisit = await _dbContext.UserVisits.SingleOrDefaultAsync(visit =>
            visit.UserId == currentUser.Id && visit.StampingPointId == stampingPointId)
            ?? throw EntityNotFoundException.Create<UserVisit>(stampingPointId);
        _dbContext.UserVisits.Remove(userVisit);
        await _dbContext.SaveChangesAsync();
    }
}
