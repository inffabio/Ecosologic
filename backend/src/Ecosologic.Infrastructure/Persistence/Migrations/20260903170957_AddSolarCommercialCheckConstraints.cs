using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecosologic.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSolarCommercialCheckConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddCheckConstraint(
                name: "CK_solar_sizings_status",
                table: "solar_sizings",
                sql: "\"Status\" IN ('Draft','Calculated','Approved','Cancelled')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_solar_quotes_margin_percent_nonnegative",
                table: "solar_quotes",
                sql: "\"MarginPercent\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_solar_quotes_status",
                table: "solar_quotes",
                sql: "\"Status\" IN ('Draft','Approved','Sent','Accepted','Rejected','Expired')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_solar_quotes_tax_percent_nonnegative",
                table: "solar_quotes",
                sql: "\"TaxPercent\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_solar_quotes_total_cost_nonnegative",
                table: "solar_quotes",
                sql: "\"TotalCost\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_solar_quotes_total_price_nonnegative",
                table: "solar_quotes",
                sql: "\"TotalPrice\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_proposals_status",
                table: "proposals",
                sql: "\"Status\" IN ('Draft','Generated','Sent','Accepted','Rejected','Expired')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_solar_sizings_status",
                table: "solar_sizings");

            migrationBuilder.DropCheckConstraint(
                name: "CK_solar_quotes_margin_percent_nonnegative",
                table: "solar_quotes");

            migrationBuilder.DropCheckConstraint(
                name: "CK_solar_quotes_status",
                table: "solar_quotes");

            migrationBuilder.DropCheckConstraint(
                name: "CK_solar_quotes_tax_percent_nonnegative",
                table: "solar_quotes");

            migrationBuilder.DropCheckConstraint(
                name: "CK_solar_quotes_total_cost_nonnegative",
                table: "solar_quotes");

            migrationBuilder.DropCheckConstraint(
                name: "CK_solar_quotes_total_price_nonnegative",
                table: "solar_quotes");

            migrationBuilder.DropCheckConstraint(
                name: "CK_proposals_status",
                table: "proposals");
        }
    }
}
