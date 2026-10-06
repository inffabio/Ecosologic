using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecosologic.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class IncludeDistributorIdInGridRuleKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_grid_compensation_rules_Distributor_Group_Subgroup_Modality~",
                table: "grid_compensation_rules");

            migrationBuilder.CreateIndex(
                name: "IX_grid_compensation_rules_Distributor_DistributorId_Group_Sub~",
                table: "grid_compensation_rules",
                columns: new[] { "Distributor", "DistributorId", "Group", "Subgroup", "Modality", "Post", "ReferenceYear", "ValidityStart" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_grid_compensation_rules_Distributor_DistributorId_Group_Sub~",
                table: "grid_compensation_rules");

            migrationBuilder.CreateIndex(
                name: "IX_grid_compensation_rules_Distributor_Group_Subgroup_Modality~",
                table: "grid_compensation_rules",
                columns: new[] { "Distributor", "Group", "Subgroup", "Modality", "Post", "ReferenceYear", "ValidityStart" },
                unique: true);
        }
    }
}
