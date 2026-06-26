using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Migrations
{
    /// <inheritdoc />
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
                values: new object[] { 1, "Touringen stamping points and hiking tours.", "Touringen", "touringen", "https://www.touringen.de/" });

            migrationBuilder.AddColumn<int>(
                name: "DefaultStampingProviderId",
                table: "Users",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateIndex(
                name: "IX_Users_DefaultStampingProviderId",
                table: "Users",
                column: "DefaultStampingProviderId");

            migrationBuilder.CreateIndex(
                name: "IX_StampingProviders_Slug",
                table: "StampingProviders",
                column: "Slug",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Users_StampingProviders_DefaultStampingProviderId",
                table: "Users",
                column: "DefaultStampingProviderId",
                principalTable: "StampingProviders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Users_StampingProviders_DefaultStampingProviderId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_DefaultStampingProviderId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "DefaultStampingProviderId",
                table: "Users");

            migrationBuilder.DropTable(
                name: "StampingProviders");
        }
    }
}
