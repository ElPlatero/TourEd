using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Migrations
{
    /// <inheritdoc />
    public partial class UpdateStampingProviderMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "UPDATE StampingProviders " +
                "SET Description = 'Touringen ist ein im Oktober 2022 von der Funke Mediengruppe in Kooperation mit der Thüringer Tourismus GmbH und regionalen Tourismusverbänden gestartetes System, das Wandererlebnisse mit einem Sammelanreiz verbindet. Nach einer Erweiterung im Juli 2023 umfasst das Netz 430 offizielle Stempelstellen an markanten Aussichtspunkten, Kulturdenkmälern und Naturhighlights in ganz Thüringen sowie im angrenzenden Frankenwald. Neben klassischen Stempel- und Tourenheften gibt es kindgerechte Varianten sowie ein mehrstufiges Abzeichensystem, bei dem Wanderer vom „Hobby Entdecker“ (ab 10 Stempeln) bis zum vollständigen „Touringen Entdecker“ (430 Stempel) mit Pins, Urkunden und einem Eintrag in die „Hall of Fame“ ausgezeichnet werden.' " +
                "WHERE Id = 1 AND Description = 'Touringen stamping points and hiking tours.';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "UPDATE StampingProviders " +
                "SET Description = 'Touringen stamping points and hiking tours.' " +
                "WHERE Id = 1 AND Description = 'Touringen ist ein im Oktober 2022 von der Funke Mediengruppe in Kooperation mit der Thüringer Tourismus GmbH und regionalen Tourismusverbänden gestartetes System, das Wandererlebnisse mit einem Sammelanreiz verbindet. Nach einer Erweiterung im Juli 2023 umfasst das Netz 430 offizielle Stempelstellen an markanten Aussichtspunkten, Kulturdenkmälern und Naturhighlights in ganz Thüringen sowie im angrenzenden Frankenwald. Neben klassischen Stempel- und Tourenheften gibt es kindgerechte Varianten sowie ein mehrstufiges Abzeichensystem, bei dem Wanderer vom „Hobby Entdecker“ (ab 10 Stempeln) bis zum vollständigen „Touringen Entdecker“ (430 Stempel) mit Pins, Urkunden und einem Eintrag in die „Hall of Fame“ ausgezeichnet werden.';");
        }
    }
}
