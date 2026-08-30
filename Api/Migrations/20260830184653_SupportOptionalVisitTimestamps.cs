using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Migrations
{
    /// <inheritdoc />
    public partial class SupportOptionalVisitTimestamps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserVisit_UserId",
                table: "UserVisit");

            migrationBuilder.AddColumn<bool>(
                name: "HasVisitedTime",
                table: "UserVisit",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql(
                "UPDATE UserVisit SET HasVisitedTime = 1 WHERE Visited IS NOT NULL;");

            migrationBuilder.CreateIndex(
                name: "IX_UserVisit_UserId_StampingPointId",
                table: "UserVisit",
                columns: new[] { "UserId", "StampingPointId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserVisit_UserId_StampingPointId",
                table: "UserVisit");

            migrationBuilder.DropColumn(
                name: "HasVisitedTime",
                table: "UserVisit");

            migrationBuilder.CreateIndex(
                name: "IX_UserVisit_UserId",
                table: "UserVisit",
                column: "UserId");
        }
    }
}
