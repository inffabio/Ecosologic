using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecosologic.Infrastructure.Persistence.Migrations;

public partial class AddQuotePriceRefreshAudit : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "PriceRefreshedAt",
            table: "solar_quotes",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "PriceRefreshedBy",
            table: "solar_quotes",
            type: "character varying(254)",
            maxLength: 254,
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "PriceRefreshedAt", table: "solar_quotes");
        migrationBuilder.DropColumn(name: "PriceRefreshedBy", table: "solar_quotes");
    }
}
