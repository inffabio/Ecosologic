using Microsoft.EntityFrameworkCore.Migrations;
using NpgsqlTypes;

#nullable disable

namespace Ecosologic.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDistributorTextSearch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS unaccent;");
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS pg_trgm;");
            migrationBuilder.AlterColumn<string>(
                name: "OfficialName",
                table: "Distributors",
                type: "character varying(240)",
                maxLength: 240,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Cnpj",
                table: "Distributors",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "AneelId",
                table: "Distributors",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<NpgsqlTsVector>(
                name: "SearchVector",
                table: "Distributors",
                type: "tsvector",
                nullable: false,
                defaultValueSql: "to_tsvector('simple', '')");

            migrationBuilder.Sql("""
                UPDATE "Distributors"
                SET "SearchVector" = to_tsvector('simple', unaccent(coalesce("OfficialName", '') || ' ' || coalesce("AneelId", '')));

                CREATE OR REPLACE FUNCTION distributors_search_vector_update() RETURNS trigger AS $$
                BEGIN
                    NEW."SearchVector" := to_tsvector('simple', unaccent(coalesce(NEW."OfficialName", '') || ' ' || coalesce(NEW."AneelId", '')));
                    RETURN NEW;
                END;
                $$ LANGUAGE plpgsql;

                CREATE TRIGGER distributors_search_vector_trigger
                BEFORE INSERT OR UPDATE OF "OfficialName", "AneelId" ON "Distributors"
                FOR EACH ROW EXECUTE FUNCTION distributors_search_vector_update();
                """);

            migrationBuilder.Sql("CREATE INDEX \"IX_Distributors_SearchVector\" ON \"Distributors\" USING GIN (\"SearchVector\");");
            migrationBuilder.Sql("CREATE INDEX \"IX_Distributors_OfficialName_Trgm\" ON \"Distributors\" USING GIN (\"OfficialName\" gin_trgm_ops);");

            migrationBuilder.CreateIndex(
                name: "IX_Distributors_AneelId",
                table: "Distributors",
                column: "AneelId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS distributors_search_vector_trigger ON \"Distributors\";");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS distributors_search_vector_update();");
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_Distributors_SearchVector\";");
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_Distributors_OfficialName_Trgm\";");
            migrationBuilder.DropIndex(
                name: "IX_Distributors_AneelId",
                table: "Distributors");

            migrationBuilder.DropColumn(
                name: "SearchVector",
                table: "Distributors");

            migrationBuilder.AlterColumn<string>(
                name: "OfficialName",
                table: "Distributors",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(240)",
                oldMaxLength: 240);

            migrationBuilder.AlterColumn<string>(
                name: "Cnpj",
                table: "Distributors",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(32)",
                oldMaxLength: 32,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "AneelId",
                table: "Distributors",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(128)",
                oldMaxLength: 128);
        }
    }
}
