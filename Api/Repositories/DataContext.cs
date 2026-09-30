using Api.Repositories.Configurations;
using Microsoft.EntityFrameworkCore;
using TourEd.Lib.Abstractions.Models;

namespace Api.Repositories;

public class DataContext : DbContext
{
    public DataContext(DbContextOptions<DataContext> options) : base(options)
    {
    }

    public DbSet<Import> Imports { get; set; } = null!;
    public DbSet<StampingProvider> StampingProviders { get; set; } = null!;
    public DbSet<StampingSeries> StampingSeries { get; set; } = null!;
    public DbSet<StampingPoint> StampingPoints { get; set; } = null!;
    public DbSet<SortedStampingPoint> StampingPointsInTours { get; set; } = null!;
    public DbSet<HikingTour> HikingTours { get; set; } = null!;
    public DbSet<User> Users { get; set; } = null!;
    public DbSet<UserStampingProvider> UserStampingProviders { get; set; } = null!;
    public DbSet<UserVisit> UserVisits { get; set; } = null!;
    public DbSet<AdminAuditEntry> AdminAuditEntries { get; set; } = null!;
    public DbSet<RegistrationRequest> RegistrationRequests { get; set; } = null!;
    public DbSet<RegistrationNotificationState> RegistrationNotificationStates { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder
            .ApplyConfiguration(new ImportConfiguration())
            .ApplyConfiguration(new StampingProviderConfiguration())
            .ApplyConfiguration(new StampingPointConfiguration())
            .ApplyConfiguration(new StampingSeriesConfiguration())
            .ApplyConfiguration(new SortedStampingPointConfiguration())
            .ApplyConfiguration(new HikingTourConfiguration())
            .ApplyConfiguration(new UserConfiguration())
            .ApplyConfiguration(new UserStampingProviderConfiguration())
            .ApplyConfiguration(new AdminAuditEntryConfiguration())
            .ApplyConfiguration(new RegistrationRequestConfiguration())
            .ApplyConfiguration(new RegistrationNotificationStateConfiguration())
            .ApplyConfiguration(new UserVisitConfiguration());
    }
}
