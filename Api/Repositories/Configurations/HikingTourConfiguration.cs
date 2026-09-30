using Api.Entities;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;

namespace Api.Repositories.Configurations;

internal sealed class HikingTourConfiguration : IEntityTypeConfiguration<HikingTour>
{
    public void Configure(EntityTypeBuilder<HikingTour> builder)
    {
        builder.HasKey(p => p.Id);
        builder.HasMany(p => p.StampingPoints).WithOne(p => p.Tour);
    }
}
