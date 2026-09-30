using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Migrations
{
    /// <inheritdoc />
    public partial class RenameStampingProviderDataReadyFlag : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "IsAnonymousAccessAllowed",
                table: "StampingProviders",
                newName: "IsDataReady");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "IsDataReady",
                table: "StampingProviders",
                newName: "IsAnonymousAccessAllowed");
        }
    }
}
