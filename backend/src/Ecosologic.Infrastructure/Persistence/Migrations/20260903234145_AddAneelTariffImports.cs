using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecosologic.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAneelTariffImports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "aneel_tariff_imports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    JobId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    FiltersJson = table.Column<string>(type: "text", nullable: false),
                    SourceUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    SourceHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    RawRecordCount = table.Column<int>(type: "integer", nullable: false),
                    AcceptedProfileCount = table.Column<int>(type: "integer", nullable: false),
                    RejectedRecordCount = table.Column<int>(type: "integer", nullable: false),
                    InsertedProfileCount = table.Column<int>(type: "integer", nullable: false),
                    ClosedProfileCount = table.Column<int>(type: "integer", nullable: false),
                    UnchangedProfileCount = table.Column<int>(type: "integer", nullable: false),
                    ErrorMessage = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_aneel_tariff_imports", x => x.Id);
                    table.CheckConstraint("CK_aneel_tariff_imports_counts_nonnegative", "\"RawRecordCount\" >= 0 AND \"AcceptedProfileCount\" >= 0 AND \"RejectedRecordCount\" >= 0 AND \"InsertedProfileCount\" >= 0 AND \"ClosedProfileCount\" >= 0 AND \"UnchangedProfileCount\" >= 0");
                    table.CheckConstraint("CK_aneel_tariff_imports_status", "\"Status\" IN ('Running','Succeeded','Failed')");
                });

            migrationBuilder.CreateIndex(
                name: "IX_aneel_tariff_imports_SourceHash",
                table: "aneel_tariff_imports",
                column: "SourceHash");

            migrationBuilder.CreateIndex(
                name: "IX_aneel_tariff_imports_Status_StartedAt",
                table: "aneel_tariff_imports",
                columns: new[] { "Status", "StartedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "aneel_tariff_imports");
        }
    }
}
