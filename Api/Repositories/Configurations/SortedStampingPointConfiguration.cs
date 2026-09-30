using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TourEd.Lib.Abstractions.Models;

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
