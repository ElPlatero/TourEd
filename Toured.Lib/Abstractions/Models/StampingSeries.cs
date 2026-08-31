namespace TourEd.Lib.Abstractions.Models;

public class StampingSeries
{
    public const int TouringenStandardId = 1;
    public const int TouringenNaturalTreasuresId = 2;
    public const int TouringenRhoenFamilyTrailsId = 3;
    public const int TouringenSpecialStampsId = 4;
    public const int HarzerWandernadelStandardId = 5;
    public const int MalerwegStandardId = 6;

    public const string TouringenStandardSlug = "standard";
    public const string TouringenNaturalTreasuresSlug = "naturschaetze";
    public const string TouringenRhoenFamilyTrailsSlug = "familienwanderwege-rhoen";
    public const string TouringenSpecialStampsSlug = "sonderstempel";
    public const string HarzerWandernadelStandardSlug = "standard";
    public const string MalerwegStandardSlug = "standard";

    public int Id { get; set; }
    public int ProviderId { get; set; }
    public string Slug { get; set; } = null!;
    public string Name { get; set; } = null!;
    public bool IsTemporary { get; set; }
    public int? ExpectedPointCount { get; set; }
    public StampingProvider Provider { get; set; } = null!;
}
