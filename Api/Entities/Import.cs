using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Entities;

public record Import(int Id, DateTime Date, int StampingPointsCount, int HikingToursCount)
{
    internal sealed class Configuration : IEntityTypeConfiguration<Import>
    {
        public void Configure(EntityTypeBuilder<Import> builder)
        {
            builder.HasKey(p => p.Id);
            builder.Property(p => p.Id).ValueGeneratedOnAdd();
            builder.Property(p => p.Date).HasDefaultValueSql("datetime('now')");
        }
    }
}
