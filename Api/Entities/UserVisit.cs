using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Entities;

public class UserVisit
{   
    public int Id { get; set; }
    public int UserId { get; set; }
    public DateTime? Visited { get; set; }
    public bool HasVisitedTime { get; set; }
    public DateTime EntryCreated { get; set; }
    public int StampingPointId { get; set; }

    internal sealed class Configuration : IEntityTypeConfiguration<UserVisit>
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
}
