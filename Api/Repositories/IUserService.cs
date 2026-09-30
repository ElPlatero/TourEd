using Api.Entities;

namespace Api.Repositories;

public interface IUserService
{
    Task<User?> GetUserOrDefaultAsync(string userEmail, CancellationToken cancellationToken = default);
    Task<User?> GetUserByIdOrDefaultAsync(int userId, CancellationToken cancellationToken = default);
    Task<User?> GetUserByGoogleSubjectOrDefaultAsync(string googleSubject, CancellationToken cancellationToken = default);
    Task<bool> TryBindGoogleSubjectAsync(int userId, string googleSubject, CancellationToken cancellationToken = default);
}
