using Api.Entities.Seeds;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Entities;

public sealed record StampingPoint(int Id, string Name, decimal Longitude, decimal Latitude, int? Number, int Code, int ProviderId, string ExternalId)
{
    public int SeriesId { get; init; }
    public DateOnly? ValidFrom { get; init; }
    public DateOnly? ValidUntil { get; init; }
    public Position Position { get; } = new(Longitude, Latitude);
    public StampingProvider Provider { get; init; } = null!;
    public StampingSeries Series { get; init; } = null!;

    internal sealed class Configuration : IEntityTypeConfiguration<StampingPoint>
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
}
