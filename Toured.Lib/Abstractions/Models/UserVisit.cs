namespace TourEd.Lib.Abstractions.Models;

public class UserVisit
{   
    public int Id { get; set; }
    public int UserId { get; set; }
    public DateTime? Visited { get; set; }
    public DateTime EntryCreated { get; set; }
    public int StampingPointId { get; set; }
}