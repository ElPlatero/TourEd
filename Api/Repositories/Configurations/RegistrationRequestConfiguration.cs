using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TourEd.Lib.Abstractions.Models;

namespace Api.Repositories.Configurations;

internal sealed class RegistrationRequestConfiguration : IEntityTypeConfiguration<RegistrationRequest>
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
