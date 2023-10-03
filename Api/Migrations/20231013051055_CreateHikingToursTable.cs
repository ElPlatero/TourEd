using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Migrations
{
    /// <inheritdoc />
    public partial class CreateHikingToursTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HikingTours",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Startpoint = table.Column<string>(type: "TEXT", nullable: true),
                    Endpoint = table.Column<string>(type: "TEXT", nullable: true),
                    KomootUri = table.Column<string>(type: "TEXT", nullable: true),
                    IsKidsTour = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsCircularPath = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsLongDistanceTrail = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HikingTours", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SortedStampingPoint",
                columns: table => new
                {
                    Position = table.Column<int>(type: "INTEGER", nullable: false),
                    StampingPointId = table.Column<int>(type: "INTEGER", nullable: false),
                    TourId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SortedStampingPoint", x => new { x.Position, x.StampingPointId, x.TourId });
                    table.ForeignKey(
                        name: "FK_SortedStampingPoint_HikingTours_TourId",
                        column: x => x.TourId,
                        principalTable: "HikingTours",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SortedStampingPoint_TourId",
                table: "SortedStampingPoint",
                column: "TourId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SortedStampingPoint");

            migrationBuilder.DropTable(
                name: "HikingTours");
        }
    }
}
