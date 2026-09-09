using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecosologic.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTariffCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "grid_compensation_rules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Distributor = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Post = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ReferenceYear = table.Column<int>(type: "integer", nullable: false),
                    ValidityStart = table.Column<DateOnly>(type: "date", nullable: false),
                    ValidityEnd = table.Column<DateOnly>(type: "date", nullable: true),
                    ProgressivePercent = table.Column<decimal>(type: "numeric(8,4)", precision: 8, scale: 4, nullable: false),
                    BaseComponent = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ResolutionCode = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    SourceUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    SourceDocumentHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    AccessedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IsComplete = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_grid_compensation_rules", x => x.Id);
                    table.CheckConstraint("CK_grid_compensation_rules_base_component", "\"BaseComponent\" IN ('TE','TUSD','TUSD_DISTRIBUTION','TUSD_TRANSMISSION','FIO_B','TAX','DEMAND','OVERAGE','OTHER')");
                    table.CheckConstraint("CK_grid_compensation_rules_distributor", "\"Distributor\" IN ('Light','EnelRio')");
                    table.CheckConstraint("CK_grid_compensation_rules_post", "\"Post\" IN ('Single','Peak','Intermediate','OffPeak')");
                    table.CheckConstraint("CK_grid_compensation_rules_progressive_percent", "\"ProgressivePercent\" >= 0 AND \"ProgressivePercent\" <= 100");
                    table.CheckConstraint("CK_grid_compensation_rules_resolution_nonempty", "\"ResolutionCode\" <> ''");
                    table.CheckConstraint("CK_grid_compensation_rules_validity", "\"ValidityEnd\" IS NULL OR \"ValidityEnd\" > \"ValidityStart\"");
                });

            migrationBuilder.CreateTable(
                name: "tariff_profiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Distributor = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Group = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    Subgroup = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Modality = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ValidityStart = table.Column<DateOnly>(type: "date", nullable: false),
                    ValidityEnd = table.Column<DateOnly>(type: "date", nullable: true),
                    ResolutionCode = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    SourceUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    SourceDocumentHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    AccessedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IsComplete = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tariff_profiles", x => x.Id);
                    table.CheckConstraint("CK_tariff_profiles_distributor", "\"Distributor\" IN ('Light','EnelRio')");
                    table.CheckConstraint("CK_tariff_profiles_group", "\"Group\" IN ('A','B')");
                    table.CheckConstraint("CK_tariff_profiles_modality", "\"Modality\" IN ('Conventional','White','Blue','Green')");
                    table.CheckConstraint("CK_tariff_profiles_resolution_nonempty", "\"ResolutionCode\" <> ''");
                    table.CheckConstraint("CK_tariff_profiles_subgroup", "\"Subgroup\" IN ('A1','A2','A3','A3a','A4','AS','B1','B2','B3','B4')");
                    table.CheckConstraint("CK_tariff_profiles_validity", "\"ValidityEnd\" IS NULL OR \"ValidityEnd\" > \"ValidityStart\"");
                });

            migrationBuilder.CreateTable(
                name: "tariff_components",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Unit = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Post = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Value = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: false),
                    TaxIncluded = table.Column<bool>(type: "boolean", nullable: false),
                    SourcePage = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tariff_components", x => x.Id);
                    table.CheckConstraint("CK_tariff_components_kind", "\"Kind\" IN ('TE','TUSD','TUSD_DISTRIBUTION','TUSD_TRANSMISSION','FIO_B','TAX','DEMAND','OVERAGE','OTHER')");
                    table.CheckConstraint("CK_tariff_components_post", "\"Post\" IN ('Single','Peak','Intermediate','OffPeak')");
                    table.CheckConstraint("CK_tariff_components_unit", "\"Unit\" IN ('KWh','KW','Month')");
                    table.CheckConstraint("CK_tariff_components_value_nonnegative", "\"Value\" >= 0");
                    table.ForeignKey(
                        name: "FK_tariff_components_tariff_profiles_ProfileId",
                        column: x => x.ProfileId,
                        principalTable: "tariff_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_grid_compensation_rules_Distributor_Post_ReferenceYear_Vali~",
                table: "grid_compensation_rules",
                columns: new[] { "Distributor", "Post", "ReferenceYear", "ValidityStart" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tariff_components_ProfileId",
                table: "tariff_components",
                column: "ProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_tariff_profiles_Distributor_Group_Subgroup_Modality_Validit~",
                table: "tariff_profiles",
                columns: new[] { "Distributor", "Group", "Subgroup", "Modality", "ValidityStart" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "grid_compensation_rules");

            migrationBuilder.DropTable(
                name: "tariff_components");

            migrationBuilder.DropTable(
                name: "tariff_profiles");
        }
    }
}
