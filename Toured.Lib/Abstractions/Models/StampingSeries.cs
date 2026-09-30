namespace TourEd.Lib.Abstractions.Models;

public class StampingSeries
{
    public const int TouringenStandardId = 1;
    public const int TouringenNaturalTreasuresId = 2;
    public const int TouringenRhoenFamilyTrailsId = 3;
    public const int TouringenSpecialStampsId = 4;
    public const int HarzerWandernadelStandardId = 5;
    public const int MalerwegStandardId = 6;
    public const int SchluchtensteigStandardId = 7;
    public const int HeidschnuckenwegStandardId = 8;
    public const int HarzerKlosterwanderwegStandardId = 9;
    public const int BliessteigStandardId = 10;
    public const int KellerwaldsteigStandardId = 11;

    /// <summary>Slug of every provider's standard series; stamping point numbers default to it.</summary>
    public const string DefaultSlug = "standard";

    public const string TouringenStandardSlug = DefaultSlug;
    public const string TouringenNaturalTreasuresSlug = "naturschaetze";
    public const string TouringenRhoenFamilyTrailsSlug = "familienwanderwege-rhoen";
    public const string TouringenSpecialStampsSlug = "sonderstempel";
    public const string HarzerWandernadelStandardSlug = DefaultSlug;
    public const string MalerwegStandardSlug = DefaultSlug;
    public const string SchluchtensteigStandardSlug = DefaultSlug;
    public const string HeidschnuckenwegStandardSlug = DefaultSlug;
    public const string HarzerKlosterwanderwegStandardSlug = DefaultSlug;
    public const string BliessteigStandardSlug = DefaultSlug;
    public const string KellerwaldsteigStandardSlug = DefaultSlug;

    public int Id { get; set; }
    public int ProviderId { get; set; }
    public string Slug { get; set; } = null!;
    public string Name { get; set; } = null!;
    public bool IsTemporary { get; set; }
    public int? ExpectedPointCount { get; set; }
    public StampingProvider Provider { get; set; } = null!;
}
