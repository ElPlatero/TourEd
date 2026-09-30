using Api.Dto;
using Api.Repositories;
using TourEd.Lib.Abstractions;
using TourEd.Lib.Abstractions.Exceptions;
using TourEd.Lib.Abstractions.Models;

namespace Api.Managers;

public sealed class AdminUserManager
{
    private readonly UserRepository _users;
    private readonly RegistrationRequestRepository _registrationRequests;
    private readonly AdminAuditRepository _audit;
    private readonly TouredRepository _repository;
    private readonly IUnitOfWorkFactory _unitOfWorkFactory;

    public AdminUserManager(
        UserRepository users,
        RegistrationRequestRepository registrationRequests,
        AdminAuditRepository audit,
        TouredRepository repository,
        IUnitOfWorkFactory unitOfWorkFactory)
    {
        _users = users;
        _registrationRequests = registrationRequests;
        _audit = audit;
        _repository = repository;
        _unitOfWorkFactory = unitOfWorkFactory;
    }

    public async Task<List<AdminUserDto>> GetUsersAsync(CancellationToken cancellationToken)
        => (await _users.GetUserSummariesAsync(cancellationToken)).Select(CreateDto).ToList();

    public async Task<List<AdminProviderDto>> GetProvidersAsync(CancellationToken cancellationToken)
        => (await _repository.GetStampingProvidersAsync(includeRestrictedProviders: true))
            .Select(provider => new AdminProviderDto(provider.Id, provider.Slug, provider.Name, provider.Abbreviation))
            .ToList();

    public async Task<List<AdminAuditEntryDto>> GetAuditEntriesAsync(
        int offset,
        int limit,
        CancellationToken cancellationToken)
        => (await _audit.GetEntriesAsync(offset, limit, cancellationToken))
            .Select(entry => new AdminAuditEntryDto(
                entry.Id,
                entry.CreatedAt,
                entry.ActorUserId,
                entry.Action,
                entry.TargetUserId,
                entry.RegistrationRequestId,
                entry.ProviderSlug))
            .ToList();

    public async Task<bool> DeleteUserAsync(int userId, int actorUserId, CancellationToken cancellationToken)
    {
        await using var unitOfWork = await _unitOfWorkFactory.BeginAsync(cancellationToken);
        var user = await _users.GetUserForUpdateAsync(userId, cancellationToken);
        if (user is null)
        {
            return false;
        }

        var registrationRequests = await _registrationRequests.GetRequestsForIdentityForUpdateAsync(
            user.GoogleSubject,
            user.Email.Trim().ToLowerInvariant(),
            cancellationToken);

        _registrationRequests.RemoveRange(registrationRequests);
        _users.Remove(user);
        _audit.Add(actorUserId, "user.deleted", userId, null);
        await _users.SaveChangesAsync(cancellationToken);
        await unitOfWork.CommitAsync(cancellationToken);
        return true;
    }

    public async Task<AdminUserDto?> UpdateProvidersAsync(
        int userId,
        UpdateAdminUserProvidersRequestDto request,
        int actorUserId,
        CancellationToken cancellationToken)
    {
        var requestedSlugs = request.Providers
            .Select(slug => slug?.Trim().ToLowerInvariant())
            .Where(slug => !string.IsNullOrWhiteSpace(slug))
            .Cast<string>()
            .ToHashSet(StringComparer.Ordinal);
        if (requestedSlugs.Count != request.Providers.Count)
        {
            throw new RequestValidationException("Provider slugs must be non-empty and unique.");
        }

        var normalizedDefault = string.IsNullOrWhiteSpace(request.DefaultProvider)
            ? null
            : request.DefaultProvider.Trim().ToLowerInvariant();
        if (normalizedDefault is not null && !requestedSlugs.Contains(normalizedDefault))
        {
            throw new RequestValidationException("The default provider must be included in Providers.");
        }

        var providersBySlug = (await _repository.GetStampingProvidersAsync(includeRestrictedProviders: true))
            .Where(provider => requestedSlugs.Contains(provider.Slug.ToLowerInvariant()))
            .ToDictionary(provider => provider.Slug.ToLowerInvariant(), StringComparer.Ordinal);
        var unknownSlugs = requestedSlugs.Except(providersBySlug.Keys).Order().ToArray();
        if (unknownSlugs.Length > 0)
        {
            throw new RequestValidationException($"Unknown provider(s): {string.Join(", ", unknownSlugs)}.");
        }

        var user = await _users.GetUserWithProvidersForUpdateAsync(userId, cancellationToken);
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

        _users.RemoveProviderAccess(removed.Select(slug => existingBySlug[slug]));
        foreach (var slug in added)
        {
            user.StampingProviders.Add(new UserStampingProvider
            {
                UserId = user.Id,
                StampingProviderId = providersBySlug[slug].Id
            });
        }

        user.DefaultStampingProviderId = normalizedDefault is null ? null : providersBySlug[normalizedDefault].Id;

        foreach (var slug in removed)
        {
            _audit.Add(actorUserId, "provider.revoked", userId, slug);
        }
        foreach (var slug in added)
        {
            _audit.Add(actorUserId, "provider.granted", userId, slug);
        }
        if (previousDefault != user.DefaultStampingProviderId)
        {
            _audit.Add(actorUserId, "default-provider.changed", userId, normalizedDefault);
        }

        await _users.SaveChangesAsync(cancellationToken);
        return CreateDto(await _users.GetUserSummaryAsync(userId, cancellationToken));
    }

    private static AdminUserDto CreateDto(UserSummary user)
        => new(user.Id, user.Email, user.IsGoogleLinked, user.DefaultProviderSlug, user.ProviderSlugs, user.VisitCount);
}
