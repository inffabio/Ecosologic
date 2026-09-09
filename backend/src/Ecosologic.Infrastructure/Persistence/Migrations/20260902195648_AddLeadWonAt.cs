using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecosologic.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLeadWonAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "WonAt",
                table: "leads",
                type: "timestamp with time zone",
                nullable: true);

            // Backfill seguro: preserva leads que já estavam fechados (Stage = Won) antes
            // desta migração, aproximando WonAt por UpdatedAt (ou CreatedAt se UpdatedAt nulo).
            migrationBuilder.Sql(LeadWonAtBackfillSql.SetWonAtForExistingWonLeads);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "WonAt",
                table: "leads");
        }
    }
}
