using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecosologic.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSolarMaterials : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "solar_materials",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Brand = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Model = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    PowerW = table.Column<int>(type: "integer", nullable: false),
                    TechnicalDataJson = table.Column<string>(type: "text", nullable: false),
                    SourceUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_solar_materials", x => x.Id);
                    table.CheckConstraint("CK_solar_materials_brand_nonempty", "\"Brand\" <> ''");
                    table.CheckConstraint("CK_solar_materials_model_nonempty", "\"Model\" <> ''");
                    table.CheckConstraint("CK_solar_materials_power_positive", "\"PowerW\" > 0");
                    table.CheckConstraint("CK_solar_materials_type", "\"Type\" IN ('Module','Inverter')");
                });

            migrationBuilder.CreateTable(
                name: "solar_material_prices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MaterialId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ValidFrom = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_solar_material_prices", x => x.Id);
                    table.CheckConstraint("CK_solar_material_prices_amount_nonnegative", "\"Amount\" >= 0");
                    table.ForeignKey(
                        name: "FK_solar_material_prices_solar_materials_MaterialId",
                        column: x => x.MaterialId,
                        principalTable: "solar_materials",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_solar_material_prices_MaterialId_ValidFrom",
                table: "solar_material_prices",
                columns: new[] { "MaterialId", "ValidFrom" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_solar_materials_Type_Brand_Model",
                table: "solar_materials",
                columns: new[] { "Type", "Brand", "Model" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "solar_material_prices");

            migrationBuilder.DropTable(
                name: "solar_materials");
        }
    }
}
