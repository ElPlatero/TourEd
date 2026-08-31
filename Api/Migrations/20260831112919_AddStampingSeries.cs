using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Api.Migrations
{
    /// <inheritdoc />
    public partial class AddStampingSeries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StampingPoints_ProviderId_Number",
                table: "StampingPoints");

            migrationBuilder.AlterColumn<int>(
                name: "Number",
                table: "StampingPoints",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AddColumn<int>(
                name: "SeriesId",
                table: "StampingPoints",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateOnly>(
                name: "ValidFrom",
                table: "StampingPoints",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "ValidUntil",
                table: "StampingPoints",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "StampingSeries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProviderId = table.Column<int>(type: "INTEGER", nullable: false),
                    Slug = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    IsTemporary = table.Column<bool>(type: "INTEGER", nullable: false),
                    ExpectedPointCount = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StampingSeries", x => x.Id);
                    table.UniqueConstraint("AK_StampingSeries_Id_ProviderId", x => new { x.Id, x.ProviderId });
                    table.ForeignKey(
                        name: "FK_StampingSeries_StampingProviders_ProviderId",
                        column: x => x.ProviderId,
                        principalTable: "StampingProviders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "StampingSeries",
                columns: new[] { "Id", "ExpectedPointCount", "IsTemporary", "Name", "ProviderId", "Slug" },
                values: new object[,]
                {
                    { 1, 430, false, "Standard", 1, "standard" },
                    { 2, 8, false, "Naturschätze", 1, "naturschaetze" },
                    { 3, 13, false, "Familienwanderwege Rhön", 1, "familienwanderwege-rhoen" },
                    { 4, null, true, "Sonderstempel", 1, "sonderstempel" },
                    { 5, 222, false, "Standard", 2, "standard" }
                });

            migrationBuilder.Sql(
                "INSERT INTO StampingSeries (ProviderId, Slug, Name, IsTemporary) " +
                "SELECT Id, 'standard', 'Standard', 0 FROM StampingProviders WHERE Id NOT IN (1, 2); " +
                "UPDATE StampingPoints SET SeriesId = (" +
                "SELECT Id FROM StampingSeries WHERE StampingSeries.ProviderId = StampingPoints.ProviderId AND Slug = 'standard');");

            migrationBuilder.CreateIndex(
                name: "IX_StampingPoints_SeriesId_Number",
                table: "StampingPoints",
                columns: new[] { "SeriesId", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StampingPoints_SeriesId_ProviderId",
                table: "StampingPoints",
                columns: new[] { "SeriesId", "ProviderId" });

            migrationBuilder.CreateIndex(
                name: "IX_StampingSeries_ProviderId_Slug",
                table: "StampingSeries",
                columns: new[] { "ProviderId", "Slug" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_StampingPoints_StampingSeries_SeriesId_ProviderId",
                table: "StampingPoints",
                columns: new[] { "SeriesId", "ProviderId" },
                principalTable: "StampingSeries",
                principalColumns: new[] { "Id", "ProviderId" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_StampingPoints_StampingSeries_SeriesId_ProviderId",
                table: "StampingPoints");

            migrationBuilder.DropTable(
                name: "StampingSeries");

            migrationBuilder.DropIndex(
                name: "IX_StampingPoints_SeriesId_Number",
                table: "StampingPoints");

            migrationBuilder.DropIndex(
                name: "IX_StampingPoints_SeriesId_ProviderId",
                table: "StampingPoints");

            migrationBuilder.DropColumn(
                name: "SeriesId",
                table: "StampingPoints");

            migrationBuilder.DropColumn(
                name: "ValidFrom",
                table: "StampingPoints");

            migrationBuilder.DropColumn(
                name: "ValidUntil",
                table: "StampingPoints");

            migrationBuilder.AlterColumn<int>(
                name: "Number",
                table: "StampingPoints",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_StampingPoints_ProviderId_Number",
                table: "StampingPoints",
                columns: new[] { "ProviderId", "Number" },
                unique: true);
        }
    }
}
