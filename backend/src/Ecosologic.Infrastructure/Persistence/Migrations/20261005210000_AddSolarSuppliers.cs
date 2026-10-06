using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Ecosologic.Infrastructure.Persistence;

#nullable disable

namespace Ecosologic.Infrastructure.Persistence.Migrations;

[DbContext(typeof(EcosologicDbContext))]
[Migration("20261005210000_ExpandSolarSupplierSource")]
public partial class ExpandSolarSupplierSource : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<string>(
            name: "Source",
            table: "solar_suppliers",
            type: "character varying(512)",
            maxLength: 512,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "character varying(64)",
            oldMaxLength: 64);
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.AlterColumn<string>(
            name: "Source",
            table: "solar_suppliers",
            type: "character varying(64)",
            maxLength: 64,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "character varying(512)",
            oldMaxLength: 512);
}
