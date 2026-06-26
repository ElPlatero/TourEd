namespace TourEd.Lib.Abstractions.Models;

public class User
{
    public int Id { get; set; }
    public string Email { get; set; } = null!;
    public int DefaultStampingProviderId { get; set; } = StampingProvider.TouringenId;
    public StampingProvider DefaultStampingProvider { get; set; } = null!;
    public List<UserVisit> VisitedStampingPoints { get; set; } = null!;
}
