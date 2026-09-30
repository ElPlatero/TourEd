using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Entities;

public sealed class AdminAuditEntry
{
    public int Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public int ActorUserId { get; set; }
    public string Action { get; set; } = null!;
    public int? TargetUserId { get; set; }
    public int? RegistrationRequestId { get; set; }
    public string? ProviderSlug { get; set; }

    internal sealed class Configuration : IEntityTypeConfiguration<AdminAuditEntry>
    {
        public void Configure(EntityTypeBuilder<AdminAuditEntry> builder)
        {
            builder.HasKey(p => p.Id);
            builder.Property(p => p.Id).ValueGeneratedOnAdd();
            builder.Property(p => p.CreatedAt).HasDefaultValueSql("datetime('now')");
            builder.Property(p => p.Action).IsRequired();
            builder.HasIndex(p => p.CreatedAt);
            builder.HasIndex(p => p.TargetUserId);
            builder.HasIndex(p => p.RegistrationRequestId);
        }
    }
}
