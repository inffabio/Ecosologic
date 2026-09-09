using Ecosologic.Infrastructure.Persistence.Migrations;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace Ecosologic.Api.Tests;

public class CrmNotificationMigrationDedupTests
{
    private static SqliteConnection NewConnection()
    {
        var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=False");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE crm_notifications (
                "Id" TEXT PRIMARY KEY,
                "TaskId" TEXT NULL,
                "LeadId" TEXT NOT NULL,
                "ReadAt" TEXT NULL,
                "CreatedAt" TEXT NOT NULL
            );
            """;
        command.ExecuteNonQuery();
        return connection;
    }

    private static void Insert(SqliteConnection connection, string id, string? taskId, string leadId, string? readAt, string createdAt)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO crm_notifications ("Id", "TaskId", "LeadId", "ReadAt", "CreatedAt")
            VALUES ($id, $taskId, $leadId, $readAt, $createdAt);
            """;
        command.Parameters.AddWithValue("$id", id);
        command.Parameters.AddWithValue("$taskId", (object?)taskId ?? DBNull.Value);
        command.Parameters.AddWithValue("$leadId", leadId);
        command.Parameters.AddWithValue("$readAt", (object?)readAt ?? DBNull.Value);
        command.Parameters.AddWithValue("$createdAt", createdAt);
        command.ExecuteNonQuery();
    }

    private static void ExecuteDedup(SqliteConnection connection)
    {
        using (var command = connection.CreateCommand())
        {
            command.CommandText = CrmNotificationDedupSql.RemoveDuplicateTaskNotifications;
            command.ExecuteNonQuery();
        }

        using (var command = connection.CreateCommand())
        {
            command.CommandText = CrmNotificationDedupSql.RemoveDuplicateLeadNotifications;
            command.ExecuteNonQuery();
        }
    }

    private static List<string> RemainingIds(SqliteConnection connection)
    {
        var ids = new List<string>();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT \"Id\" FROM crm_notifications ORDER BY \"Id\";";
        using var reader = command.ExecuteReader();
        while (reader.Read())
            ids.Add(reader.GetString(0));
        return ids;
    }

    [Fact]
    public void Dedup_removes_duplicate_task_notifications_keeping_unread_most_recent()
    {
        using var connection = NewConnection();
        Insert(connection, "t1-old-unread", "T1", "L1", null, "2026-09-01T09:00:00Z");
        Insert(connection, "t1-new-unread", "T1", "L1", null, "2026-09-01T10:00:00Z");
        Insert(connection, "other-task", "T2", "L1", null, "2026-09-01T08:00:00Z");

        ExecuteDedup(connection);

        Assert.Equal(new[] { "other-task", "t1-new-unread" }, RemainingIds(connection));
    }

    [Fact]
    public void Dedup_prefers_unread_over_more_recent_read()
    {
        using var connection = NewConnection();
        Insert(connection, "t-unread", "T1", "L1", null, "2026-09-01T09:00:00Z");
        Insert(connection, "t-read-newer", "T1", "L1", "2026-09-01T12:00:00Z", "2026-09-01T11:00:00Z");

        ExecuteDedup(connection);

        Assert.Equal(new[] { "t-unread" }, RemainingIds(connection));
    }

    [Fact]
    public void Dedup_removes_duplicate_lead_notifications_keeping_unread_most_recent()
    {
        using var connection = NewConnection();
        Insert(connection, "l1-old", null, "L1", null, "2026-09-01T09:00:00Z");
        Insert(connection, "l1-new", null, "L1", null, "2026-09-01T10:00:00Z");
        Insert(connection, "l2-keep", null, "L2", "2026-09-01T08:00:00Z", "2026-09-01T08:00:00Z");

        ExecuteDedup(connection);

        Assert.Equal(new[] { "l1-new", "l2-keep" }, RemainingIds(connection));
    }

    [Fact]
    public void Dedup_does_not_merge_task_and_lead_origins()
    {
        using var connection = NewConnection();
        Insert(connection, "task", "T1", "L1", null, "2026-09-01T09:00:00Z");
        Insert(connection, "lead", null, "L1", null, "2026-09-01T09:00:00Z");

        ExecuteDedup(connection);

        Assert.Equal(new[] { "lead", "task" }, RemainingIds(connection));
    }

    [Fact]
    public void Dedup_is_safe_on_empty_table()
    {
        using var connection = NewConnection();

        ExecuteDedup(connection);

        Assert.Empty(RemainingIds(connection));
    }

    [Fact]
    public void Migration_removes_duplicates_before_creating_unique_indexes()
    {
        var migration = new TestableMigration();
        var builder = new MigrationBuilder("Npgsql.EntityFrameworkCore.PostgreSQL");
        migration.Up(builder);

        var operations = builder.Operations.ToList();
        var dedupOperations = operations.OfType<SqlOperation>().Where(o => o.Sql.Contains("DELETE FROM crm_notifications")).ToList();
        var leadIndex = operations.FindIndex(o => o is CreateIndexOperation create && create.Name == "IX_crm_notifications_LeadId");
        var taskIndex = operations.FindIndex(o => o is CreateIndexOperation create && create.Name == "IX_crm_notifications_TaskId");

        Assert.Equal(2, dedupOperations.Count);

        var firstDedup = operations.FindIndex(o => dedupOperations.Contains(o));
        Assert.True(firstDedup >= 0);
        Assert.True(firstDedup < leadIndex);
        Assert.True(firstDedup < taskIndex);

        Assert.Contains("ROW_NUMBER", dedupOperations[0].Sql);
        Assert.Contains("PARTITION BY \"TaskId\"", dedupOperations[0].Sql);
        Assert.Contains("(\"ReadAt\" IS NULL) DESC", dedupOperations[0].Sql);
        Assert.Contains("PARTITION BY \"LeadId\"", dedupOperations[1].Sql);
    }

    private sealed class TestableMigration : AddCrmNotificationDedupAndOrderIndex
    {
        public new void Up(MigrationBuilder migrationBuilder) => base.Up(migrationBuilder);
    }
}
