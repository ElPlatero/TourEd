using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Migrations
{
    /// <inheritdoc />
    public partial class AddHarzerWandernadelProvider : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Abbreviation",
                table: "StampingProviders",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsAnonymousAccessAllowed",
                table: "StampingProviders",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.UpdateData(
                table: "StampingProviders",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "Abbreviation", "IsAnonymousAccessAllowed" },
                values: new object[] { null, true });

            migrationBuilder.InsertData(
                table: "StampingProviders",
                columns: new[] { "Id", "Abbreviation", "Description", "IsAnonymousAccessAllowed", "Name", "Slug", "WebsiteUri" },
                values: new object[] { 2, "HWN", "Die Harzer Wandernadel ist ein seit 2006 bestehendes Wanderstempelsystem im Harz mit 222 regulären Stempelstellen. Wandernde sammeln die Stempel in einem Wanderpass und können damit verschiedene Leistungsabzeichen bis zum Harzer Wanderkaiser erreichen.", false, "Harzer Wandernadel", "harzer-wandernadel", "https://www.harzer-wandernadel.de/" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "StampingProviders",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DropColumn(
                name: "Abbreviation",
                table: "StampingProviders");

            migrationBuilder.DropColumn(
                name: "IsAnonymousAccessAllowed",
                table: "StampingProviders");
        }
    }
}
