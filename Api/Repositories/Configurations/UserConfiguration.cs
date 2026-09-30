using Api.Entities;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;

namespace Api.Repositories.Configurations;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedOnAdd();
        builder.HasIndex(p => p.GoogleSubject).IsUnique();
        builder.HasOne(p => p.DefaultStampingProvider).WithMany()
            .HasForeignKey(p => p.DefaultStampingProviderId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(p => p.StampingProviders).WithOne(p => p.User);
        builder.HasMany(p => p.VisitedStampingPoints);
    }
}
