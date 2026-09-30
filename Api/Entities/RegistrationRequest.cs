using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Entities;

public enum RegistrationRequestStatus
{
    Pending = 0,
    Approved = 1,
    Rejected = 2
}

public sealed class RegistrationRequest
{
    public int Id { get; set; }
    public string GoogleSubject { get; set; } = null!;
    public string Email { get; set; } = null!;
    public RegistrationRequestStatus Status { get; set; } = RegistrationRequestStatus.Pending;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? DecidedAt { get; set; }
    public DateTime? AdminNotificationSentAt { get; set; }

    internal sealed class Configuration : IEntityTypeConfiguration<RegistrationRequest>
    {
        public void Configure(EntityTypeBuilder<RegistrationRequest> builder)
        {
            builder.HasKey(p => p.Id);
            builder.Property(p => p.Id).ValueGeneratedOnAdd();
            builder.Property(p => p.GoogleSubject).IsRequired();
            builder.Property(p => p.Email).IsRequired();
            builder.Property(p => p.Status).HasConversion<string>().IsRequired();
            builder.Property(p => p.CreatedAt).HasDefaultValueSql("datetime('now')");
            builder.HasIndex(p => p.GoogleSubject).IsUnique();
            builder.HasIndex(p => p.CreatedAt);
            builder.HasIndex(p => p.Status);
            builder.HasIndex(p => new { p.Status, p.AdminNotificationSentAt });
        }
    }
}
