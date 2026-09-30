using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TourEd.Lib.Abstractions.Models;

namespace Api.Repositories.Configurations;

internal sealed class AdminAuditEntryConfiguration : IEntityTypeConfiguration<AdminAuditEntry>
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
