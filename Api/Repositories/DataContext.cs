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
    public DbSet<UserStampingProvider> UserStampingProviders { get; set; } = null!;
    public DbSet<UserVisit> UserVisits { get; set; } = null!;
    public DbSet<AdminAuditEntry> AdminAuditEntries { get; set; } = null!;
    public DbSet<RegistrationRequest> RegistrationRequests { get; set; } = null!;
    
    
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
            },
            new StampingProvider
            {
                Id = StampingProvider.SchluchtensteigId,
                Slug = StampingProvider.SchluchtensteigSlug,
                Name = "Schluchtensteig",
                Abbreviation = "SS",
                IsAnonymousAccessAllowed = true,
                WebsiteUri = new Uri("https://www.schluchtensteig.de/"),
                Description = "Der Schluchtensteig im Naturpark Südschwarzwald führt über 119 Kilometer in 6 Etappen von Stühlingen quer durch spektakuläre Schluchten bis nach Wehr. Entlang der Etappenorte laden Stempelstellen zum Eintragen in den Wanderpass ein."
            },
            new StampingProvider
            {
                Id = StampingProvider.HeidschnuckenwegId,
                Slug = StampingProvider.HeidschnuckenwegSlug,
                Name = "Heidschnuckenweg",
                Abbreviation = "HNW",
                IsAnonymousAccessAllowed = true,
                WebsiteUri = new Uri("https://www.heidschnuckenweg.de/"),
                Description = "Der Heidschnuckenweg verbindet auf über 220 Kilometern in 13 Etappen Hamburg-Fischbek durch die Lüneburger Heide mit der Residenzstadt Celle. Mit dem offiziellen Wanderpass werden gesammelte Stempel mit Heidschnucken-Wandernadeln belohnt."
            },
            new StampingProvider
            {
                Id = StampingProvider.HarzerKlosterwanderwegId,
                Slug = StampingProvider.HarzerKlosterwanderwegSlug,
                Name = "Harzer Klosterwanderweg",
                Abbreviation = "HKW",
                IsAnonymousAccessAllowed = true,
                WebsiteUri = new Uri("https://www.harzinfo.de/erlebnisse/harzer-kloester/harzer-klosterwanderweg"),
                Description = "Der Harzer Klosterwanderweg führt über rund 117 Kilometer entlang geschichtsträchtiger Klöster und Kirchen am Nordrand des Harzes von Goslar bis Halberstadt. 16 markante rote Stempelkästen der Harzer Wandernadel laden zum Sammeln im Begleitheft ein."
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
                },
                new
                {
                    Id = 5101,
                    Name = "Stühlingen",
                    Longitude = 8.4462100m,
                    Latitude = 47.7448200m,
                    Number = (int?)1,
                    Code = 1,
                    ProviderId = StampingProvider.SchluchtensteigId,
                    SeriesId = global::TourEd.Lib.Abstractions.Models.StampingSeries.SchluchtensteigStandardId,
                    ExternalId = "standard-1"
                },
                new
                {
                    Id = 5102,
                    Name = "Blumberg",
                    Longitude = 8.5342200m,
                    Latitude = 47.8398100m,
                    Number = (int?)2,
                    Code = 2,
                    ProviderId = StampingProvider.SchluchtensteigId,
                    SeriesId = global::TourEd.Lib.Abstractions.Models.StampingSeries.SchluchtensteigStandardId,
                    ExternalId = "standard-2"
                },
                new
                {
                    Id = 5103,
                    Name = "Schattenmühle",
                    Longitude = 8.3188500m,
                    Latitude = 47.8443100m,
                    Number = (int?)3,
                    Code = 3,
                    ProviderId = StampingProvider.SchluchtensteigId,
                    SeriesId = global::TourEd.Lib.Abstractions.Models.StampingSeries.SchluchtensteigStandardId,
                    ExternalId = "standard-3"
                },
                new
                {
                    Id = 5104,
                    Name = "Oberfischbach (Schluchsee)",
                    Longitude = 8.1637100m,
                    Latitude = 47.8182400m,
                    Number = (int?)4,
                    Code = 4,
                    ProviderId = StampingProvider.SchluchtensteigId,
                    SeriesId = global::TourEd.Lib.Abstractions.Models.StampingSeries.SchluchtensteigStandardId,
                    ExternalId = "standard-4"
                },
                new
                {
                    Id = 5105,
                    Name = "St. Blasien",
                    Longitude = 8.1294500m,
                    Latitude = 47.7601200m,
                    Number = (int?)5,
                    Code = 5,
                    ProviderId = StampingProvider.SchluchtensteigId,
                    SeriesId = global::TourEd.Lib.Abstractions.Models.StampingSeries.SchluchtensteigStandardId,
                    ExternalId = "standard-5"
                },
                new
                {
                    Id = 5106,
                    Name = "Todtmoos",
                    Longitude = 8.0002100m,
                    Latitude = 47.7397100m,
                    Number = (int?)6,
                    Code = 6,
                    ProviderId = StampingProvider.SchluchtensteigId,
                    SeriesId = global::TourEd.Lib.Abstractions.Models.StampingSeries.SchluchtensteigStandardId,
                    ExternalId = "standard-6"
                },
                new
                {
                    Id = 5201,
                    Name = "Fischbek (Hamburg)",
                    Longitude = 9.8322100m,
                    Latitude = 53.4475100m,
                    Number = (int?)1,
                    Code = 1,
                    ProviderId = StampingProvider.HeidschnuckenwegId,
                    SeriesId = global::TourEd.Lib.Abstractions.Models.StampingSeries.HeidschnuckenwegStandardId,
                    ExternalId = "standard-1"
                },
                new
                {
                    Id = 5202,
                    Name = "Buchholz in der Nordheide",
                    Longitude = 9.8708100m,
                    Latitude = 53.3275200m,
                    Number = (int?)2,
                    Code = 2,
                    ProviderId = StampingProvider.HeidschnuckenwegId,
                    SeriesId = global::TourEd.Lib.Abstractions.Models.StampingSeries.HeidschnuckenwegStandardId,
                    ExternalId = "standard-2"
                },
                new
                {
                    Id = 5203,
                    Name = "Handeloh",
                    Longitude = 9.8236200m,
                    Latitude = 53.2458100m,
                    Number = (int?)3,
                    Code = 3,
                    ProviderId = StampingProvider.HeidschnuckenwegId,
                    SeriesId = global::TourEd.Lib.Abstractions.Models.StampingSeries.HeidschnuckenwegStandardId,
                    ExternalId = "standard-3"
                },
                new
                {
                    Id = 5204,
                    Name = "Undeloh",
                    Longitude = 9.9753100m,
                    Latitude = 53.1956100m,
                    Number = (int?)4,
                    Code = 4,
                    ProviderId = StampingProvider.HeidschnuckenwegId,
                    SeriesId = global::TourEd.Lib.Abstractions.Models.StampingSeries.HeidschnuckenwegStandardId,
                    ExternalId = "standard-4"
                },
                new
                {
                    Id = 5205,
                    Name = "Niederhaverbeck",
                    Longitude = 9.9103200m,
                    Latitude = 53.1511200m,
                    Number = (int?)5,
                    Code = 5,
                    ProviderId = StampingProvider.HeidschnuckenwegId,
                    SeriesId = global::TourEd.Lib.Abstractions.Models.StampingSeries.HeidschnuckenwegStandardId,
                    ExternalId = "standard-5"
                },
                new
                {
                    Id = 5206,
                    Name = "Bispingen",
                    Longitude = 9.9986100m,
                    Latitude = 53.0833100m,
                    Number = (int?)6,
                    Code = 6,
                    ProviderId = StampingProvider.HeidschnuckenwegId,
                    SeriesId = global::TourEd.Lib.Abstractions.Models.StampingSeries.HeidschnuckenwegStandardId,
                    ExternalId = "standard-6"
                },
                new
                {
                    Id = 5207,
                    Name = "Soltau",
                    Longitude = 9.8389100m,
                    Latitude = 52.9869200m,
                    Number = (int?)7,
                    Code = 7,
                    ProviderId = StampingProvider.HeidschnuckenwegId,
                    SeriesId = global::TourEd.Lib.Abstractions.Models.StampingSeries.HeidschnuckenwegStandardId,
                    ExternalId = "standard-7"
                },
                new
                {
                    Id = 5208,
                    Name = "Wietzendorf",
                    Longitude = 9.9786200m,
                    Latitude = 52.9189100m,
                    Number = (int?)8,
                    Code = 8,
                    ProviderId = StampingProvider.HeidschnuckenwegId,
                    SeriesId = global::TourEd.Lib.Abstractions.Models.StampingSeries.HeidschnuckenwegStandardId,
                    ExternalId = "standard-8"
                },
                new
                {
                    Id = 5209,
                    Name = "Müden (Örtze)",
                    Longitude = 10.1167100m,
                    Latitude = 52.8753200m,
                    Number = (int?)9,
                    Code = 9,
                    ProviderId = StampingProvider.HeidschnuckenwegId,
                    SeriesId = global::TourEd.Lib.Abstractions.Models.StampingSeries.HeidschnuckenwegStandardId,
                    ExternalId = "standard-9"
                },
                new
                {
                    Id = 5210,
                    Name = "Faßberg",
                    Longitude = 10.1742100m,
                    Latitude = 52.9011100m,
                    Number = (int?)10,
                    Code = 10,
                    ProviderId = StampingProvider.HeidschnuckenwegId,
                    SeriesId = global::TourEd.Lib.Abstractions.Models.StampingSeries.HeidschnuckenwegStandardId,
                    ExternalId = "standard-10"
                },
                new
                {
                    Id = 5211,
                    Name = "Hermannsburg",
                    Longitude = 10.0911200m,
                    Latitude = 52.8317200m,
                    Number = (int?)11,
                    Code = 11,
                    ProviderId = StampingProvider.HeidschnuckenwegId,
                    SeriesId = global::TourEd.Lib.Abstractions.Models.StampingSeries.HeidschnuckenwegStandardId,
                    ExternalId = "standard-11"
                },
                new
                {
                    Id = 5212,
                    Name = "Eschede",
                    Longitude = 10.2444100m,
                    Latitude = 52.7344100m,
                    Number = (int?)12,
                    Code = 12,
                    ProviderId = StampingProvider.HeidschnuckenwegId,
                    SeriesId = global::TourEd.Lib.Abstractions.Models.StampingSeries.HeidschnuckenwegStandardId,
                    ExternalId = "standard-12"
                },
                new
                {
                    Id = 5213,
                    Name = "Celle (Schloss)",
                    Longitude = 10.0811200m,
                    Latitude = 52.6247200m,
                    Number = (int?)13,
                    Code = 13,
                    ProviderId = StampingProvider.HeidschnuckenwegId,
                    SeriesId = global::TourEd.Lib.Abstractions.Models.StampingSeries.HeidschnuckenwegStandardId,
                    ExternalId = "standard-13"
                },
                new
                {
                    Id = 5301,
                    Name = "Neuwerkkirche Goslar",
                    Longitude = 10.4241200m,
                    Latitude = 51.9082100m,
                    Number = (int?)1,
                    Code = 1,
                    ProviderId = StampingProvider.HarzerKlosterwanderwegId,
                    SeriesId = global::TourEd.Lib.Abstractions.Models.StampingSeries.HarzerKlosterwanderwegStandardId,
                    ExternalId = "standard-1"
                },
                new
                {
                    Id = 5302,
                    Name = "Kloster Grauhof",
                    Longitude = 10.4358100m,
                    Latitude = 51.9367100m,
                    Number = (int?)2,
                    Code = 2,
                    ProviderId = StampingProvider.HarzerKlosterwanderwegId,
                    SeriesId = global::TourEd.Lib.Abstractions.Models.StampingSeries.HarzerKlosterwanderwegStandardId,
                    ExternalId = "standard-2"
                },
                new
                {
                    Id = 5303,
                    Name = "Kloster Wöltingerode",
                    Longitude = 10.5398200m,
                    Latitude = 51.9572200m,
                    Number = (int?)3,
                    Code = 3,
                    ProviderId = StampingProvider.HarzerKlosterwanderwegId,
                    SeriesId = global::TourEd.Lib.Abstractions.Models.StampingSeries.HarzerKlosterwanderwegStandardId,
                    ExternalId = "standard-3"
                },
                new
                {
                    Id = 5304,
                    Name = "Kloster Ilsenburg",
                    Longitude = 10.6791100m,
                    Latitude = 51.8601100m,
                    Number = (int?)4,
                    Code = 4,
                    ProviderId = StampingProvider.HarzerKlosterwanderwegId,
                    SeriesId = global::TourEd.Lib.Abstractions.Models.StampingSeries.HarzerKlosterwanderwegStandardId,
                    ExternalId = "standard-4"
                },
                new
                {
                    Id = 5305,
                    Name = "Kloster Drübeck",
                    Longitude = 10.7144200m,
                    Latitude = 51.8561200m,
                    Number = (int?)5,
                    Code = 5,
                    ProviderId = StampingProvider.HarzerKlosterwanderwegId,
                    SeriesId = global::TourEd.Lib.Abstractions.Models.StampingSeries.HarzerKlosterwanderwegStandardId,
                    ExternalId = "standard-5"
                },
                new
                {
                    Id = 5306,
                    Name = "St. Laurentius Darlingerode",
                    Longitude = 10.7303100m,
                    Latitude = 51.8488100m,
                    Number = (int?)6,
                    Code = 6,
                    ProviderId = StampingProvider.HarzerKlosterwanderwegId,
                    SeriesId = global::TourEd.Lib.Abstractions.Models.StampingSeries.HarzerKlosterwanderwegStandardId,
                    ExternalId = "standard-6"
                },
                new
                {
                    Id = 5307,
                    Name = "Kloster Himmelpforte (Wernigerode)",
                    Longitude = 10.7551200m,
                    Latitude = 51.8262200m,
                    Number = (int?)7,
                    Code = 7,
                    ProviderId = StampingProvider.HarzerKlosterwanderwegId,
                    SeriesId = global::TourEd.Lib.Abstractions.Models.StampingSeries.HarzerKlosterwanderwegStandardId,
                    ExternalId = "standard-7"
                },
                new
                {
                    Id = 5308,
                    Name = "Kloster Michaelstein (Blankenburg)",
                    Longitude = 10.9142100m,
                    Latitude = 51.8061100m,
                    Number = (int?)8,
                    Code = 8,
                    ProviderId = StampingProvider.HarzerKlosterwanderwegId,
                    SeriesId = global::TourEd.Lib.Abstractions.Models.StampingSeries.HarzerKlosterwanderwegStandardId,
                    ExternalId = "standard-8"
                },
                new
                {
                    Id = 5309,
                    Name = "Bergkirche St. Bartholomäus (Blankenburg)",
                    Longitude = 10.9575200m,
                    Latitude = 51.7891200m,
                    Number = (int?)9,
                    Code = 9,
                    ProviderId = StampingProvider.HarzerKlosterwanderwegId,
                    SeriesId = global::TourEd.Lib.Abstractions.Models.StampingSeries.HarzerKlosterwanderwegStandardId,
                    ExternalId = "standard-9"
                },
                new
                {
                    Id = 5310,
                    Name = "Kloster Wendhusen (Thale)",
                    Longitude = 11.0506100m,
                    Latitude = 51.7547100m,
                    Number = (int?)10,
                    Code = 10,
                    ProviderId = StampingProvider.HarzerKlosterwanderwegId,
                    SeriesId = global::TourEd.Lib.Abstractions.Models.StampingSeries.HarzerKlosterwanderwegStandardId,
                    ExternalId = "standard-10"
                },
                new
                {
                    Id = 5311,
                    Name = "Stiftskirche St. Cyriakus Gernrode",
                    Longitude = 11.1364200m,
                    Latitude = 51.7244200m,
                    Number = (int?)11,
                    Code = 11,
                    ProviderId = StampingProvider.HarzerKlosterwanderwegId,
                    SeriesId = global::TourEd.Lib.Abstractions.Models.StampingSeries.HarzerKlosterwanderwegStandardId,
                    ExternalId = "standard-11"
                },
                new
                {
                    Id = 5312,
                    Name = "Klosterkirche St. Marien (Quedlinburg)",
                    Longitude = 11.1398100m,
                    Latitude = 51.7871100m,
                    Number = (int?)12,
                    Code = 12,
                    ProviderId = StampingProvider.HarzerKlosterwanderwegId,
                    SeriesId = global::TourEd.Lib.Abstractions.Models.StampingSeries.HarzerKlosterwanderwegStandardId,
                    ExternalId = "standard-12"
                },
                new
                {
                    Id = 5313,
                    Name = "Stiftskirche St. Servatii (Quedlinburg)",
                    Longitude = 11.1369200m,
                    Latitude = 51.7858200m,
                    Number = (int?)13,
                    Code = 13,
                    ProviderId = StampingProvider.HarzerKlosterwanderwegId,
                    SeriesId = global::TourEd.Lib.Abstractions.Models.StampingSeries.HarzerKlosterwanderwegStandardId,
                    ExternalId = "standard-13"
                },
                new
                {
                    Id = 5314,
                    Name = "Spiegelsberge (Halberstadt)",
                    Longitude = 11.0421100m,
                    Latitude = 51.8722100m,
                    Number = (int?)14,
                    Code = 14,
                    ProviderId = StampingProvider.HarzerKlosterwanderwegId,
                    SeriesId = global::TourEd.Lib.Abstractions.Models.StampingSeries.HarzerKlosterwanderwegStandardId,
                    ExternalId = "standard-14"
                },
                new
                {
                    Id = 5315,
                    Name = "Dom und Domschatz Halberstadt",
                    Longitude = 11.0483200m,
                    Latitude = 51.8958200m,
                    Number = (int?)15,
                    Code = 15,
                    ProviderId = StampingProvider.HarzerKlosterwanderwegId,
                    SeriesId = global::TourEd.Lib.Abstractions.Models.StampingSeries.HarzerKlosterwanderwegStandardId,
                    ExternalId = "standard-15"
                },
                new
                {
                    Id = 5316,
                    Name = "Kloster St. Burchardi (Halberstadt)",
                    Longitude = 11.0664100m,
                    Latitude = 51.8988100m,
                    Number = (int?)16,
                    Code = 16,
                    ProviderId = StampingProvider.HarzerKlosterwanderwegId,
                    SeriesId = global::TourEd.Lib.Abstractions.Models.StampingSeries.HarzerKlosterwanderwegStandardId,
                    ExternalId = "standard-16"
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
                new StampingSeries { Id = global::TourEd.Lib.Abstractions.Models.StampingSeries.MalerwegStandardId, ProviderId = StampingProvider.MalerwegId, Slug = global::TourEd.Lib.Abstractions.Models.StampingSeries.MalerwegStandardSlug, Name = "Standard", ExpectedPointCount = 8 },
                new StampingSeries { Id = global::TourEd.Lib.Abstractions.Models.StampingSeries.SchluchtensteigStandardId, ProviderId = StampingProvider.SchluchtensteigId, Slug = global::TourEd.Lib.Abstractions.Models.StampingSeries.SchluchtensteigStandardSlug, Name = "Standard", ExpectedPointCount = 6 },
                new StampingSeries { Id = global::TourEd.Lib.Abstractions.Models.StampingSeries.HeidschnuckenwegStandardId, ProviderId = StampingProvider.HeidschnuckenwegId, Slug = global::TourEd.Lib.Abstractions.Models.StampingSeries.HeidschnuckenwegStandardSlug, Name = "Standard", ExpectedPointCount = 13 },
                new StampingSeries { Id = global::TourEd.Lib.Abstractions.Models.StampingSeries.HarzerKlosterwanderwegStandardId, ProviderId = StampingProvider.HarzerKlosterwanderwegId, Slug = global::TourEd.Lib.Abstractions.Models.StampingSeries.HarzerKlosterwanderwegStandardSlug, Name = "Standard", ExpectedPointCount = 16 });
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
            dto.HasIndex(p => p.GoogleSubject).IsUnique();
            dto.HasOne(p => p.DefaultStampingProvider).WithMany()
                .HasForeignKey(p => p.DefaultStampingProviderId)
                .OnDelete(DeleteBehavior.Restrict);
            dto.HasMany(p => p.StampingProviders).WithOne(p => p.User);
            dto.HasMany(p => p.VisitedStampingPoints);
        });

        modelBuilder.Entity<UserStampingProvider>(dto =>
        {
            dto.HasKey(p => new { p.UserId, p.StampingProviderId });
            dto.HasOne(p => p.User).WithMany(p => p.StampingProviders)
                .HasForeignKey(p => p.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            dto.HasOne(p => p.StampingProvider).WithMany(p => p.Users)
                .HasForeignKey(p => p.StampingProviderId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AdminAuditEntry>(dto =>
        {
            dto.HasKey(p => p.Id);
            dto.Property(p => p.Id).ValueGeneratedOnAdd();
            dto.Property(p => p.CreatedAt).HasDefaultValueSql("datetime('now')");
            dto.Property(p => p.Action).IsRequired();
            dto.HasIndex(p => p.CreatedAt);
            dto.HasIndex(p => p.TargetUserId);
        });

        modelBuilder.Entity<RegistrationRequest>(dto =>
        {
            dto.HasKey(p => p.Id);
            dto.Property(p => p.Id).ValueGeneratedOnAdd();
            dto.Property(p => p.GoogleSubject).IsRequired();
            dto.Property(p => p.Email).IsRequired();
            dto.Property(p => p.Status).HasConversion<string>().IsRequired();
            dto.Property(p => p.CreatedAt).HasDefaultValueSql("datetime('now')");
            dto.HasIndex(p => p.GoogleSubject).IsUnique();
            dto.HasIndex(p => p.CreatedAt);
            dto.HasIndex(p => p.Status);
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
