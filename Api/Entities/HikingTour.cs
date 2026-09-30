using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Entities;

public sealed record HikingTour(int Id, string Name, string? Startpoint, string? Endpoint, Uri? KomootUri, bool IsKidsTour, bool IsCircularPath, bool IsLongDistanceTrail)
{
    public List<SortedStampingPoint> StampingPoints { get; set; } = null!;

    internal sealed class Configuration : IEntityTypeConfiguration<HikingTour>
    {
        public void Configure(EntityTypeBuilder<HikingTour> builder)
        {
            builder.HasKey(p => p.Id);
            builder.HasMany(p => p.StampingPoints).WithOne(p => p.Tour);
        }
    }
}
