using Ecosologic.Infrastructure.Persistence.Migrations;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace Ecosologic.Api.Tests;

public class GridRuleMigrationTests
{
    private static SqliteConnection NewConnection()
    {
        var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=False");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE grid_compensation_rules (
                "Id" TEXT PRIMARY KEY,
                "Distributor" TEXT NOT NULL,
                "Post" TEXT NOT NULL,
                "ReferenceYear" INTEGER NOT NULL,
                "ValidityStart" TEXT NOT NULL,
                "ValidityEnd" TEXT NULL,
                "ProgressivePercent" REAL NOT NULL,
                "BaseComponent" TEXT NOT NULL,
                "ResolutionCode" TEXT NOT NULL,
                "SourceUrl" TEXT NOT NULL,
                "SourceDocumentHash" TEXT NULL,
                "AccessedAt" TEXT NOT NULL,
                "IsComplete" INTEGER NOT NULL
            );
            """;
        command.ExecuteNonQuery();
        return connection;
    }

    private static void Insert(SqliteConnection connection, string id)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO grid_compensation_rules ("Id", "Distributor", "Post", "ReferenceYear", "ValidityStart", "ProgressivePercent", "BaseComponent", "ResolutionCode", "SourceUrl", "AccessedAt", "IsComplete")
            VALUES ($id, 'Light', 'Single', 2024, '2024-01-01', 15, 'TUSD_DISTRIBUTION', 'RES', 'url', '2024-01-01', 1);
            """;
        command.Parameters.AddWithValue("$id", id);
        command.ExecuteNonQuery();
    }

    private static void AddLegacyDimensionColumns(SqliteConnection connection)
    {
        foreach (var column in new[] { "Group", "Subgroup", "Modality" })
        {
            using var command = connection.CreateCommand();
            command.CommandText = $"ALTER TABLE grid_compensation_rules ADD COLUMN \"{column}\" TEXT NULL;";
            command.ExecuteNonQuery();
        }
    }

    private static List<string> RemainingIds(SqliteConnection connection)
    {
        var ids = new List<string>();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT \"Id\" FROM grid_compensation_rules ORDER BY \"Id\";";
        using var reader = command.ExecuteReader();
        while (reader.Read())
            ids.Add(reader.GetString(0));
        return ids;
    }

    private static bool IsComplete(SqliteConnection connection, string id)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT \"IsComplete\" FROM grid_compensation_rules WHERE \"Id\" = $id;";
        command.Parameters.AddWithValue("$id", id);
        return Convert.ToInt32(command.ExecuteScalar()) != 0;
    }

    [Fact]
    public void Migration_preserves_legacy_grid_rules()
    {
        var migration = new TestableMigration();
        var builder = new MigrationBuilder("Npgsql.EntityFrameworkCore.PostgreSQL");
        migration.Up(builder);

        var operations = builder.Operations.ToList();
        Assert.DoesNotContain(
            operations,
            operation => operation is SqlOperation sql && sql.Sql.Contains("DELETE FROM grid_compensation_rules"));
    }

    [Fact]
    public void Migration_adds_group_subgroup_modality_as_nullable_without_default()
    {
        var migration = new TestableMigration();
        var builder = new MigrationBuilder("Npgsql.EntityFrameworkCore.PostgreSQL");
        migration.Up(builder);

        var columns = builder.Operations
            .OfType<AddColumnOperation>()
            .Where(add => add.Name is "Group" or "Subgroup" or "Modality")
            .ToList();

        Assert.Equal(3, columns.Count);
        Assert.All(columns, column =>
        {
            Assert.True(column.IsNullable, $"{column.Name} deve ser anulável.");
            Assert.Null(column.DefaultValue);
        });
    }

    [Fact]
    public void Migration_marks_legacy_rules_incomplete_after_adding_dimensions()
    {
        var migration = new TestableMigration();
        var builder = new MigrationBuilder("Npgsql.EntityFrameworkCore.PostgreSQL");
        migration.Up(builder);

        var operations = builder.Operations.ToList();
        var addGroup = operations.FindIndex(operation => operation is AddColumnOperation add && add.Name == "Group");
        var backfill = operations.FindIndex(operation => operation is SqlOperation sql && sql.Sql.Contains("UPDATE grid_compensation_rules"));

        Assert.True(addGroup >= 0, "migração deve adicionar a coluna Group");
        Assert.True(backfill > addGroup, "backfill deve ocorrer após adicionar as dimensões");

        var sql = ((SqlOperation)operations[backfill]).Sql;
        Assert.Contains("\"IsComplete\" = FALSE", sql);
        Assert.Contains("\"Group\" IS NULL", sql);
    }

    [Fact]
    public void Migration_does_not_add_check_constraints_that_reject_null_dimensions()
    {
        var migration = new TestableMigration();
        var builder = new MigrationBuilder("Npgsql.EntityFrameworkCore.PostgreSQL");
        migration.Up(builder);

        var checks = builder.Operations
            .OfType<AddCheckConstraintOperation>()
            .Select(check => check.Name)
            .ToList();

        Assert.DoesNotContain("CK_grid_compensation_rules_group", checks);
        Assert.DoesNotContain("CK_grid_compensation_rules_subgroup", checks);
        Assert.DoesNotContain("CK_grid_compensation_rules_modality", checks);
        Assert.Contains("CK_grid_compensation_rules_progressive_percent", checks);
    }

    [Fact]
    public void MarkLegacyRulesIncomplete_preserves_rows_and_marks_them_incomplete()
    {
        using var connection = NewConnection();
        Insert(connection, "r1");
        Insert(connection, "r2");
        AddLegacyDimensionColumns(connection);

        using (var command = connection.CreateCommand())
        {
            command.CommandText = GridRuleBackfillSql.MarkLegacyRulesIncomplete;
            command.ExecuteNonQuery();
        }

        Assert.Equal(new[] { "r1", "r2" }, RemainingIds(connection));
        Assert.False(IsComplete(connection, "r1"));
        Assert.False(IsComplete(connection, "r2"));
    }

    private sealed class TestableMigration : AddGridRuleDimensionsAndComponentUniqueness
    {
        public new void Up(MigrationBuilder migrationBuilder) => base.Up(migrationBuilder);
    }
}
