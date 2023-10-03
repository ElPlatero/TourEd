using Microsoft.EntityFrameworkCore;
using TourEd.Lib.Abstractions.Models;

namespace Api.Repositories;

public class DataContext : DbContext
{
    private readonly IConfiguration _configuration;

    public DataContext(IConfiguration configuration) { _configuration = configuration; }

    public DbSet<Import> Imports { get; set; } = null!;
    public DbSet<StampingPoint> StampingPoints { get; set; } = null!;
    public DbSet<SortedStampingPoint> StampingPointsInTours { get; set; } = null!;
    public DbSet<HikingTour> HikingTours { get; set; } = null!;
    public DbSet<User> Users { get; set; } = null!;
    public DbSet<UserVisit> UserVisits { get; set; } = null!;
    
    
    protected override void OnConfiguring(DbContextOptionsBuilder options)
    {
        options.UseSqlite(_configuration.GetConnectionString("TouredDb"));
        options.EnableSensitiveDataLogging();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Import>(dto =>
        {
            dto.HasKey(p => p.Id);
            dto.Property(p => p.Id).ValueGeneratedOnAdd();
            dto.Property(p => p.Date).HasDefaultValueSql("datetime('now')");
        });

        modelBuilder.Entity<StampingPoint>(dto =>
        {
            dto.HasKey(p => p.Id);
            dto.Ignore(p => p.Position);
        });

        modelBuilder.Entity<SortedStampingPoint>(dto =>
        {
            dto.ToTable("SortedStampingPoint");
            dto.HasKey("Position", "StampingPointId", "TourId");
            dto.HasOne(p => p.StampingPoint);
        });
        
        modelBuilder.Entity<HikingTour>(dto =>
        {
            dto.HasKey(p => p.Id);
            dto.HasMany(p => p.StampingPoints).WithOne(p => p.Tour);
        });

        modelBuilder.Entity<User>(dto =>
        {
            dto.HasKey(p => p.Id);
            dto.Property(p => p.Id).ValueGeneratedOnAdd();
            dto.HasMany(p => p.VisitedStampingPoints);
        });

        modelBuilder.Entity<UserVisit>(dto =>
        {
            dto.ToTable("UserVisit");
            dto.HasKey(p => p.Id);
            dto.Property(p => p.Id).ValueGeneratedOnAdd();
            dto.Property(p => p.EntryCreated).HasDefaultValueSql("datetime('now')");
        });
    }
}
