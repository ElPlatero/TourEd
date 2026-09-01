using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Migrations
{
    /// <inheritdoc />
    public partial class ExtendAdminAuditEntries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "TargetUserId",
                table: "AdminAuditEntries",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.Sql("UPDATE AdminAuditEntries SET TargetUserId = NULL WHERE TargetUserId = 0;");

            migrationBuilder.AddColumn<int>(
                name: "RegistrationRequestId",
                table: "AdminAuditEntries",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AdminAuditEntries_RegistrationRequestId",
                table: "AdminAuditEntries",
                column: "RegistrationRequestId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AdminAuditEntries_RegistrationRequestId",
                table: "AdminAuditEntries");

            migrationBuilder.DropColumn(
                name: "RegistrationRequestId",
                table: "AdminAuditEntries");

            migrationBuilder.AlterColumn<int>(
                name: "TargetUserId",
                table: "AdminAuditEntries",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldNullable: true);
        }
    }
}
