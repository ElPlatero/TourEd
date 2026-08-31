namespace TourEd.Lib.Abstractions.Models;

public sealed class UserStampingProvider
{
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public int StampingProviderId { get; set; }
    public StampingProvider StampingProvider { get; set; } = null!;
}
