using Api.Repositories.Seeds;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TourEd.Lib.Abstractions.Models;

namespace Api.Repositories.Configurations;

internal sealed class StampingPointConfiguration : IEntityTypeConfiguration<StampingPoint>
{
    public void Configure(EntityTypeBuilder<StampingPoint> builder)
    {
        builder.HasKey(p => p.Id);
        builder.Property(p => p.ProviderId).HasDefaultValue(StampingProvider.TouringenId);
        builder.Property(p => p.ExternalId).IsRequired();
        builder.HasOne(p => p.Provider).WithMany().OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(p => p.Series).WithMany()
            .HasForeignKey(p => new { p.SeriesId, p.ProviderId })
            .HasPrincipalKey(p => new { p.Id, p.ProviderId })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(p => new { p.SeriesId, p.Number }).IsUnique();
        builder.HasIndex(p => new { p.ProviderId, p.ExternalId }).IsUnique();
        builder.Ignore(p => p.Position);
        builder.HasData(StampingPointSeed.Data);
    }
}
