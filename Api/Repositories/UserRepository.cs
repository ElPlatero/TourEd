using Microsoft.EntityFrameworkCore;
using TourEd.Lib.Abstractions.Interfaces.Services;
using TourEd.Lib.Abstractions.Models;

namespace Api.Repositories;

public sealed class UserRepository : IUserService
{
    private readonly DataContext _dbContext;

    public UserRepository(DataContext dbContext)
    {
        _dbContext = dbContext;
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

    public Task<List<UserSummary>> GetUserSummariesAsync(CancellationToken cancellationToken = default)
        => SelectSummaries(_dbContext.Users.AsNoTracking()
                .OrderBy(user => user.Email)
                .ThenBy(user => user.Id))
            .ToListAsync(cancellationToken);

    public Task<UserSummary> GetUserSummaryAsync(int userId, CancellationToken cancellationToken = default)
        => SelectSummaries(_dbContext.Users.AsNoTracking().Where(user => user.Id == userId))
            .SingleAsync(cancellationToken);

    public Task<User?> GetUserForUpdateAsync(int userId, CancellationToken cancellationToken = default)
        => _dbContext.Users.SingleOrDefaultAsync(user => user.Id == userId, cancellationToken);

    public Task<User?> GetUserWithProvidersForUpdateAsync(int userId, CancellationToken cancellationToken = default)
        => _dbContext.Users
            .Include(user => user.StampingProviders)
            .ThenInclude(access => access.StampingProvider)
            .SingleOrDefaultAsync(user => user.Id == userId, cancellationToken);

    public Task<User?> FindUserByGoogleSubjectForUpdateAsync(string googleSubject, CancellationToken cancellationToken = default)
        => _dbContext.Users.FirstOrDefaultAsync(user => user.GoogleSubject == googleSubject, cancellationToken);

    public Task<User?> FindUserByNormalizedEmailForUpdateAsync(string normalizedEmail, CancellationToken cancellationToken = default)
        => _dbContext.Users.FirstOrDefaultAsync(user => user.Email.ToLower() == normalizedEmail, cancellationToken);

    public void Add(User user) => _dbContext.Users.Add(user);

    public void Remove(User user) => _dbContext.Users.Remove(user);

    public void RemoveProviderAccess(IEnumerable<UserStampingProvider> accesses)
        => _dbContext.UserStampingProviders.RemoveRange(accesses);

    /// <summary>Persists all pending changes of the request's shared <see cref="DataContext"/>.</summary>
    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => _dbContext.SaveChangesAsync(cancellationToken);

    private static IQueryable<UserSummary> SelectSummaries(IQueryable<User> users)
        => users.Select(user => new UserSummary(
            user.Id,
            user.Email,
            user.GoogleSubject != null,
            user.DefaultStampingProvider == null ? null : user.DefaultStampingProvider.Slug,
            user.StampingProviders
                .OrderBy(access => access.StampingProvider.Name)
                .ThenBy(access => access.StampingProvider.Slug)
                .Select(access => access.StampingProvider.Slug)
                .ToList(),
            user.VisitedStampingPoints.Count));
}
