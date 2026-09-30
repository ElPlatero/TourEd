using Api.Entities;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;

namespace Api.Repositories.Configurations;

internal sealed class SortedStampingPointConfiguration : IEntityTypeConfiguration<SortedStampingPoint>
{
    public void Configure(EntityTypeBuilder<SortedStampingPoint> builder)
    {
        builder.ToTable("SortedStampingPoint");
        builder.HasKey("Position", "StampingPointId", "TourId");
        builder.HasOne(p => p.StampingPoint);
    }
}
