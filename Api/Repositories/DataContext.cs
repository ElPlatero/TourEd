using Microsoft.EntityFrameworkCore;
using TourEd.Lib.Abstractions.Models;

namespace Api.Repositories;

public class DataContext : DbContext
{
    private readonly IConfiguration _configuration;

    public DataContext(IConfiguration configuration) { _configuration = configuration; }

    public DbSet<Import> Imports { get; set; } = null!;
    public DbSet<StampingProvider> StampingProviders { get; set; } = null!;
    public DbSet<StampingSeries> StampingSeries { get; set; } = null!;
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

        modelBuilder.Entity<StampingProvider>(dto =>
        {
            dto.HasKey(p => p.Id);
            dto.Property(p => p.Id).ValueGeneratedOnAdd();
            dto.Property(p => p.Slug).IsRequired();
            dto.Property(p => p.Name).IsRequired();
            dto.HasIndex(p => p.Slug).IsUnique();
            dto.HasData(new StampingProvider
            {
                Id = StampingProvider.TouringenId,
                Slug = StampingProvider.TouringenSlug,
                Name = "Touringen",
                IsAnonymousAccessAllowed = true,
                WebsiteUri = new Uri("https://www.touringen.de/"),
                Description = "Touringen ist ein im Oktober 2022 von der Funke Mediengruppe in Kooperation mit der Thüringer Tourismus GmbH und regionalen Tourismusverbänden gestartetes System, das Wandererlebnisse mit einem Sammelanreiz verbindet. Nach einer Erweiterung im Juli 2023 umfasst das Netz 430 offizielle Stempelstellen an markanten Aussichtspunkten, Kulturdenkmälern und Naturhighlights in ganz Thüringen sowie im angrenzenden Frankenwald. Neben klassischen Stempel- und Tourenheften gibt es kindgerechte Varianten sowie ein mehrstufiges Abzeichensystem, bei dem Wanderer vom „Hobby Entdecker“ (ab 10 Stempeln) bis zum vollständigen „Touringen Entdecker“ (430 Stempel) mit Pins, Urkunden und einem Eintrag in die „Hall of Fame“ ausgezeichnet werden."
            },
            new StampingProvider
            {
                Id = StampingProvider.HarzerWandernadelId,
                Slug = StampingProvider.HarzerWandernadelSlug,
                Name = "Harzer Wandernadel",
                Abbreviation = "HWN",
                IsAnonymousAccessAllowed = false,
                WebsiteUri = new Uri("https://www.harzer-wandernadel.de/"),
                Description = "Die Harzer Wandernadel ist ein seit 2006 bestehendes Wanderstempelsystem im Harz mit 222 regulären Stempelstellen. Wandernde sammeln die Stempel in einem Wanderpass und können damit verschiedene Leistungsabzeichen bis zum Harzer Wanderkaiser erreichen."
            });
        });

        modelBuilder.Entity<StampingPoint>(dto =>
        {
            dto.HasKey(p => p.Id);
            dto.Property(p => p.ProviderId).HasDefaultValue(StampingProvider.TouringenId);
            dto.Property(p => p.ExternalId).IsRequired();
            dto.HasOne(p => p.Provider).WithMany().OnDelete(DeleteBehavior.Restrict);
            dto.HasOne(p => p.Series).WithMany()
                .HasForeignKey(p => new { p.SeriesId, p.ProviderId })
                .HasPrincipalKey(p => new { p.Id, p.ProviderId })
                .OnDelete(DeleteBehavior.Restrict);
            dto.HasIndex(p => new { p.SeriesId, p.Number }).IsUnique();
            dto.HasIndex(p => new { p.ProviderId, p.ExternalId }).IsUnique();
            dto.Ignore(p => p.Position);
        });

        modelBuilder.Entity<StampingSeries>(dto =>
        {
            dto.HasKey(p => p.Id);
            dto.HasAlternateKey(p => new { p.Id, p.ProviderId });
            dto.Property(p => p.Id).ValueGeneratedOnAdd();
            dto.Property(p => p.Slug).IsRequired();
            dto.Property(p => p.Name).IsRequired();
            dto.HasOne(p => p.Provider).WithMany().OnDelete(DeleteBehavior.Restrict);
            dto.HasIndex(p => new { p.ProviderId, p.Slug }).IsUnique();
            dto.HasData(
                new StampingSeries { Id = global::TourEd.Lib.Abstractions.Models.StampingSeries.TouringenStandardId, ProviderId = StampingProvider.TouringenId, Slug = global::TourEd.Lib.Abstractions.Models.StampingSeries.TouringenStandardSlug, Name = "Standard", ExpectedPointCount = 430 },
                new StampingSeries { Id = global::TourEd.Lib.Abstractions.Models.StampingSeries.TouringenNaturalTreasuresId, ProviderId = StampingProvider.TouringenId, Slug = global::TourEd.Lib.Abstractions.Models.StampingSeries.TouringenNaturalTreasuresSlug, Name = "Naturschätze", ExpectedPointCount = 8 },
                new StampingSeries { Id = global::TourEd.Lib.Abstractions.Models.StampingSeries.TouringenRhoenFamilyTrailsId, ProviderId = StampingProvider.TouringenId, Slug = global::TourEd.Lib.Abstractions.Models.StampingSeries.TouringenRhoenFamilyTrailsSlug, Name = "Familienwanderwege Rhön", ExpectedPointCount = 13 },
                new StampingSeries { Id = global::TourEd.Lib.Abstractions.Models.StampingSeries.TouringenSpecialStampsId, ProviderId = StampingProvider.TouringenId, Slug = global::TourEd.Lib.Abstractions.Models.StampingSeries.TouringenSpecialStampsSlug, Name = "Sonderstempel", IsTemporary = true },
                new StampingSeries { Id = global::TourEd.Lib.Abstractions.Models.StampingSeries.HarzerWandernadelStandardId, ProviderId = StampingProvider.HarzerWandernadelId, Slug = global::TourEd.Lib.Abstractions.Models.StampingSeries.HarzerWandernadelStandardSlug, Name = "Standard", ExpectedPointCount = 222 });
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
            dto.Property(p => p.DefaultStampingProviderId).HasDefaultValue(StampingProvider.TouringenId);
            dto.HasIndex(p => p.GoogleSubject).IsUnique();
            dto.HasOne(p => p.DefaultStampingProvider).WithMany().OnDelete(DeleteBehavior.Restrict);
            dto.HasMany(p => p.VisitedStampingPoints);
        });

        modelBuilder.Entity<UserVisit>(dto =>
        {
            dto.ToTable("UserVisit");
            dto.HasKey(p => p.Id);
            dto.Property(p => p.Id).ValueGeneratedOnAdd();
            dto.Property(p => p.EntryCreated).HasDefaultValueSql("datetime('now')");
            dto.HasIndex(p => new { p.UserId, p.StampingPointId }).IsUnique();
        });
    }
}
