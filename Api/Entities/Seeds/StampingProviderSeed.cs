namespace Api.Entities.Seeds;

/// <summary>Seeded stamping providers; imported providers receive their provenance on import.</summary>
internal static class StampingProviderSeed
{
    public static StampingProvider[] Data =>
    [
        new StampingProvider
        {
            Id = StampingProvider.TouringenId,
            Slug = StampingProvider.TouringenSlug,
            Name = "Touringen",
            IsDataReady = true,
            WebsiteUri = new Uri("https://www.touringen.de/"),
            Description = "Touringen ist ein im Oktober 2022 von der Funke Mediengruppe in Kooperation mit der Thüringer Tourismus GmbH und regionalen Tourismusverbänden gestartetes System, das Wandererlebnisse mit einem Sammelanreiz verbindet. Nach einer Erweiterung im Juli 2023 umfasst das Netz 430 offizielle Stempelstellen an markanten Aussichtspunkten, Kulturdenkmälern und Naturhighlights in ganz Thüringen sowie im angrenzenden Frankenwald. Neben klassischen Stempel- und Tourenheften gibt es kindgerechte Varianten sowie ein mehrstufiges Abzeichensystem, bei dem Wanderer vom „Hobby Entdecker“ (ab 10 Stempeln) bis zum vollständigen „Touringen Entdecker“ (430 Stempel) mit Pins, Urkunden und einem Eintrag in die „Hall of Fame“ ausgezeichnet werden."
        },
        new StampingProvider
        {
            Id = StampingProvider.HarzerWandernadelId,
            Slug = StampingProvider.HarzerWandernadelSlug,
            Name = "Harzer Wandernadel",
            Abbreviation = "HWN",
            IsDataReady = false,
            WebsiteUri = new Uri("https://www.harzer-wandernadel.de/"),
            Description = "Die Harzer Wandernadel ist ein seit 2006 bestehendes Wanderstempelsystem im Harz mit 222 regulären Stempelstellen. Wandernde sammeln die Stempel in einem Wanderpass und können damit verschiedene Leistungsabzeichen bis zum Harzer Wanderkaiser erreichen."
        },
        new StampingProvider
        {
            Id = StampingProvider.MalerwegId,
            Slug = StampingProvider.MalerwegSlug,
            Name = "Malerweg",
            Abbreviation = "MW",
            IsDataReady = true,
            WebsiteUri = new Uri("https://www.saechsische-schweiz.de/malerweg"),
            Description = "Der Malerweg im Elbsandsteingebirge der Sächsischen Schweiz gehört zu den traditionsreichsten und beliebtesten Wanderwegen Deutschlands. Der offizielle Wanderpass umfasst 8 Stempelstellen entlang der Etappen."
        },
        new StampingProvider
        {
            Id = StampingProvider.SchluchtensteigId,
            Slug = StampingProvider.SchluchtensteigSlug,
            Name = "Schluchtensteig",
            Abbreviation = "SST",
            IsDataReady = true,
            WebsiteUri = new Uri("https://www.schluchtensteig.de/"),
            Description = "Der Schluchtensteig im Naturpark Südschwarzwald führt über 119 Kilometer in 6 Etappen von Stühlingen quer durch spektakuläre Schluchten bis nach Wehr. Entlang der Etappenorte laden Stempelstellen zum Eintragen in den Wanderpass ein."
        },
        new StampingProvider
        {
            Id = StampingProvider.HeidschnuckenwegId,
            Slug = StampingProvider.HeidschnuckenwegSlug,
            Name = "Heidschnuckenweg",
            Abbreviation = "HNW",
            IsDataReady = true,
            WebsiteUri = new Uri("https://www.heidschnuckenweg.de/"),
            Description = "Der Heidschnuckenweg verbindet auf über 220 Kilometern in 13 Etappen Hamburg-Fischbek durch die Lüneburger Heide mit der Residenzstadt Celle. Mit dem offiziellen Wanderpass werden gesammelte Stempel mit Heidschnucken-Wandernadeln belohnt."
        },
        new StampingProvider
        {
            Id = StampingProvider.HarzerKlosterwanderwegId,
            Slug = StampingProvider.HarzerKlosterwanderwegSlug,
            Name = "Harzer Klosterwanderweg",
            Abbreviation = "HKW",
            IsDataReady = true,
            WebsiteUri = new Uri("https://www.harzinfo.de/erlebnisse/harzer-kloester/harzer-klosterwanderweg"),
            Description = "Der Harzer Klosterwanderweg führt über rund 117 Kilometer entlang geschichtsträchtiger Klöster und Kirchen am Nordrand des Harzes von Goslar bis Halberstadt. 16 markante rote Stempelkästen der Harzer Wandernadel laden zum Sammeln im Begleitheft ein."
        },
        new StampingProvider
        {
            Id = StampingProvider.BliessteigId,
            Slug = StampingProvider.BliessteigSlug,
            Name = "Bliessteig",
            Abbreviation = "BS",
            IsDataReady = true,
            WebsiteUri = new Uri("https://www.saarpfalz-touristik.de/erlebnisse/wandern/wanderservice/stempelstationen"),
            Description = "Der rund 106 Kilometer lange Bliessteig führt in neun Etappen von Sarreguemines durch den Bliesgau bis nach Bexbach. An den Etappenorten stehen 10 feste Stempelstationen.",
            DataSourceUri = new Uri("https://www.saarpfalz-touristik.de/touren/bliessteig-c62caf7374"),
            DataSourceAttribution = "Saarpfalz-Touristik, Julia Serov; von TourEd aus den Etappenendpunkten abgeleitet",
            DataLicenseName = "Creative Commons Namensnennung 4.0 International (CC BY 4.0)",
            DataLicenseUri = new Uri("https://creativecommons.org/licenses/by/4.0/"),
            DataSourceRevision = "f6ccf2af-e2e7-4bd1-becc-4590f8e3456a:2026-09-03T02:08:17",
            DataSourceUpdatedAt = new DateTime(2026, 9, 3, 2, 8, 17, DateTimeKind.Utc)
        },
        new StampingProvider
        {
            Id = StampingProvider.KellerwaldsteigId,
            Slug = StampingProvider.KellerwaldsteigSlug,
            Name = "Kellerwaldsteig",
            Abbreviation = "KWS",
            IsDataReady = true,
            WebsiteUri = new Uri("https://www.naturpark-kellerwald-edersee.de/wandern/wanderpass-kellerwaldsteig"),
            Description = "Der 164 Kilometer lange Kellerwaldsteig führt durch den Naturpark Kellerwald-Edersee, am Edersee und am Nationalpark entlang. Zehn feste Wanderpass-Stationen verbinden Stanzmotive mit Geocaches; ein vollständiger Pass kann gegen eine Wandermünze eingetauscht werden.",
            DataSourceUri = new Uri("https://www.naturpark-kellerwald-edersee.de/wandern/wanderpass-kellerwaldsteig"),
            DataSourceAttribution = "Edersee Marketing GmbH; von TourEd als Punktliste aus den offiziellen Stationsdatensätzen übernommen",
            DataLicenseName = "Creative Commons Namensnennung - Weitergabe unter gleichen Bedingungen 4.0 International (CC BY-SA 4.0)",
            DataLicenseUri = new Uri("https://creativecommons.org/licenses/by-sa/4.0/"),
            DataSourceRevision = "destination.one:2025-10-13T11:40:00+02:00",
            DataSourceUpdatedAt = new DateTime(2025, 10, 13, 9, 40, 0, DateTimeKind.Utc)
        }
    ];
}
