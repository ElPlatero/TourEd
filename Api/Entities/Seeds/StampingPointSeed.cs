namespace Api.Entities.Seeds;

/// <summary>Seeded stamping points of the trail providers without network import. Anonymous objects are used because <see cref="StampingPoint"/> is a positional record with ignored members.</summary>
internal static class StampingPointSeed
{
    public static object[] Data =>
    [
        new
        {
            Id = 5001,
            Name = "Liebethal",
            Longitude = 13.9538612m,
            Latitude = 50.9982441m,
            Number = (int?)1,
            Code = 1,
            ProviderId = StampingProvider.MalerwegId,
            SeriesId = StampingSeries.MalerwegStandardId,
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
            SeriesId = StampingSeries.MalerwegStandardId,
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
            SeriesId = StampingSeries.MalerwegStandardId,
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
            SeriesId = StampingSeries.MalerwegStandardId,
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
            SeriesId = StampingSeries.MalerwegStandardId,
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
            SeriesId = StampingSeries.MalerwegStandardId,
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
            SeriesId = StampingSeries.MalerwegStandardId,
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
            SeriesId = StampingSeries.MalerwegStandardId,
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
            SeriesId = StampingSeries.SchluchtensteigStandardId,
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
            SeriesId = StampingSeries.SchluchtensteigStandardId,
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
            SeriesId = StampingSeries.SchluchtensteigStandardId,
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
            SeriesId = StampingSeries.SchluchtensteigStandardId,
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
            SeriesId = StampingSeries.SchluchtensteigStandardId,
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
            SeriesId = StampingSeries.SchluchtensteigStandardId,
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
            SeriesId = StampingSeries.HeidschnuckenwegStandardId,
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
            SeriesId = StampingSeries.HeidschnuckenwegStandardId,
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
            SeriesId = StampingSeries.HeidschnuckenwegStandardId,
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
            SeriesId = StampingSeries.HeidschnuckenwegStandardId,
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
            SeriesId = StampingSeries.HeidschnuckenwegStandardId,
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
            SeriesId = StampingSeries.HeidschnuckenwegStandardId,
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
            SeriesId = StampingSeries.HeidschnuckenwegStandardId,
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
            SeriesId = StampingSeries.HeidschnuckenwegStandardId,
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
            SeriesId = StampingSeries.HeidschnuckenwegStandardId,
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
            SeriesId = StampingSeries.HeidschnuckenwegStandardId,
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
            SeriesId = StampingSeries.HeidschnuckenwegStandardId,
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
            SeriesId = StampingSeries.HeidschnuckenwegStandardId,
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
            SeriesId = StampingSeries.HeidschnuckenwegStandardId,
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
            SeriesId = StampingSeries.HarzerKlosterwanderwegStandardId,
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
            SeriesId = StampingSeries.HarzerKlosterwanderwegStandardId,
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
            SeriesId = StampingSeries.HarzerKlosterwanderwegStandardId,
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
            SeriesId = StampingSeries.HarzerKlosterwanderwegStandardId,
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
            SeriesId = StampingSeries.HarzerKlosterwanderwegStandardId,
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
            SeriesId = StampingSeries.HarzerKlosterwanderwegStandardId,
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
            SeriesId = StampingSeries.HarzerKlosterwanderwegStandardId,
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
            SeriesId = StampingSeries.HarzerKlosterwanderwegStandardId,
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
            SeriesId = StampingSeries.HarzerKlosterwanderwegStandardId,
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
            SeriesId = StampingSeries.HarzerKlosterwanderwegStandardId,
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
            SeriesId = StampingSeries.HarzerKlosterwanderwegStandardId,
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
            SeriesId = StampingSeries.HarzerKlosterwanderwegStandardId,
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
            SeriesId = StampingSeries.HarzerKlosterwanderwegStandardId,
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
            SeriesId = StampingSeries.HarzerKlosterwanderwegStandardId,
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
            SeriesId = StampingSeries.HarzerKlosterwanderwegStandardId,
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
            SeriesId = StampingSeries.HarzerKlosterwanderwegStandardId,
            ExternalId = "standard-16"
        },
        new
        {
            Id = 5401,
            Name = "Sarreguemines Bahnhof",
            Longitude = 7.072924m,
            Latitude = 49.110405m,
            Number = (int?)1,
            Code = 1,
            ProviderId = StampingProvider.BliessteigId,
            SeriesId = StampingSeries.BliessteigStandardId,
            ExternalId = "standard-1"
        },
        new
        {
            Id = 5402,
            Name = "Gräfinthal",
            Longitude = 7.119782m,
            Latitude = 49.160345m,
            Number = (int?)2,
            Code = 2,
            ProviderId = StampingProvider.BliessteigId,
            SeriesId = StampingSeries.BliessteigStandardId,
            ExternalId = "standard-2"
        },
        new
        {
            Id = 5403,
            Name = "Bebelsheim",
            Longitude = 7.170777m,
            Latitude = 49.170907m,
            Number = (int?)3,
            Code = 3,
            ProviderId = StampingProvider.BliessteigId,
            SeriesId = StampingSeries.BliessteigStandardId,
            ExternalId = "standard-3"
        },
        new
        {
            Id = 5404,
            Name = "Blieskastel",
            Longitude = 7.259220m,
            Latitude = 49.237008m,
            Number = (int?)4,
            Code = 4,
            ProviderId = StampingProvider.BliessteigId,
            SeriesId = StampingSeries.BliessteigStandardId,
            ExternalId = "standard-4"
        },
        new
        {
            Id = 5405,
            Name = "Kirkel",
            Longitude = 7.240993m,
            Latitude = 49.285312m,
            Number = (int?)5,
            Code = 5,
            ProviderId = StampingProvider.BliessteigId,
            SeriesId = StampingSeries.BliessteigStandardId,
            ExternalId = "standard-5"
        },
        new
        {
            Id = 5406,
            Name = "Schwarzenacker",
            Longitude = 7.315955m,
            Latitude = 49.283315m,
            Number = (int?)6,
            Code = 6,
            ProviderId = StampingProvider.BliessteigId,
            SeriesId = StampingSeries.BliessteigStandardId,
            ExternalId = "standard-6"
        },
        new
        {
            Id = 5407,
            Name = "Homburg",
            Longitude = 7.344550m,
            Latitude = 49.321074m,
            Number = (int?)7,
            Code = 7,
            ProviderId = StampingProvider.BliessteigId,
            SeriesId = StampingSeries.BliessteigStandardId,
            ExternalId = "standard-7"
        },
        new
        {
            Id = 5408,
            Name = "Jägersburg",
            Longitude = 7.312004m,
            Latitude = 49.362197m,
            Number = (int?)8,
            Code = 8,
            ProviderId = StampingProvider.BliessteigId,
            SeriesId = StampingSeries.BliessteigStandardId,
            ExternalId = "standard-8"
        },
        new
        {
            Id = 5409,
            Name = "Höchen",
            Longitude = 7.266016m,
            Latitude = 49.397474m,
            Number = (int?)9,
            Code = 9,
            ProviderId = StampingProvider.BliessteigId,
            SeriesId = StampingSeries.BliessteigStandardId,
            ExternalId = "standard-9"
        },
        new
        {
            Id = 5410,
            Name = "Kulturbahnhof Bexbach",
            Longitude = 7.254470m,
            Latitude = 49.346269m,
            Number = (int?)10,
            Code = 10,
            ProviderId = StampingProvider.BliessteigId,
            SeriesId = StampingSeries.BliessteigStandardId,
            ExternalId = "standard-10"
        },
        new
        {
            Id = 5501,
            Name = "Asel",
            Longitude = 8.9601660m,
            Latitude = 51.2018317m,
            Number = (int?)null,
            Code = 1,
            ProviderId = StampingProvider.KellerwaldsteigId,
            SeriesId = StampingSeries.KellerwaldsteigStandardId,
            ExternalId = "p_100217885"
        },
        new
        {
            Id = 5502,
            Name = "Reckenberg",
            Longitude = 8.8274825m,
            Latitude = 51.1553140m,
            Number = (int?)null,
            Code = 2,
            ProviderId = StampingProvider.KellerwaldsteigId,
            SeriesId = StampingSeries.KellerwaldsteigStandardId,
            ExternalId = "p_100217884"
        },
        new
        {
            Id = 5503,
            Name = "Keseburg",
            Longitude = 8.8836265m,
            Latitude = 51.1287343m,
            Number = (int?)null,
            Code = 3,
            ProviderId = StampingProvider.KellerwaldsteigId,
            SeriesId = StampingSeries.KellerwaldsteigStandardId,
            ExternalId = "p_100217882"
        },
        new
        {
            Id = 5504,
            Name = "Löhlbach",
            Longitude = 8.9657450m,
            Latitude = 51.0548499m,
            Number = (int?)null,
            Code = 4,
            ProviderId = StampingProvider.KellerwaldsteigId,
            SeriesId = StampingSeries.KellerwaldsteigStandardId,
            ExternalId = "p_100217881"
        },
        new
        {
            Id = 5505,
            Name = "Jeust",
            Longitude = 9.0304170m,
            Latitude = 50.9908330m,
            Number = (int?)null,
            Code = 5,
            ProviderId = StampingProvider.KellerwaldsteigId,
            SeriesId = StampingSeries.KellerwaldsteigStandardId,
            ExternalId = "p_100217879"
        },
        new
        {
            Id = 5506,
            Name = "Waldeck",
            Longitude = 9.0621328m,
            Latitude = 51.2061205m,
            Number = (int?)null,
            Code = 6,
            ProviderId = StampingProvider.KellerwaldsteigId,
            SeriesId = StampingSeries.KellerwaldsteigStandardId,
            ExternalId = "p_100217860"
        },
        new
        {
            Id = 5507,
            Name = "Kesselbach",
            Longitude = 9.0554330m,
            Latitude = 51.1247179m,
            Number = (int?)null,
            Code = 7,
            ProviderId = StampingProvider.KellerwaldsteigId,
            SeriesId = StampingSeries.KellerwaldsteigStandardId,
            ExternalId = "p_100217873"
        },
        new
        {
            Id = 5508,
            Name = "Armsfeld",
            Longitude = 9.0656304m,
            Latitude = 51.0703515m,
            Number = (int?)null,
            Code = 8,
            ProviderId = StampingProvider.KellerwaldsteigId,
            SeriesId = StampingSeries.KellerwaldsteigStandardId,
            ExternalId = "p_100217876"
        },
        new
        {
            Id = 5509,
            Name = "Bad Zwesten",
            Longitude = 9.1547066m,
            Latitude = 51.0658917m,
            Number = (int?)null,
            Code = 9,
            ProviderId = StampingProvider.KellerwaldsteigId,
            SeriesId = StampingSeries.KellerwaldsteigStandardId,
            ExternalId = "p_100217877"
        },
        new
        {
            Id = 5510,
            Name = "Wüstegarten",
            Longitude = 9.0840197m,
            Latitude = 51.0156851m,
            Number = (int?)null,
            Code = 10,
            ProviderId = StampingProvider.KellerwaldsteigId,
            SeriesId = StampingSeries.KellerwaldsteigStandardId,
            ExternalId = "p_100217878"
        }
    ];
}
