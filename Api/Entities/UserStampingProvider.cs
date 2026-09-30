using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Entities;

public sealed class UserStampingProvider
{
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public int StampingProviderId { get; set; }
    public StampingProvider StampingProvider { get; set; } = null!;

    internal sealed class Configuration : IEntityTypeConfiguration<UserStampingProvider>
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
}
