using Api.Repositories;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(DataContext))]
    [Migration("20260626000000_AddStampingProvider")]
    public partial class AddStampingProvider : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StampingProviders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Slug = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    WebsiteUri = table.Column<string>(type: "TEXT", nullable: true),
                    Description = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StampingProviders", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "StampingProviders",
                columns: new[] { "Id", "Description", "Name", "Slug", "WebsiteUri" },
                columnTypes: new[] { "INTEGER", "TEXT", "TEXT", "TEXT", "TEXT" },
                values: new object[] { 1, "Touringen stamping points and hiking tours.", "Touringen", "touringen", "https://www.touringen.de/" });

            migrationBuilder.Sql("PRAGMA foreign_keys = 0;", suppressTransaction: true);

            migrationBuilder.CreateTable(
                name: "ef_temp_Users",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Email = table.Column<string>(type: "TEXT", nullable: false),
                    DefaultStampingProviderId = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 1)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Users_StampingProviders_DefaultStampingProviderId",
                        column: x => x.DefaultStampingProviderId,
                        principalTable: "StampingProviders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.Sql(
                "INSERT INTO ef_temp_Users (Id, Email, DefaultStampingProviderId) " +
                "SELECT Id, Email, 1 FROM Users;");

            migrationBuilder.DropTable(name: "Users");

            migrationBuilder.RenameTable(
                name: "ef_temp_Users",
                newName: "Users");

            migrationBuilder.Sql("PRAGMA foreign_keys = 1;", suppressTransaction: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_DefaultStampingProviderId",
                table: "Users",
                column: "DefaultStampingProviderId");

            migrationBuilder.CreateIndex(
                name: "IX_StampingProviders_Slug",
                table: "StampingProviders",
                column: "Slug",
                unique: true);

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("PRAGMA foreign_keys = 0;", suppressTransaction: true);

            migrationBuilder.CreateTable(
                name: "ef_temp_Users",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Email = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.Sql(
                "INSERT INTO ef_temp_Users (Id, Email) SELECT Id, Email FROM Users;");

            migrationBuilder.DropTable(name: "Users");

            migrationBuilder.RenameTable(
                name: "ef_temp_Users",
                newName: "Users");

            migrationBuilder.Sql("PRAGMA foreign_keys = 1;", suppressTransaction: true);

            migrationBuilder.DropTable(
                name: "StampingProviders");
        }
    }
}
