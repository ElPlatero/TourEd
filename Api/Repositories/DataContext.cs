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
            },
            new StampingProvider
            {
                Id = StampingProvider.MalerwegId,
                Slug = StampingProvider.MalerwegSlug,
                Name = "Malerweg",
                Abbreviation = "MW",
                IsAnonymousAccessAllowed = true,
                WebsiteUri = new Uri("https://www.saechsische-schweiz.de/malerweg"),
                Description = "Der Malerweg im Elbsandsteingebirge der Sächsischen Schweiz gehört zu den traditionsreichsten und beliebtesten Wanderwegen Deutschlands. Der offizielle Wanderpass umfasst 8 Stempelstellen entlang der Etappen."
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
            dto.HasData(
                new
                {
                    Id = 5001,
                    Name = "Liebethal",
                    Longitude = 13.9538612m,
                    Latitude = 50.9982441m,
                    Number = (int?)1,
                    Code = 1,
                    ProviderId = StampingProvider.MalerwegId,
                    SeriesId = global::TourEd.Lib.Abstractions.Models.StampingSeries.MalerwegStandardId,
                    ExternalId = "standard-1"
                },
                new
                {
                    Id = 5002,
                    Name = "Stadt Wehlen",
                    Longitude = 14.0729352m,
                    Latitude = 50.9622998m,
                    Number = (int?)2,
                    Code = 2,
                    ProviderId = StampingProvider.MalerwegId,
                    SeriesId = global::TourEd.Lib.Abstractions.Models.StampingSeries.MalerwegStandardId,
                    ExternalId = "standard-2"
                },
                new
                {
                    Id = 5003,
                    Name = "Hohnstein",
                    Longitude = 14.1105942m,
                    Latitude = 50.9788094m,
                    Number = (int?)3,
                    Code = 3,
                    ProviderId = StampingProvider.MalerwegId,
                    SeriesId = global::TourEd.Lib.Abstractions.Models.StampingSeries.MalerwegStandardId,
                    ExternalId = "standard-3"
                },
                new
                {
                    Id = 5004,
                    Name = "Brand",
                    Longitude = 14.1206126m,
                    Latitude = 50.9702213m,
                    Number = (int?)4,
                    Code = 4,
                    ProviderId = StampingProvider.MalerwegId,
                    SeriesId = global::TourEd.Lib.Abstractions.Models.StampingSeries.MalerwegStandardId,
                    ExternalId = "standard-4"
                },
                new
                {
                    Id = 5005,
                    Name = "Neumannmühle",
                    Longitude = 14.1843440m,
                    Latitude = 50.9416556m,
                    Number = (int?)5,
                    Code = 5,
                    ProviderId = StampingProvider.MalerwegId,
                    SeriesId = global::TourEd.Lib.Abstractions.Models.StampingSeries.MalerwegStandardId,
                    ExternalId = "standard-5"
                },
                new
                {
                    Id = 5006,
                    Name = "Großer Zschirnstein",
                    Longitude = 14.2562470m,
                    Latitude = 50.9080517m,
                    Number = (int?)6,
                    Code = 6,
                    ProviderId = StampingProvider.MalerwegId,
                    SeriesId = global::TourEd.Lib.Abstractions.Models.StampingSeries.MalerwegStandardId,
                    ExternalId = "standard-6"
                },
                new
                {
                    Id = 5007,
                    Name = "Gohrisch",
                    Longitude = 14.1206126m,
                    Latitude = 50.8872242m,
                    Number = (int?)7,
                    Code = 7,
                    ProviderId = StampingProvider.MalerwegId,
                    SeriesId = global::TourEd.Lib.Abstractions.Models.StampingSeries.MalerwegStandardId,
                    ExternalId = "standard-7"
                },
                new
                {
                    Id = 5008,
                    Name = "Rauenstein",
                    Longitude = 14.0734005m,
                    Latitude = 50.9255018m,
                    Number = (int?)8,
                    Code = 8,
                    ProviderId = StampingProvider.MalerwegId,
                    SeriesId = global::TourEd.Lib.Abstractions.Models.StampingSeries.MalerwegStandardId,
                    ExternalId = "standard-8"
                });
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
                new StampingSeries { Id = global::TourEd.Lib.Abstractions.Models.StampingSeries.HarzerWandernadelStandardId, ProviderId = StampingProvider.HarzerWandernadelId, Slug = global::TourEd.Lib.Abstractions.Models.StampingSeries.HarzerWandernadelStandardSlug, Name = "Standard", ExpectedPointCount = 222 },
                new StampingSeries { Id = global::TourEd.Lib.Abstractions.Models.StampingSeries.MalerwegStandardId, ProviderId = StampingProvider.MalerwegId, Slug = global::TourEd.Lib.Abstractions.Models.StampingSeries.MalerwegStandardSlug, Name = "Standard", ExpectedPointCount = 8 });
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
