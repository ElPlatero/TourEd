using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Api.Migrations
{
    /// <inheritdoc />
    public partial class AddBliessteigProvider : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "StampingProviders",
                columns: new[] { "Id", "Abbreviation", "DataImportedAt", "DataLicenseName", "DataLicenseUri", "DataSourceAttribution", "DataSourceRevision", "DataSourceUpdatedAt", "DataSourceUri", "Description", "IsAnonymousAccessAllowed", "Name", "Slug", "WebsiteUri" },
                values: new object[] { 7, "BS", new DateTime(2026, 9, 3, 18, 6, 12, 0, DateTimeKind.Utc), "Creative Commons Namensnennung 4.0 International (CC BY 4.0)", "https://creativecommons.org/licenses/by/4.0/", "Saarpfalz-Touristik, Julia Serov", "f6ccf2af-e2e7-4bd1-becc-4590f8e3456a:2026-09-03T02:08:17", new DateTime(2026, 9, 3, 2, 8, 17, 0, DateTimeKind.Utc), "https://www.saarpfalz-touristik.de/touren/bliessteig-c62caf7374", "Der rund 106 Kilometer lange Bliessteig führt in neun Etappen von Sarreguemines durch den Bliesgau bis nach Bexbach. An den Etappenorten stehen 10 feste Stempelstationen.", true, "Bliessteig", "bliessteig", "https://www.saarpfalz-touristik.de/erlebnisse/wandern/wanderservice/stempelstationen" });

            migrationBuilder.InsertData(
                table: "StampingSeries",
                columns: new[] { "Id", "ExpectedPointCount", "IsTemporary", "Name", "ProviderId", "Slug" },
                values: new object[] { 10, 10, false, "Standard", 7, "standard" });

            migrationBuilder.InsertData(
                table: "StampingPoints",
                columns: new[] { "Id", "Code", "ExternalId", "Latitude", "Longitude", "Name", "Number", "ProviderId", "SeriesId", "ValidFrom", "ValidUntil" },
                values: new object[,]
                {
                    { 5401, 1, "standard-1", 49.110405m, 7.072924m, "Sarreguemines Bahnhof", 1, 7, 10, null, null },
                    { 5402, 2, "standard-2", 49.160345m, 7.119782m, "Gräfinthal", 2, 7, 10, null, null },
                    { 5403, 3, "standard-3", 49.170907m, 7.170777m, "Bebelsheim", 3, 7, 10, null, null },
                    { 5404, 4, "standard-4", 49.237008m, 7.259220m, "Blieskastel", 4, 7, 10, null, null },
                    { 5405, 5, "standard-5", 49.285312m, 7.240993m, "Kirkel", 5, 7, 10, null, null },
                    { 5406, 6, "standard-6", 49.283315m, 7.315955m, "Schwarzenacker", 6, 7, 10, null, null },
                    { 5407, 7, "standard-7", 49.321074m, 7.344550m, "Homburg", 7, 7, 10, null, null },
                    { 5408, 8, "standard-8", 49.362197m, 7.312004m, "Jägersburg", 8, 7, 10, null, null },
                    { 5409, 9, "standard-9", 49.397474m, 7.266016m, "Höchen", 9, 7, 10, null, null },
                    { 5410, 10, "standard-10", 49.346269m, 7.254470m, "Kulturbahnhof Bexbach", 10, 7, 10, null, null }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "StampingPoints",
                keyColumn: "Id",
                keyValue: 5401);

            migrationBuilder.DeleteData(
                table: "StampingPoints",
                keyColumn: "Id",
                keyValue: 5402);

            migrationBuilder.DeleteData(
                table: "StampingPoints",
                keyColumn: "Id",
                keyValue: 5403);

            migrationBuilder.DeleteData(
                table: "StampingPoints",
                keyColumn: "Id",
                keyValue: 5404);

            migrationBuilder.DeleteData(
                table: "StampingPoints",
                keyColumn: "Id",
                keyValue: 5405);

            migrationBuilder.DeleteData(
                table: "StampingPoints",
                keyColumn: "Id",
                keyValue: 5406);

            migrationBuilder.DeleteData(
                table: "StampingPoints",
                keyColumn: "Id",
                keyValue: 5407);

            migrationBuilder.DeleteData(
                table: "StampingPoints",
                keyColumn: "Id",
                keyValue: 5408);

            migrationBuilder.DeleteData(
                table: "StampingPoints",
                keyColumn: "Id",
                keyValue: 5409);

            migrationBuilder.DeleteData(
                table: "StampingPoints",
                keyColumn: "Id",
                keyValue: 5410);

            migrationBuilder.DeleteData(
                table: "StampingSeries",
                keyColumn: "Id",
                keyValue: 10);

            migrationBuilder.DeleteData(
                table: "StampingProviders",
                keyColumn: "Id",
                keyValue: 7);
        }
    }
}
