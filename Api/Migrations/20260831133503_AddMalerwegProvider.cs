using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Api.Migrations
{
    /// <inheritdoc />
    public partial class AddMalerwegProvider : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "StampingProviders",
                columns: new[] { "Id", "Abbreviation", "DataImportedAt", "DataLicenseName", "DataLicenseUri", "DataSourceAttribution", "DataSourceRevision", "DataSourceUpdatedAt", "DataSourceUri", "Description", "IsAnonymousAccessAllowed", "Name", "Slug", "WebsiteUri" },
                values: new object[] { 3, "MW", null, null, null, null, null, null, null, "Der Malerweg im Elbsandsteingebirge der Sächsischen Schweiz gehört zu den traditionsreichsten und beliebtesten Wanderwegen Deutschlands. Der offizielle Wanderpass umfasst 8 Stempelstellen entlang der Etappen.", true, "Malerweg", "malerweg", "https://www.saechsische-schweiz.de/malerweg" });

            migrationBuilder.InsertData(
                table: "StampingSeries",
                columns: new[] { "Id", "ExpectedPointCount", "IsTemporary", "Name", "ProviderId", "Slug" },
                values: new object[] { 6, 8, false, "Standard", 3, "standard" });

            migrationBuilder.InsertData(
                table: "StampingPoints",
                columns: new[] { "Id", "Code", "ExternalId", "Latitude", "Longitude", "Name", "Number", "ProviderId", "SeriesId", "ValidFrom", "ValidUntil" },
                values: new object[,]
                {
                    { 5001, 1, "standard-1", 50.9982441m, 13.9538612m, "Liebethal", 1, 3, 6, null, null },
                    { 5002, 2, "standard-2", 50.9622998m, 14.0729352m, "Stadt Wehlen", 2, 3, 6, null, null },
                    { 5003, 3, "standard-3", 50.9788094m, 14.1105942m, "Hohnstein", 3, 3, 6, null, null },
                    { 5004, 4, "standard-4", 50.9702213m, 14.1206126m, "Brand", 4, 3, 6, null, null },
                    { 5005, 5, "standard-5", 50.9416556m, 14.1843440m, "Neumannmühle", 5, 3, 6, null, null },
                    { 5006, 6, "standard-6", 50.9080517m, 14.2562470m, "Großer Zschirnstein", 6, 3, 6, null, null },
                    { 5007, 7, "standard-7", 50.8872242m, 14.1206126m, "Gohrisch", 7, 3, 6, null, null },
                    { 5008, 8, "standard-8", 50.9255018m, 14.0734005m, "Rauenstein", 8, 3, 6, null, null }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "StampingPoints",
                keyColumn: "Id",
                keyValue: 5001);

            migrationBuilder.DeleteData(
                table: "StampingPoints",
                keyColumn: "Id",
                keyValue: 5002);

            migrationBuilder.DeleteData(
                table: "StampingPoints",
                keyColumn: "Id",
                keyValue: 5003);

            migrationBuilder.DeleteData(
                table: "StampingPoints",
                keyColumn: "Id",
                keyValue: 5004);

            migrationBuilder.DeleteData(
                table: "StampingPoints",
                keyColumn: "Id",
                keyValue: 5005);

            migrationBuilder.DeleteData(
                table: "StampingPoints",
                keyColumn: "Id",
                keyValue: 5006);

            migrationBuilder.DeleteData(
                table: "StampingPoints",
                keyColumn: "Id",
                keyValue: 5007);

            migrationBuilder.DeleteData(
                table: "StampingPoints",
                keyColumn: "Id",
                keyValue: 5008);

            migrationBuilder.DeleteData(
                table: "StampingSeries",
                keyColumn: "Id",
                keyValue: 6);

            migrationBuilder.DeleteData(
                table: "StampingProviders",
                keyColumn: "Id",
                keyValue: 3);
        }
    }
}
