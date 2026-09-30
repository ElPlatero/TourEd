using Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace Api.Repositories;

public sealed class AdminAuditRepository
{
    private readonly DataContext _dbContext;

    public AdminAuditRepository(DataContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>Adds an audit entry to the shared context; it is persisted with the caller's next save.</summary>
    public void Add(
        int actorUserId,
        string action,
        int? targetUserId,
        string? providerSlug,
        int? registrationRequestId = null)
        => _dbContext.AdminAuditEntries.Add(new AdminAuditEntry
        {
            CreatedAt = DateTime.UtcNow,
            ActorUserId = actorUserId,
            Action = action,
            TargetUserId = targetUserId,
            RegistrationRequestId = registrationRequestId,
            ProviderSlug = providerSlug
        });

    public Task<List<AdminAuditEntry>> GetEntriesAsync(
        int offset,
        int limit,
        CancellationToken cancellationToken = default)
        => _dbContext.AdminAuditEntries
            .AsNoTracking()
            .OrderByDescending(entry => entry.CreatedAt)
            .ThenByDescending(entry => entry.Id)
            .Skip(offset)
            .Take(limit)
            .ToListAsync(cancellationToken);

    public async Task<int> CleanupExpiredAdminAuditEntriesAsync(
        DateTime createdBefore,
        CancellationToken cancellationToken = default)
    {
        var deletedCount = await _dbContext.AdminAuditEntries
            .Where(entry => entry.CreatedAt < createdBefore)
            .ExecuteDeleteAsync(cancellationToken);

        return deletedCount;
    }
}
