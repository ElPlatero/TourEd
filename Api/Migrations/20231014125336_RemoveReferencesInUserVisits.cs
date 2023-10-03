using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Migrations
{
    /// <inheritdoc />
    public partial class RemoveReferencesInUserVisits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UserVisit_StampingPoints_StampingPointId",
                table: "UserVisit");

            migrationBuilder.DropIndex(
                name: "IX_UserVisit_StampingPointId",
                table: "UserVisit");

            migrationBuilder.AlterColumn<DateTime>(
                name: "Visited",
                table: "UserVisit",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<DateTime>(
                name: "EntryCreated",
                table: "UserVisit",
                type: "TEXT",
                nullable: false,
                defaultValueSql: "datetime('now')",
                oldClrType: typeof(DateTime),
                oldType: "TEXT");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<DateTime>(
                name: "Visited",
                table: "UserVisit",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "EntryCreated",
                table: "UserVisit",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "TEXT",
                oldDefaultValueSql: "datetime('now')");

            migrationBuilder.CreateIndex(
                name: "IX_UserVisit_StampingPointId",
                table: "UserVisit",
                column: "StampingPointId");

            migrationBuilder.AddForeignKey(
                name: "FK_UserVisit_StampingPoints_StampingPointId",
                table: "UserVisit",
                column: "StampingPointId",
                principalTable: "StampingPoints",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
