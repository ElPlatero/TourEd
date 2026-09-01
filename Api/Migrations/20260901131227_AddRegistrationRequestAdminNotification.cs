using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Migrations
{
    /// <inheritdoc />
    public partial class AddRegistrationRequestAdminNotification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "AdminNotificationSentAt",
                table: "RegistrationRequests",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "RegistrationNotificationStates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    LastSentAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RegistrationNotificationStates", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "RegistrationNotificationStates",
                columns: new[] { "Id", "LastSentAt" },
                values: new object[] { 1, null });

            migrationBuilder.CreateIndex(
                name: "IX_RegistrationRequests_Status_AdminNotificationSentAt",
                table: "RegistrationRequests",
                columns: new[] { "Status", "AdminNotificationSentAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RegistrationNotificationStates");

            migrationBuilder.DropIndex(
                name: "IX_RegistrationRequests_Status_AdminNotificationSentAt",
                table: "RegistrationRequests");

            migrationBuilder.DropColumn(
                name: "AdminNotificationSentAt",
                table: "RegistrationRequests");
        }
    }
}
