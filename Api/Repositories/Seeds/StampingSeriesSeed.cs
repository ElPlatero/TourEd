using TourEd.Lib.Abstractions.Models;

namespace Api.Repositories.Seeds;

/// <summary>Seeded provider-scoped series; each provider has at least its standard series.</summary>
internal static class StampingSeriesSeed
{
    public static StampingSeries[] Data =>
    [
        new StampingSeries { Id = StampingSeries.TouringenStandardId, ProviderId = StampingProvider.TouringenId, Slug = StampingSeries.TouringenStandardSlug, Name = "Standard", ExpectedPointCount = 430 },
        new StampingSeries { Id = StampingSeries.TouringenNaturalTreasuresId, ProviderId = StampingProvider.TouringenId, Slug = StampingSeries.TouringenNaturalTreasuresSlug, Name = "Naturschätze", ExpectedPointCount = 8 },
        new StampingSeries { Id = StampingSeries.TouringenRhoenFamilyTrailsId, ProviderId = StampingProvider.TouringenId, Slug = StampingSeries.TouringenRhoenFamilyTrailsSlug, Name = "Familienwanderwege Rhön", ExpectedPointCount = 13 },
        new StampingSeries { Id = StampingSeries.TouringenSpecialStampsId, ProviderId = StampingProvider.TouringenId, Slug = StampingSeries.TouringenSpecialStampsSlug, Name = "Sonderstempel", IsTemporary = true },
        new StampingSeries { Id = StampingSeries.HarzerWandernadelStandardId, ProviderId = StampingProvider.HarzerWandernadelId, Slug = StampingSeries.HarzerWandernadelStandardSlug, Name = "Standard", ExpectedPointCount = 222 },
        new StampingSeries { Id = StampingSeries.MalerwegStandardId, ProviderId = StampingProvider.MalerwegId, Slug = StampingSeries.MalerwegStandardSlug, Name = "Standard", ExpectedPointCount = 8 },
        new StampingSeries { Id = StampingSeries.SchluchtensteigStandardId, ProviderId = StampingProvider.SchluchtensteigId, Slug = StampingSeries.SchluchtensteigStandardSlug, Name = "Standard", ExpectedPointCount = 6 },
        new StampingSeries { Id = StampingSeries.HeidschnuckenwegStandardId, ProviderId = StampingProvider.HeidschnuckenwegId, Slug = StampingSeries.HeidschnuckenwegStandardSlug, Name = "Standard", ExpectedPointCount = 13 },
        new StampingSeries { Id = StampingSeries.HarzerKlosterwanderwegStandardId, ProviderId = StampingProvider.HarzerKlosterwanderwegId, Slug = StampingSeries.HarzerKlosterwanderwegStandardSlug, Name = "Standard", ExpectedPointCount = 16 },
        new StampingSeries { Id = StampingSeries.BliessteigStandardId, ProviderId = StampingProvider.BliessteigId, Slug = StampingSeries.BliessteigStandardSlug, Name = "Standard", ExpectedPointCount = 10 },
        new StampingSeries { Id = StampingSeries.KellerwaldsteigStandardId, ProviderId = StampingProvider.KellerwaldsteigId, Slug = StampingSeries.KellerwaldsteigStandardSlug, Name = "Standard", ExpectedPointCount = 10 }
    ];
}
