using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecosologic.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTariffProfileVersioning : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_tariff_profiles_Distributor_Group_Subgroup_Modality_Validit~",
                table: "tariff_profiles");

            migrationBuilder.AddColumn<bool>(
                name: "IsCurrent",
                table: "tariff_profiles",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "tariff_profiles",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateIndex(
                name: "IX_tariff_profiles_Distributor_Group_Subgroup_Modality_Validit~",
                table: "tariff_profiles",
                columns: new[] { "Distributor", "Group", "Subgroup", "Modality", "ValidityStart", "Version" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_tariff_profiles_Distributor_Group_Subgroup_Modality_Validit~",
                table: "tariff_profiles");

            migrationBuilder.DropColumn(
                name: "IsCurrent",
                table: "tariff_profiles");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "tariff_profiles");

            migrationBuilder.CreateIndex(
                name: "IX_tariff_profiles_Distributor_Group_Subgroup_Modality_Validit~",
                table: "tariff_profiles",
                columns: new[] { "Distributor", "Group", "Subgroup", "Modality", "ValidityStart" },
                unique: true);
        }
    }
}
