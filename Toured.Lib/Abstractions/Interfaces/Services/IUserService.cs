using TourEd.Lib.Abstractions.Models;

namespace TourEd.Lib.Abstractions.Interfaces.Services;

public interface IUserService
{
    Task<User?> GetUserOrDefaultAsync(string userEmail, CancellationToken cancellationToken = default);
    Task<User?> GetUserByGoogleSubjectOrDefaultAsync(string googleSubject, CancellationToken cancellationToken = default);
    Task<bool> TryBindGoogleSubjectAsync(int userId, string googleSubject, CancellationToken cancellationToken = default);
}
