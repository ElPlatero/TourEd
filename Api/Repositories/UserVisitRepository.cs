using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TourEd.Lib.Abstractions.Exceptions;
using TourEd.Lib.Abstractions.Models;

namespace Api.Repositories;

public sealed class UserVisitRepository
{
    private readonly DataContext _dbContext;

    public UserVisitRepository(DataContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<UserVisit?> GetUserVisitOrDefaultAsync(User currentUser, int stampingPointId) 
        => await _dbContext.UserVisits.FirstOrDefaultAsync(p => p.StampingPointId == stampingPointId && p.UserId == currentUser.Id);

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
                throw new ConflictException("This stamping point has already been visited.", exception);
            }
        }
        else
        {
            throw new ConflictException("This stamping point has already been visited.");
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

    public async Task<(UserVisit? Visit, bool IsConflict)> SynchronizeUserVisitAsync(
        User currentUser,
        int stampingPointId,
        VisitStateValue expected,
        VisitStateValue desired)
    {
        var currentVisit = await _dbContext.UserVisits.AsNoTracking().SingleOrDefaultAsync(visit =>
            visit.UserId == currentUser.Id && visit.StampingPointId == stampingPointId);
        var current = VisitStateValue.FromVisit(currentVisit);

        if (current == desired)
        {
            return (currentVisit, false);
        }

        if (current != expected)
        {
            return (currentVisit, true);
        }

        if (!expected.IsVisited)
        {
            _dbContext.UserVisits.Add(new UserVisit
            {
                UserId = currentUser.Id,
                StampingPointId = stampingPointId,
                Visited = desired.Visited,
                HasVisitedTime = desired.HasVisitedTime
            });
            try
            {
                await _dbContext.SaveChangesAsync();
                return (await GetUserVisitOrDefaultAsync(currentUser, stampingPointId), false);
            }
            catch (DbUpdateException exception) when (
                exception.InnerException is SqliteException { SqliteErrorCode: 19 })
            {
                _dbContext.ChangeTracker.Clear();
                var concurrentVisit = await GetUserVisitOrDefaultAsync(currentUser, stampingPointId);
                return (concurrentVisit, VisitStateValue.FromVisit(concurrentVisit) != desired);
            }
        }

        var matchingVisits = _dbContext.UserVisits.Where(visit =>
            visit.UserId == currentUser.Id &&
            visit.StampingPointId == stampingPointId &&
            visit.Visited == expected.Visited &&
            visit.HasVisitedTime == expected.HasVisitedTime);

        var affected = desired.IsVisited
            ? await matchingVisits.ExecuteUpdateAsync(update => update
                .SetProperty(visit => visit.Visited, desired.Visited)
                .SetProperty(visit => visit.HasVisitedTime, desired.HasVisitedTime))
            : await matchingVisits.ExecuteDeleteAsync();

        var finalVisit = await GetUserVisitOrDefaultAsync(currentUser, stampingPointId);
        if (affected == 1 || VisitStateValue.FromVisit(finalVisit) == desired)
        {
            return (finalVisit, false);
        }

        return (finalVisit, true);
    }

    public async Task<int> SaveUserDataAsync(params UserVisit[] visits)
    {
        if (!visits.Any()) return 0;
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
        return updatedEntries.Count;
    }
}
