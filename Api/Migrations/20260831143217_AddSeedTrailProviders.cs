using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Api.Migrations
{
    /// <inheritdoc />
    public partial class AddSeedTrailProviders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "StampingProviders",
                columns: new[] { "Id", "Abbreviation", "DataImportedAt", "DataLicenseName", "DataLicenseUri", "DataSourceAttribution", "DataSourceRevision", "DataSourceUpdatedAt", "DataSourceUri", "Description", "IsAnonymousAccessAllowed", "Name", "Slug", "WebsiteUri" },
                values: new object[,]
                {
                    { 4, "SS", null, null, null, null, null, null, null, "Der Schluchtensteig im Naturpark Südschwarzwald führt über 119 Kilometer in 6 Etappen von Stühlingen quer durch spektakuläre Schluchten bis nach Wehr. Entlang der Etappenorte laden Stempelstellen zum Eintragen in den Wanderpass ein.", true, "Schluchtensteig", "schluchtensteig", "https://www.schluchtensteig.de/" },
                    { 5, "HNW", null, null, null, null, null, null, null, "Der Heidschnuckenweg verbindet auf über 220 Kilometern in 13 Etappen Hamburg-Fischbek durch die Lüneburger Heide mit der Residenzstadt Celle. Mit dem offiziellen Wanderpass werden gesammelte Stempel mit Heidschnucken-Wandernadeln belohnt.", true, "Heidschnuckenweg", "heidschnuckenweg", "https://www.heidschnuckenweg.de/" },
                    { 6, "HKW", null, null, null, null, null, null, null, "Der Harzer Klosterwanderweg führt über rund 117 Kilometer entlang geschichtsträchtiger Klöster und Kirchen am Nordrand des Harzes von Goslar bis Halberstadt. 16 markante rote Stempelkästen der Harzer Wandernadel laden zum Sammeln im Begleitheft ein.", true, "Harzer Klosterwanderweg", "harzer-klosterwanderweg", "https://www.harzinfo.de/erlebnisse/harzer-kloester/harzer-klosterwanderweg" }
                });

            migrationBuilder.InsertData(
                table: "StampingSeries",
                columns: new[] { "Id", "ExpectedPointCount", "IsTemporary", "Name", "ProviderId", "Slug" },
                values: new object[,]
                {
                    { 7, 6, false, "Standard", 4, "standard" },
                    { 8, 13, false, "Standard", 5, "standard" },
                    { 9, 16, false, "Standard", 6, "standard" }
                });

            migrationBuilder.InsertData(
                table: "StampingPoints",
                columns: new[] { "Id", "Code", "ExternalId", "Latitude", "Longitude", "Name", "Number", "ProviderId", "SeriesId", "ValidFrom", "ValidUntil" },
                values: new object[,]
                {
                    { 5101, 1, "standard-1", 47.7448200m, 8.4462100m, "Stühlingen", 1, 4, 7, null, null },
                    { 5102, 2, "standard-2", 47.8398100m, 8.5342200m, "Blumberg", 2, 4, 7, null, null },
                    { 5103, 3, "standard-3", 47.8443100m, 8.3188500m, "Schattenmühle", 3, 4, 7, null, null },
                    { 5104, 4, "standard-4", 47.8182400m, 8.1637100m, "Oberfischbach (Schluchsee)", 4, 4, 7, null, null },
                    { 5105, 5, "standard-5", 47.7601200m, 8.1294500m, "St. Blasien", 5, 4, 7, null, null },
                    { 5106, 6, "standard-6", 47.7397100m, 8.0002100m, "Todtmoos", 6, 4, 7, null, null },
                    { 5201, 1, "standard-1", 53.4475100m, 9.8322100m, "Fischbek (Hamburg)", 1, 5, 8, null, null },
                    { 5202, 2, "standard-2", 53.3275200m, 9.8708100m, "Buchholz in der Nordheide", 2, 5, 8, null, null },
                    { 5203, 3, "standard-3", 53.2458100m, 9.8236200m, "Handeloh", 3, 5, 8, null, null },
                    { 5204, 4, "standard-4", 53.1956100m, 9.9753100m, "Undeloh", 4, 5, 8, null, null },
                    { 5205, 5, "standard-5", 53.1511200m, 9.9103200m, "Niederhaverbeck", 5, 5, 8, null, null },
                    { 5206, 6, "standard-6", 53.0833100m, 9.9986100m, "Bispingen", 6, 5, 8, null, null },
                    { 5207, 7, "standard-7", 52.9869200m, 9.8389100m, "Soltau", 7, 5, 8, null, null },
                    { 5208, 8, "standard-8", 52.9189100m, 9.9786200m, "Wietzendorf", 8, 5, 8, null, null },
                    { 5209, 9, "standard-9", 52.8753200m, 10.1167100m, "Müden (Örtze)", 9, 5, 8, null, null },
                    { 5210, 10, "standard-10", 52.9011100m, 10.1742100m, "Faßberg", 10, 5, 8, null, null },
                    { 5211, 11, "standard-11", 52.8317200m, 10.0911200m, "Hermannsburg", 11, 5, 8, null, null },
                    { 5212, 12, "standard-12", 52.7344100m, 10.2444100m, "Eschede", 12, 5, 8, null, null },
                    { 5213, 13, "standard-13", 52.6247200m, 10.0811200m, "Celle (Schloss)", 13, 5, 8, null, null },
                    { 5301, 1, "standard-1", 51.9082100m, 10.4241200m, "Neuwerkkirche Goslar", 1, 6, 9, null, null },
                    { 5302, 2, "standard-2", 51.9367100m, 10.4358100m, "Kloster Grauhof", 2, 6, 9, null, null },
                    { 5303, 3, "standard-3", 51.9572200m, 10.5398200m, "Kloster Wöltingerode", 3, 6, 9, null, null },
                    { 5304, 4, "standard-4", 51.8601100m, 10.6791100m, "Kloster Ilsenburg", 4, 6, 9, null, null },
                    { 5305, 5, "standard-5", 51.8561200m, 10.7144200m, "Kloster Drübeck", 5, 6, 9, null, null },
                    { 5306, 6, "standard-6", 51.8488100m, 10.7303100m, "St. Laurentius Darlingerode", 6, 6, 9, null, null },
                    { 5307, 7, "standard-7", 51.8262200m, 10.7551200m, "Kloster Himmelpforte (Wernigerode)", 7, 6, 9, null, null },
                    { 5308, 8, "standard-8", 51.8061100m, 10.9142100m, "Kloster Michaelstein (Blankenburg)", 8, 6, 9, null, null },
                    { 5309, 9, "standard-9", 51.7891200m, 10.9575200m, "Bergkirche St. Bartholomäus (Blankenburg)", 9, 6, 9, null, null },
                    { 5310, 10, "standard-10", 51.7547100m, 11.0506100m, "Kloster Wendhusen (Thale)", 10, 6, 9, null, null },
                    { 5311, 11, "standard-11", 51.7244200m, 11.1364200m, "Stiftskirche St. Cyriakus Gernrode", 11, 6, 9, null, null },
                    { 5312, 12, "standard-12", 51.7871100m, 11.1398100m, "Klosterkirche St. Marien (Quedlinburg)", 12, 6, 9, null, null },
                    { 5313, 13, "standard-13", 51.7858200m, 11.1369200m, "Stiftskirche St. Servatii (Quedlinburg)", 13, 6, 9, null, null },
                    { 5314, 14, "standard-14", 51.8722100m, 11.0421100m, "Spiegelsberge (Halberstadt)", 14, 6, 9, null, null },
                    { 5315, 15, "standard-15", 51.8958200m, 11.0483200m, "Dom und Domschatz Halberstadt", 15, 6, 9, null, null },
                    { 5316, 16, "standard-16", 51.8988100m, 11.0664100m, "Kloster St. Burchardi (Halberstadt)", 16, 6, 9, null, null }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "StampingPoints",
                keyColumn: "Id",
                keyValue: 5101);

            migrationBuilder.DeleteData(
                table: "StampingPoints",
                keyColumn: "Id",
                keyValue: 5102);

            migrationBuilder.DeleteData(
                table: "StampingPoints",
                keyColumn: "Id",
                keyValue: 5103);

            migrationBuilder.DeleteData(
                table: "StampingPoints",
                keyColumn: "Id",
                keyValue: 5104);

            migrationBuilder.DeleteData(
                table: "StampingPoints",
                keyColumn: "Id",
                keyValue: 5105);

            migrationBuilder.DeleteData(
                table: "StampingPoints",
                keyColumn: "Id",
                keyValue: 5106);

            migrationBuilder.DeleteData(
                table: "StampingPoints",
                keyColumn: "Id",
                keyValue: 5201);

            migrationBuilder.DeleteData(
                table: "StampingPoints",
                keyColumn: "Id",
                keyValue: 5202);

            migrationBuilder.DeleteData(
                table: "StampingPoints",
                keyColumn: "Id",
                keyValue: 5203);

            migrationBuilder.DeleteData(
                table: "StampingPoints",
                keyColumn: "Id",
                keyValue: 5204);

            migrationBuilder.DeleteData(
                table: "StampingPoints",
                keyColumn: "Id",
                keyValue: 5205);

            migrationBuilder.DeleteData(
                table: "StampingPoints",
                keyColumn: "Id",
                keyValue: 5206);

            migrationBuilder.DeleteData(
                table: "StampingPoints",
                keyColumn: "Id",
                keyValue: 5207);

            migrationBuilder.DeleteData(
                table: "StampingPoints",
                keyColumn: "Id",
                keyValue: 5208);

            migrationBuilder.DeleteData(
                table: "StampingPoints",
                keyColumn: "Id",
                keyValue: 5209);

            migrationBuilder.DeleteData(
                table: "StampingPoints",
                keyColumn: "Id",
                keyValue: 5210);

            migrationBuilder.DeleteData(
                table: "StampingPoints",
                keyColumn: "Id",
                keyValue: 5211);

            migrationBuilder.DeleteData(
                table: "StampingPoints",
                keyColumn: "Id",
                keyValue: 5212);

            migrationBuilder.DeleteData(
                table: "StampingPoints",
                keyColumn: "Id",
                keyValue: 5213);

            migrationBuilder.DeleteData(
                table: "StampingPoints",
                keyColumn: "Id",
                keyValue: 5301);

            migrationBuilder.DeleteData(
                table: "StampingPoints",
                keyColumn: "Id",
                keyValue: 5302);

            migrationBuilder.DeleteData(
                table: "StampingPoints",
                keyColumn: "Id",
                keyValue: 5303);

            migrationBuilder.DeleteData(
                table: "StampingPoints",
                keyColumn: "Id",
                keyValue: 5304);

            migrationBuilder.DeleteData(
                table: "StampingPoints",
                keyColumn: "Id",
                keyValue: 5305);

            migrationBuilder.DeleteData(
                table: "StampingPoints",
                keyColumn: "Id",
                keyValue: 5306);

            migrationBuilder.DeleteData(
                table: "StampingPoints",
                keyColumn: "Id",
                keyValue: 5307);

            migrationBuilder.DeleteData(
                table: "StampingPoints",
                keyColumn: "Id",
                keyValue: 5308);

            migrationBuilder.DeleteData(
                table: "StampingPoints",
                keyColumn: "Id",
                keyValue: 5309);

            migrationBuilder.DeleteData(
                table: "StampingPoints",
                keyColumn: "Id",
                keyValue: 5310);

            migrationBuilder.DeleteData(
                table: "StampingPoints",
                keyColumn: "Id",
                keyValue: 5311);

            migrationBuilder.DeleteData(
                table: "StampingPoints",
                keyColumn: "Id",
                keyValue: 5312);

            migrationBuilder.DeleteData(
                table: "StampingPoints",
                keyColumn: "Id",
                keyValue: 5313);

            migrationBuilder.DeleteData(
                table: "StampingPoints",
                keyColumn: "Id",
                keyValue: 5314);

            migrationBuilder.DeleteData(
                table: "StampingPoints",
                keyColumn: "Id",
                keyValue: 5315);

            migrationBuilder.DeleteData(
                table: "StampingPoints",
                keyColumn: "Id",
                keyValue: 5316);

            migrationBuilder.DeleteData(
                table: "StampingSeries",
                keyColumn: "Id",
                keyValue: 7);

            migrationBuilder.DeleteData(
                table: "StampingSeries",
                keyColumn: "Id",
                keyValue: 8);

            migrationBuilder.DeleteData(
                table: "StampingSeries",
                keyColumn: "Id",
                keyValue: 9);

            migrationBuilder.DeleteData(
                table: "StampingProviders",
                keyColumn: "Id",
                keyValue: 4);

            migrationBuilder.DeleteData(
                table: "StampingProviders",
                keyColumn: "Id",
                keyValue: 5);

            migrationBuilder.DeleteData(
                table: "StampingProviders",
                keyColumn: "Id",
                keyValue: 6);
        }
    }
}
