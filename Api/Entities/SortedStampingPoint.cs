using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Entities;

public record SortedStampingPoint(int Position)
{
    public int StampingPointId { get; init; }
    public HikingTour Tour { get; set; } = null!;

    public StampingPoint? StampingPoint { get; set; }

    internal sealed class Configuration : IEntityTypeConfiguration<SortedStampingPoint>
    {
        public void Configure(EntityTypeBuilder<SortedStampingPoint> builder)
        {
            builder.ToTable("SortedStampingPoint");
            builder.HasKey("Position", "StampingPointId", "TourId");
            builder.Property(p => p.Position).HasColumnOrder(0);
            builder.Property(p => p.StampingPointId).HasColumnOrder(1);
            builder.HasOne(p => p.StampingPoint);
        }
    }
}
