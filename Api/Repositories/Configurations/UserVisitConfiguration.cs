using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TourEd.Lib.Abstractions.Models;

namespace Api.Repositories.Configurations;

internal sealed class UserVisitConfiguration : IEntityTypeConfiguration<UserVisit>
{
    public void Configure(EntityTypeBuilder<UserVisit> builder)
    {
        builder.ToTable("UserVisit");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedOnAdd();
        builder.Property(p => p.EntryCreated).HasDefaultValueSql("datetime('now')");
        builder.HasIndex(p => new { p.UserId, p.StampingPointId }).IsUnique();
    }
}
