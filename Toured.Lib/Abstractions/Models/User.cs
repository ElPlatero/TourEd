namespace TourEd.Lib.Abstractions.Models;

public class User
{
    public int Id { get; set; }
    public string Email { get; set; } = null!;
    public List<UserVisit> VisitedStampingPoints { get; set; } = null!;
}
