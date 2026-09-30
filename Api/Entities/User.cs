using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Entities;

public class User
{
    public int Id { get; set; }
    public string Email { get; set; } = null!;
    public string? GoogleSubject { get; set; }
    public int? DefaultStampingProviderId { get; set; }
    public StampingProvider? DefaultStampingProvider { get; set; }
    public List<UserStampingProvider> StampingProviders { get; set; } = [];
    public List<UserVisit> VisitedStampingPoints { get; set; } = null!;

    internal sealed class Configuration : IEntityTypeConfiguration<User>
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
}
