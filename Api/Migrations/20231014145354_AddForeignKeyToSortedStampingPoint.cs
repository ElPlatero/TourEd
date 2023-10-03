using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Migrations
{
    /// <inheritdoc />
    public partial class AddForeignKeyToSortedStampingPoint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_SortedStampingPoint_StampingPointId",
                table: "SortedStampingPoint",
                column: "StampingPointId");

            migrationBuilder.AddForeignKey(
                name: "FK_SortedStampingPoint_StampingPoints_StampingPointId",
                table: "SortedStampingPoint",
                column: "StampingPointId",
                principalTable: "StampingPoints",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SortedStampingPoint_StampingPoints_StampingPointId",
                table: "SortedStampingPoint");

            migrationBuilder.DropIndex(
                name: "IX_SortedStampingPoint_StampingPointId",
                table: "SortedStampingPoint");
        }
    }
}
