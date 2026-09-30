using Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace Api.Repositories;

public sealed class RegistrationRequestRepository : IRegistrationRequestService
{
    private readonly DataContext _dbContext;
    private readonly IUnitOfWorkFactory _unitOfWorkFactory;

    public RegistrationRequestRepository(DataContext dbContext)
    {
        _dbContext = dbContext;
        _unitOfWorkFactory = new UnitOfWorkFactory(dbContext);
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
        existing.AdminNotificationSentAt = null;
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

    public Task<List<RegistrationRequest>> GetRequestsAsync(
        RegistrationRequestStatus? status = null,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.RegistrationRequests.AsNoTracking();
        if (status is { } requestedStatus)
        {
            query = query.Where(r => r.Status == requestedStatus);
        }

        return query
            .OrderBy(r => r.Status == RegistrationRequestStatus.Pending ? 0 : 1)
            .ThenBy(r => r.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public Task<RegistrationRequest?> GetRequestForUpdateAsync(int id, CancellationToken cancellationToken = default)
        => _dbContext.RegistrationRequests.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    public Task<List<RegistrationRequest>> GetRequestsForIdentityForUpdateAsync(
        string? googleSubject,
        string normalizedEmail,
        CancellationToken cancellationToken = default)
        => _dbContext.RegistrationRequests
            .Where(request =>
                (googleSubject != null && request.GoogleSubject == googleSubject) ||
                request.Email.ToLower() == normalizedEmail)
            .ToListAsync(cancellationToken);

    public void RemoveRange(IEnumerable<RegistrationRequest> requests)
        => _dbContext.RegistrationRequests.RemoveRange(requests);

    /// <summary>Persists all pending changes of the request's shared <see cref="DataContext"/>.</summary>
    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => _dbContext.SaveChangesAsync(cancellationToken);

    public async Task ClaimRegistrationDecisionAsync(
        RegistrationRequest request,
        RegistrationRequestStatus status,
        CancellationToken cancellationToken)
    {
        var decidedAt = DateTime.UtcNow;
        // Both callers run inside a unit of work. Claim the still-pending row before any
        // user or audit writes, even if another request decided it after our read.
        var updated = await _dbContext.RegistrationRequests
            .Where(r => r.Id == request.Id && r.Status == RegistrationRequestStatus.Pending)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(r => r.Status, status)
                .SetProperty(r => r.DecidedAt, decidedAt)
                .SetProperty(r => r.UpdatedAt, decidedAt), cancellationToken);
        if (updated != 1)
        {
            throw new RegistrationRequestAlreadyDecidedException(request.Id);
        }

        // Keep the tracked entity and response consistent with the claimed status.
        // SaveChanges and all remaining writes still run inside the same transaction.
        request.Status = status;
        request.DecidedAt = decidedAt;
        request.UpdatedAt = decidedAt;
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

    public async Task<List<int>> GetUnnotifiedPendingRegistrationRequestIdsAsync(
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.RegistrationRequests
            .Where(r => r.Status == RegistrationRequestStatus.Pending && r.AdminNotificationSentAt == null)
            .OrderBy(r => r.Id)
            .Select(r => r.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> CountPendingRegistrationRequestsAsync(
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.RegistrationRequests
            .CountAsync(r => r.Status == RegistrationRequestStatus.Pending, cancellationToken);
    }

    public async Task<DateTime?> GetLastRegistrationNotificationSentAtAsync(
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.RegistrationNotificationStates
            .Where(state => state.Id == RegistrationNotificationState.SingletonId)
            .Select(state => state.LastSentAt)
            .SingleAsync(cancellationToken);
    }

    public async Task<int> MarkRegistrationRequestsAdminNotifiedAsync(
        IReadOnlyCollection<int> requestIds,
        DateTime sentAt,
        CancellationToken cancellationToken = default)
    {
        if (requestIds.Count == 0)
        {
            return 0;
        }

        await using var unitOfWork = await _unitOfWorkFactory.BeginAsync(cancellationToken);

        var markedCount = await _dbContext.RegistrationRequests
            .Where(r => requestIds.Contains(r.Id) &&
                        r.Status == RegistrationRequestStatus.Pending &&
                        r.AdminNotificationSentAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.AdminNotificationSentAt, sentAt), cancellationToken);

        var updatedStateCount = await _dbContext.RegistrationNotificationStates
            .Where(state => state.Id == RegistrationNotificationState.SingletonId)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(state => state.LastSentAt, sentAt),
                cancellationToken);
        if (updatedStateCount != 1)
        {
            throw new InvalidOperationException("The registration notification state is missing.");
        }

        await unitOfWork.CommitAsync(cancellationToken);
        return markedCount;
    }
}
