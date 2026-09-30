using Api.Entities;
using Microsoft.EntityFrameworkCore;

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
            .ApplyConfiguration(new Import.Configuration())
            .ApplyConfiguration(new StampingProvider.Configuration())
            .ApplyConfiguration(new StampingPoint.Configuration())
            .ApplyConfiguration(new StampingSeries.Configuration())
            .ApplyConfiguration(new SortedStampingPoint.Configuration())
            .ApplyConfiguration(new HikingTour.Configuration())
            .ApplyConfiguration(new User.Configuration())
            .ApplyConfiguration(new UserStampingProvider.Configuration())
            .ApplyConfiguration(new AdminAuditEntry.Configuration())
            .ApplyConfiguration(new RegistrationRequest.Configuration())
            .ApplyConfiguration(new RegistrationNotificationState.Configuration())
            .ApplyConfiguration(new UserVisit.Configuration());
    }
}
