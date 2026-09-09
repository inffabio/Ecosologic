using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecosologic.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSolarCommercialNonEmptyChecks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddCheckConstraint(
                name: "CK_solar_sizings_grupo_nonempty",
                table: "solar_sizings",
                sql: "\"Grupo\" <> ''");

            migrationBuilder.AddCheckConstraint(
                name: "CK_solar_sizings_modalidade_nonempty",
                table: "solar_sizings",
                sql: "\"Modalidade\" <> ''");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_solar_sizings_grupo_nonempty",
                table: "solar_sizings");

            migrationBuilder.DropCheckConstraint(
                name: "CK_solar_sizings_modalidade_nonempty",
                table: "solar_sizings");
        }
    }
}
