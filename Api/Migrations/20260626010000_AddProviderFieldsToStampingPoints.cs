using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Migrations
{
    /// <inheritdoc />
    [Migration("20260626010000_AddProviderFieldsToStampingPoints")]
    public partial class AddProviderFieldsToStampingPoints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExternalId",
                table: "StampingPoints",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "ProviderId",
                table: "StampingPoints",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.Sql("UPDATE StampingPoints SET ExternalId = CAST(Id AS TEXT) WHERE ExternalId = ''");

            migrationBuilder.CreateIndex(
                name: "IX_StampingPoints_ProviderId_ExternalId",
                table: "StampingPoints",
                columns: new[] { "ProviderId", "ExternalId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StampingPoints_ProviderId_Number",
                table: "StampingPoints",
                columns: new[] { "ProviderId", "Number" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_StampingPoints_StampingProviders_ProviderId",
                table: "StampingPoints",
                column: "ProviderId",
                principalTable: "StampingProviders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_StampingPoints_StampingProviders_ProviderId",
                table: "StampingPoints");

            migrationBuilder.DropIndex(
                name: "IX_StampingPoints_ProviderId_ExternalId",
                table: "StampingPoints");

            migrationBuilder.DropIndex(
                name: "IX_StampingPoints_ProviderId_Number",
                table: "StampingPoints");

            migrationBuilder.DropColumn(
                name: "ExternalId",
                table: "StampingPoints");

            migrationBuilder.DropColumn(
                name: "ProviderId",
                table: "StampingPoints");
        }
    }
}
