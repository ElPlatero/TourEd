using TourEd.Lib.Abstractions.Models;

namespace TourEd.Lib.Abstractions.Interfaces.Services;

public interface IUserService
{
    Task<User?> GetUserOrDefaultAsync(string userEmail);
}
