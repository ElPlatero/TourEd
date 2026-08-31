namespace TourEd.Lib.Abstractions.Models;

public class User
{
    public int Id { get; set; }
    public string Email { get; set; } = null!;
    public string? GoogleSubject { get; set; }
    public int? DefaultStampingProviderId { get; set; }
    public StampingProvider? DefaultStampingProvider { get; set; }
    public List<UserStampingProvider> StampingProviders { get; set; } = [];
    public List<UserVisit> VisitedStampingPoints { get; set; } = null!;
}
