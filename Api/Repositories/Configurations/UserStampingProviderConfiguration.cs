using Api.Entities;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;

namespace Api.Repositories.Configurations;

internal sealed class UserStampingProviderConfiguration : IEntityTypeConfiguration<UserStampingProvider>
{
    public void Configure(EntityTypeBuilder<UserStampingProvider> builder)
    {
        builder.HasKey(p => new { p.UserId, p.StampingProviderId });
        builder.HasOne(p => p.User).WithMany(p => p.StampingProviders)
            .HasForeignKey(p => p.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(p => p.StampingProvider).WithMany(p => p.Users)
            .HasForeignKey(p => p.StampingProviderId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
