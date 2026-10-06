using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecosologic.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class StrengthenDistributorCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Distributors_OfficialName",
                table: "Distributors",
                column: "OfficialName");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Distributors_OfficialName",
                table: "Distributors");
        }
    }
}
