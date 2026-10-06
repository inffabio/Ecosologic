using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecosologic.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AllowDynamicDistributorTariffs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_tariff_profiles_distributor",
                table: "tariff_profiles");

            migrationBuilder.DropCheckConstraint(
                name: "CK_grid_compensation_rules_distributor",
                table: "grid_compensation_rules");

            migrationBuilder.AddCheckConstraint(
                name: "CK_tariff_profiles_distributor",
                table: "tariff_profiles",
                sql: "\"Distributor\" IN ('Light','EnelRio','Dynamic')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_grid_compensation_rules_distributor",
                table: "grid_compensation_rules",
                sql: "\"Distributor\" IN ('Light','EnelRio','Dynamic')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_tariff_profiles_distributor",
                table: "tariff_profiles");

            migrationBuilder.DropCheckConstraint(
                name: "CK_grid_compensation_rules_distributor",
                table: "grid_compensation_rules");

            migrationBuilder.AddCheckConstraint(
                name: "CK_tariff_profiles_distributor",
                table: "tariff_profiles",
                sql: "\"Distributor\" IN ('Light','EnelRio')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_grid_compensation_rules_distributor",
                table: "grid_compensation_rules",
                sql: "\"Distributor\" IN ('Light','EnelRio')");
        }
    }
}
