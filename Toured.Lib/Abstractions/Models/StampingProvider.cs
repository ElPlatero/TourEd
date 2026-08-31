namespace TourEd.Lib.Abstractions.Models;

public class StampingProvider
{
    public const int TouringenId = 1;
    public const string TouringenSlug = "touringen";
    public const int HarzerWandernadelId = 2;
    public const string HarzerWandernadelSlug = "harzer-wandernadel";
    public const int MalerwegId = 3;
    public const string MalerwegSlug = "malerweg";
    public const int SchluchtensteigId = 4;
    public const string SchluchtensteigSlug = "schluchtensteig";
    public const int HeidschnuckenwegId = 5;
    public const string HeidschnuckenwegSlug = "heidschnuckenweg";
    public const int HarzerKlosterwanderwegId = 6;
    public const string HarzerKlosterwanderwegSlug = "harzer-klosterwanderweg";

    public int Id { get; set; }
    public string Slug { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string? Abbreviation { get; set; }
    public bool IsAnonymousAccessAllowed { get; set; }
    public Uri? WebsiteUri { get; set; }
    public string? Description { get; set; }
    public Uri? DataSourceUri { get; set; }
    public string? DataSourceAttribution { get; set; }
    public string? DataLicenseName { get; set; }
    public Uri? DataLicenseUri { get; set; }
    public string? DataSourceRevision { get; set; }
    public DateTime? DataSourceUpdatedAt { get; set; }
    public DateTime? DataImportedAt { get; set; }
}
