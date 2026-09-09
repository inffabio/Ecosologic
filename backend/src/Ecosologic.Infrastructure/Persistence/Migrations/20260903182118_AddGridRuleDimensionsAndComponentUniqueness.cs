using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecosologic.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGridRuleDimensionsAndComponentUniqueness : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // O esquema antigo de grid_compensation_rules não possuía Group/Subgroup/Modality.
            // Em vez de EXCLUIR os registros legados, as novas colunas são introduzidas como
            // ANULÁVEIS e os registros legados são marcados como incompletos (IsComplete = false),
            // preservando os dados e impedindo que regras sem dimensão sejam elegíveis no lookup
            // até serem substituídas. Nenhuma CHECK constraint é adicionada sobre essas colunas,
            // de modo que o valor NULL (legado) permaneça aceito.

            migrationBuilder.DropIndex(
                name: "IX_grid_compensation_rules_Distributor_Post_ReferenceYear_Vali~",
                table: "grid_compensation_rules");

            migrationBuilder.DropCheckConstraint(
                name: "CK_grid_compensation_rules_progressive_percent",
                table: "grid_compensation_rules");

            migrationBuilder.AddColumn<string>(
                name: "Group",
                table: "grid_compensation_rules",
                type: "character varying(8)",
                maxLength: 8,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Modality",
                table: "grid_compensation_rules",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Subgroup",
                table: "grid_compensation_rules",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.Sql(GridRuleBackfillSql.MarkLegacyRulesIncomplete);

            migrationBuilder.CreateIndex(
                name: "IX_tariff_components_ProfileId_Kind_Unit_Post",
                table: "tariff_components",
                columns: new[] { "ProfileId", "Kind", "Unit", "Post" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_grid_compensation_rules_Distributor_Group_Subgroup_Modality~",
                table: "grid_compensation_rules",
                columns: new[] { "Distributor", "Group", "Subgroup", "Modality", "Post", "ReferenceYear", "ValidityStart" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_grid_compensation_rules_progressive_percent",
                table: "grid_compensation_rules",
                sql: "CAST(\"ProgressivePercent\" AS REAL) >= 0 AND CAST(\"ProgressivePercent\" AS REAL) <= 100");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_tariff_components_ProfileId_Kind_Unit_Post",
                table: "tariff_components");

            migrationBuilder.DropIndex(
                name: "IX_grid_compensation_rules_Distributor_Group_Subgroup_Modality~",
                table: "grid_compensation_rules");

            migrationBuilder.DropCheckConstraint(
                name: "CK_grid_compensation_rules_progressive_percent",
                table: "grid_compensation_rules");

            migrationBuilder.DropColumn(
                name: "Group",
                table: "grid_compensation_rules");

            migrationBuilder.DropColumn(
                name: "Modality",
                table: "grid_compensation_rules");

            migrationBuilder.DropColumn(
                name: "Subgroup",
                table: "grid_compensation_rules");

            migrationBuilder.CreateIndex(
                name: "IX_grid_compensation_rules_Distributor_Post_ReferenceYear_Vali~",
                table: "grid_compensation_rules",
                columns: new[] { "Distributor", "Post", "ReferenceYear", "ValidityStart" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_grid_compensation_rules_progressive_percent",
                table: "grid_compensation_rules",
                sql: "\"ProgressivePercent\" >= 0 AND \"ProgressivePercent\" <= 100");
        }
    }
}
