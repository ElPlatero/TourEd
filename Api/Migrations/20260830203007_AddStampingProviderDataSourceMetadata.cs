using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Migrations
{
    /// <inheritdoc />
    public partial class AddStampingProviderDataSourceMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DataImportedAt",
                table: "StampingProviders",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DataLicenseName",
                table: "StampingProviders",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DataLicenseUri",
                table: "StampingProviders",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DataSourceAttribution",
                table: "StampingProviders",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DataSourceRevision",
                table: "StampingProviders",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DataSourceUpdatedAt",
                table: "StampingProviders",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DataSourceUri",
                table: "StampingProviders",
                type: "TEXT",
                nullable: true);

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DataImportedAt",
                table: "StampingProviders");

            migrationBuilder.DropColumn(
                name: "DataLicenseName",
                table: "StampingProviders");

            migrationBuilder.DropColumn(
                name: "DataLicenseUri",
                table: "StampingProviders");

            migrationBuilder.DropColumn(
                name: "DataSourceAttribution",
                table: "StampingProviders");

            migrationBuilder.DropColumn(
                name: "DataSourceRevision",
                table: "StampingProviders");

            migrationBuilder.DropColumn(
                name: "DataSourceUpdatedAt",
                table: "StampingProviders");

            migrationBuilder.DropColumn(
                name: "DataSourceUri",
                table: "StampingProviders");
        }
    }
}
