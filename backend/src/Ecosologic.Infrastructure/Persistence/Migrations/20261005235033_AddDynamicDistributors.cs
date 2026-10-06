using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecosologic.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDynamicDistributors : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "DistributorId",
                table: "tariff_profiles",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DistributorId",
                table: "grid_compensation_rules",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Distributors",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AneelId = table.Column<string>(type: "text", nullable: false),
                    OfficialName = table.Column<string>(type: "text", nullable: false),
                    Cnpj = table.Column<string>(type: "text", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    LastSyncedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Distributors", x => x.Id);
                });

            migrationBuilder.Sql("""
                INSERT INTO "Distributors" ("Id", "AneelId", "OfficialName", "Cnpj", "IsActive", "LastSyncedAt")
                VALUES
                    ('00000000-0000-0000-0000-000000000001', 'LIGHT', 'Light Serviços de Eletricidade S.A.', NULL, TRUE, NOW()),
                    ('00000000-0000-0000-0000-000000000002', 'ENEL_RJ', 'Enel Distribuição Rio', NULL, TRUE, NOW())
                ON CONFLICT ("Id") DO NOTHING;

                UPDATE tariff_profiles
                SET "DistributorId" = CASE "Distributor"
                    WHEN 'Light' THEN '00000000-0000-0000-0000-000000000001'::uuid
                    WHEN 'EnelRio' THEN '00000000-0000-0000-0000-000000000002'::uuid
                    ELSE NULL
                END;

                UPDATE grid_compensation_rules
                SET "DistributorId" = CASE "Distributor"
                    WHEN 'Light' THEN '00000000-0000-0000-0000-000000000001'::uuid
                    WHEN 'EnelRio' THEN '00000000-0000-0000-0000-000000000002'::uuid
                    ELSE NULL
                END;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_tariff_profiles_DistributorId",
                table: "tariff_profiles",
                column: "DistributorId");

            migrationBuilder.CreateIndex(
                name: "IX_grid_compensation_rules_DistributorId",
                table: "grid_compensation_rules",
                column: "DistributorId");

            migrationBuilder.AddForeignKey(
                name: "FK_grid_compensation_rules_Distributors_DistributorId",
                table: "grid_compensation_rules",
                column: "DistributorId",
                principalTable: "Distributors",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_tariff_profiles_Distributors_DistributorId",
                table: "tariff_profiles",
                column: "DistributorId",
                principalTable: "Distributors",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_grid_compensation_rules_Distributors_DistributorId",
                table: "grid_compensation_rules");

            migrationBuilder.DropForeignKey(
                name: "FK_tariff_profiles_Distributors_DistributorId",
                table: "tariff_profiles");

            migrationBuilder.DropTable(
                name: "Distributors");

            migrationBuilder.DropIndex(
                name: "IX_tariff_profiles_DistributorId",
                table: "tariff_profiles");

            migrationBuilder.DropIndex(
                name: "IX_grid_compensation_rules_DistributorId",
                table: "grid_compensation_rules");

            migrationBuilder.DropColumn(
                name: "DistributorId",
                table: "tariff_profiles");

            migrationBuilder.DropColumn(
                name: "DistributorId",
                table: "grid_compensation_rules");
        }
    }
}
