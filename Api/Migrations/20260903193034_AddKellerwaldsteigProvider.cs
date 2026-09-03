using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Api.Migrations
{
    /// <inheritdoc />
    public partial class AddKellerwaldsteigProvider : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "StampingProviders",
                columns: new[] { "Id", "Abbreviation", "DataImportedAt", "DataLicenseName", "DataLicenseUri", "DataSourceAttribution", "DataSourceRevision", "DataSourceUpdatedAt", "DataSourceUri", "Description", "IsAnonymousAccessAllowed", "Name", "Slug", "WebsiteUri" },
                values: new object[] { 8, "KWS", null, "Creative Commons Namensnennung - Weitergabe unter gleichen Bedingungen 4.0 International (CC BY-SA 4.0)", "https://creativecommons.org/licenses/by-sa/4.0/", "Edersee Marketing GmbH; von TourEd als Punktliste aus den offiziellen Stationsdatensätzen übernommen", "destination.one:2025-10-13T11:40:00+02:00", new DateTime(2025, 10, 13, 9, 40, 0, 0, DateTimeKind.Utc), "https://www.naturpark-kellerwald-edersee.de/wandern/wanderpass-kellerwaldsteig", "Der 164 Kilometer lange Kellerwaldsteig führt durch den Naturpark Kellerwald-Edersee, am Edersee und am Nationalpark entlang. Zehn feste Wanderpass-Stationen verbinden Stanzmotive mit Geocaches; ein vollständiger Pass kann gegen eine Wandermünze eingetauscht werden.", true, "Kellerwaldsteig", "kellerwaldsteig", "https://www.naturpark-kellerwald-edersee.de/wandern/wanderpass-kellerwaldsteig" });

            migrationBuilder.InsertData(
                table: "StampingSeries",
                columns: new[] { "Id", "ExpectedPointCount", "IsTemporary", "Name", "ProviderId", "Slug" },
                values: new object[] { 11, 10, false, "Standard", 8, "standard" });

            migrationBuilder.InsertData(
                table: "StampingPoints",
                columns: new[] { "Id", "Code", "ExternalId", "Latitude", "Longitude", "Name", "Number", "ProviderId", "SeriesId", "ValidFrom", "ValidUntil" },
                values: new object[,]
                {
                    { 5501, 1, "p_100217885", 51.2018317m, 8.9601660m, "Asel", null, 8, 11, null, null },
                    { 5502, 2, "p_100217884", 51.1553140m, 8.8274825m, "Reckenberg", null, 8, 11, null, null },
                    { 5503, 3, "p_100217882", 51.1287343m, 8.8836265m, "Keseburg", null, 8, 11, null, null },
                    { 5504, 4, "p_100217881", 51.0548499m, 8.9657450m, "Löhlbach", null, 8, 11, null, null },
                    { 5505, 5, "p_100217879", 50.9908330m, 9.0304170m, "Jeust", null, 8, 11, null, null },
                    { 5506, 6, "p_100217860", 51.2061205m, 9.0621328m, "Waldeck", null, 8, 11, null, null },
                    { 5507, 7, "p_100217873", 51.1247179m, 9.0554330m, "Kesselbach", null, 8, 11, null, null },
                    { 5508, 8, "p_100217876", 51.0703515m, 9.0656304m, "Armsfeld", null, 8, 11, null, null },
                    { 5509, 9, "p_100217877", 51.0658917m, 9.1547066m, "Bad Zwesten", null, 8, 11, null, null },
                    { 5510, 10, "p_100217878", 51.0156851m, 9.0840197m, "Wüstegarten", null, 8, 11, null, null }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "StampingPoints",
                keyColumn: "Id",
                keyValue: 5501);

            migrationBuilder.DeleteData(
                table: "StampingPoints",
                keyColumn: "Id",
                keyValue: 5502);

            migrationBuilder.DeleteData(
                table: "StampingPoints",
                keyColumn: "Id",
                keyValue: 5503);

            migrationBuilder.DeleteData(
                table: "StampingPoints",
                keyColumn: "Id",
                keyValue: 5504);

            migrationBuilder.DeleteData(
                table: "StampingPoints",
                keyColumn: "Id",
                keyValue: 5505);

            migrationBuilder.DeleteData(
                table: "StampingPoints",
                keyColumn: "Id",
                keyValue: 5506);

            migrationBuilder.DeleteData(
                table: "StampingPoints",
                keyColumn: "Id",
                keyValue: 5507);

            migrationBuilder.DeleteData(
                table: "StampingPoints",
                keyColumn: "Id",
                keyValue: 5508);

            migrationBuilder.DeleteData(
                table: "StampingPoints",
                keyColumn: "Id",
                keyValue: 5509);

            migrationBuilder.DeleteData(
                table: "StampingPoints",
                keyColumn: "Id",
                keyValue: 5510);

            migrationBuilder.DeleteData(
                table: "StampingSeries",
                keyColumn: "Id",
                keyValue: 11);

            migrationBuilder.DeleteData(
                table: "StampingProviders",
                keyColumn: "Id",
                keyValue: 8);
        }
    }
}
