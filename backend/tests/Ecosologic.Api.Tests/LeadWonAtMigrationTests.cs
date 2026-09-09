using Ecosologic.Infrastructure.Persistence.Migrations;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace Ecosologic.Api.Tests;

public class LeadWonAtMigrationTests
{
    [Fact]
    public void AddLeadWonAt_backfills_won_at_for_existing_won_leads()
    {
        var migration = new TestableMigration();
        var builder = new MigrationBuilder("Npgsql.EntityFrameworkCore.PostgreSQL");
        migration.Up(builder);

        var operations = builder.Operations.ToList();
        var addColumn = operations.FindIndex(o => o is AddColumnOperation add && add.Name == "WonAt");
        var backfill = operations.FindIndex(o => o is SqlOperation sql && sql.Sql.Contains("UPDATE leads"));

        Assert.True(addColumn >= 0);
        Assert.True(backfill > addColumn);

        var sql = ((SqlOperation)operations[backfill]).Sql;
        Assert.Contains("COALESCE(\"UpdatedAt\", \"CreatedAt\")", sql);
        Assert.Contains("\"Stage\" = 'Won'", sql);
    }

    [Fact]
    public void Backfill_sets_won_at_from_updated_at_then_created_at_only_for_won_leads()
    {
        using var connection = NewConnection();
        Insert(connection, "won-updated", "Won", "2026-09-01T09:00:00Z", "2026-09-01T10:00:00Z");
        Insert(connection, "won-no-updated", "Won", "2026-09-01T08:00:00Z", null);
        Insert(connection, "new-lead", "New", "2026-09-01T07:00:00Z", null);

        using (var command = connection.CreateCommand())
        {
            command.CommandText = LeadWonAtBackfillSql.SetWonAtForExistingWonLeads;
            command.ExecuteNonQuery();
        }

        Assert.Equal("2026-09-01T10:00:00Z", WonAt(connection, "won-updated"));
        Assert.Equal("2026-09-01T08:00:00Z", WonAt(connection, "won-no-updated"));
        Assert.Null(WonAt(connection, "new-lead"));
    }

    private static SqliteConnection NewConnection()
    {
        var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=False");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE leads (
                "Id" TEXT PRIMARY KEY,
                "Stage" TEXT NOT NULL,
                "CreatedAt" TEXT NULL,
                "UpdatedAt" TEXT NULL,
                "WonAt" TEXT NULL
            );
            """;
        command.ExecuteNonQuery();
        return connection;
    }

    private static void Insert(SqliteConnection connection, string id, string stage, string createdAt, string? updatedAt)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO leads ("Id", "Stage", "CreatedAt", "UpdatedAt")
            VALUES ($id, $stage, $createdAt, $updatedAt);
            """;
        command.Parameters.AddWithValue("$id", id);
        command.Parameters.AddWithValue("$stage", stage);
        command.Parameters.AddWithValue("$createdAt", createdAt);
        command.Parameters.AddWithValue("$updatedAt", (object?)updatedAt ?? DBNull.Value);
        command.ExecuteNonQuery();
    }

    private static string? WonAt(SqliteConnection connection, string id)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT \"WonAt\" FROM leads WHERE \"Id\" = $id;";
        command.Parameters.AddWithValue("$id", id);
        return command.ExecuteScalar() as string;
    }

    private sealed class TestableMigration : AddLeadWonAt
    {
        public new void Up(MigrationBuilder migrationBuilder) => base.Up(migrationBuilder);
    }
}
