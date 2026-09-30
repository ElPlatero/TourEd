using Api.Entities;
using Api.Repositories.Seeds;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;

namespace Api.Repositories.Configurations;

internal sealed class StampingSeriesConfiguration : IEntityTypeConfiguration<StampingSeries>
{
    public void Configure(EntityTypeBuilder<StampingSeries> builder)
    {
        builder.HasKey(p => p.Id);
        builder.HasAlternateKey(p => new { p.Id, p.ProviderId });
        builder.Property(p => p.Id).ValueGeneratedOnAdd();
        builder.Property(p => p.Slug).IsRequired();
        builder.Property(p => p.Name).IsRequired();
        builder.HasOne(p => p.Provider).WithMany().OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(p => new { p.ProviderId, p.Slug }).IsUnique();
        builder.HasData(StampingSeriesSeed.Data);
    }
}
