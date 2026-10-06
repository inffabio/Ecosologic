using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecosologic.Infrastructure.Persistence.Migrations;

public partial class AddSizingSnapshots : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "MaterialSnapshotJson",
            table: "solar_sizings",
            type: "text",
            nullable: false,
            defaultValue: "{}");

        migrationBuilder.AddColumn<string>(
            name: "TariffSnapshotJson",
            table: "solar_sizings",
            type: "text",
            nullable: false,
            defaultValue: "{}");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "MaterialSnapshotJson", table: "solar_sizings");
        migrationBuilder.DropColumn(name: "TariffSnapshotJson", table: "solar_sizings");
    }
}
