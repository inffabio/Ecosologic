using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecosologic.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSolarCommercialInvariants : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddCheckConstraint(
                name: "CK_solar_sizings_results_for_calculated_approved",
                table: "solar_sizings",
                sql: "(\"Status\" NOT IN ('Calculated','Approved')) OR (\"ResultsJson\" IS NOT NULL AND \"ResultsJson\" <> '')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_solar_quotes_approved_requires_snapshot",
                table: "solar_quotes",
                sql: "(\"Status\" <> 'Approved') OR (\"SnapshotJson\" IS NOT NULL AND \"SnapshotJson\" <> '')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_proposals_generated_requires_file",
                table: "proposals",
                sql: "(\"Status\" <> 'Generated') OR (\"FileUrl\" IS NOT NULL AND \"FileUrl\" <> '')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_solar_sizings_results_for_calculated_approved",
                table: "solar_sizings");

            migrationBuilder.DropCheckConstraint(
                name: "CK_solar_quotes_approved_requires_snapshot",
                table: "solar_quotes");

            migrationBuilder.DropCheckConstraint(
                name: "CK_proposals_generated_requires_file",
                table: "proposals");
        }
    }
}
