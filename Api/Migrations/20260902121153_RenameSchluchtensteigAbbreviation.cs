using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Migrations
{
    /// <inheritdoc />
    public partial class RenameSchluchtensteigAbbreviation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "StampingProviders",
                keyColumn: "Id",
                keyValue: 4,
                column: "Abbreviation",
                value: "SST");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "StampingProviders",
                keyColumn: "Id",
                keyValue: 4,
                column: "Abbreviation",
                value: "SS");
        }
    }
}
