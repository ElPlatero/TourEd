using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TourEd.Lib.Abstractions.Models;

public record SortedStampingPoint([property: Key, Column(Order = 0)] int Position)
{
    [Key, Column(Order = 1)] public int StampingPointId { get; init; }
    [Key, Column(Order = 2)] public HikingTour Tour { get; set; } = null!;
    
    public StampingPoint? StampingPoint { get; set; }
}
