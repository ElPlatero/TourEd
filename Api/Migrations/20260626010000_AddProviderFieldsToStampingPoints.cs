using Api.Repositories;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(DataContext))]
    [Migration("20260626010000_AddProviderFieldsToStampingPoints")]
    public partial class AddProviderFieldsToStampingPoints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("PRAGMA foreign_keys = 0;", suppressTransaction: true);

            migrationBuilder.CreateTable(
                name: "ef_temp_StampingPoints",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Longitude = table.Column<decimal>(type: "TEXT", nullable: false),
                    Latitude = table.Column<decimal>(type: "TEXT", nullable: false),
                    Number = table.Column<int>(type: "INTEGER", nullable: false),
                    Code = table.Column<int>(type: "INTEGER", nullable: false),
                    ProviderId = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 1),
                    ExternalId = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StampingPoints", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StampingPoints_StampingProviders_ProviderId",
                        column: x => x.ProviderId,
                        principalTable: "StampingProviders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.Sql(
                "INSERT INTO ef_temp_StampingPoints " +
                "(Id, Name, Longitude, Latitude, Number, Code, ProviderId, ExternalId) " +
                "SELECT Id, Name, Longitude, Latitude, Number, Code, 1, CAST(Id AS TEXT) " +
                "FROM StampingPoints;");

            migrationBuilder.DropTable(name: "StampingPoints");

            migrationBuilder.RenameTable(
                name: "ef_temp_StampingPoints",
                newName: "StampingPoints");

            migrationBuilder.Sql("PRAGMA foreign_keys = 1;", suppressTransaction: true);

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

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("PRAGMA foreign_keys = 0;", suppressTransaction: true);

            migrationBuilder.CreateTable(
                name: "ef_temp_StampingPoints",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Longitude = table.Column<decimal>(type: "TEXT", nullable: false),
                    Latitude = table.Column<decimal>(type: "TEXT", nullable: false),
                    Number = table.Column<int>(type: "INTEGER", nullable: false),
                    Code = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StampingPoints", x => x.Id);
                });

            migrationBuilder.Sql(
                "INSERT INTO ef_temp_StampingPoints (Id, Name, Longitude, Latitude, Number, Code) " +
                "SELECT Id, Name, Longitude, Latitude, Number, Code FROM StampingPoints;");

            migrationBuilder.DropTable(name: "StampingPoints");

            migrationBuilder.RenameTable(
                name: "ef_temp_StampingPoints",
                newName: "StampingPoints");

            migrationBuilder.Sql("PRAGMA foreign_keys = 1;", suppressTransaction: true);
        }
    }
}
