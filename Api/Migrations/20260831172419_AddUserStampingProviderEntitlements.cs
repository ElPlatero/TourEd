using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Migrations
{
    /// <inheritdoc />
    public partial class AddUserStampingProviderEntitlements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "DefaultStampingProviderId",
                table: "Users",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldDefaultValue: 1);

            migrationBuilder.CreateTable(
                name: "UserStampingProviders",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "INTEGER", nullable: false),
                    StampingProviderId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserStampingProviders", x => new { x.UserId, x.StampingProviderId });
                    table.ForeignKey(
                        name: "FK_UserStampingProviders_StampingProviders_StampingProviderId",
                        column: x => x.StampingProviderId,
                        principalTable: "StampingProviders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserStampingProviders_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserStampingProviders_StampingProviderId",
                table: "UserStampingProviders",
                column: "StampingProviderId");

            migrationBuilder.Sql(
                "INSERT INTO UserStampingProviders (UserId, StampingProviderId) " +
                "SELECT Users.Id, StampingProviders.Id FROM Users CROSS JOIN StampingProviders;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserStampingProviders");

            migrationBuilder.Sql(
                "UPDATE Users SET DefaultStampingProviderId = 1 WHERE DefaultStampingProviderId IS NULL;");

            migrationBuilder.AlterColumn<int>(
                name: "DefaultStampingProviderId",
                table: "Users",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldNullable: true);
        }
    }
}
