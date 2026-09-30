using Api.Repositories.Seeds;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TourEd.Lib.Abstractions.Models;

namespace Api.Repositories.Configurations;

internal sealed class StampingProviderConfiguration : IEntityTypeConfiguration<StampingProvider>
{
    public void Configure(EntityTypeBuilder<StampingProvider> builder)
    {
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedOnAdd();
        builder.Property(p => p.Slug).IsRequired();
        builder.Property(p => p.Name).IsRequired();
        builder.HasIndex(p => p.Slug).IsUnique();
        builder.HasData(StampingProviderSeed.Data);
    }
}
