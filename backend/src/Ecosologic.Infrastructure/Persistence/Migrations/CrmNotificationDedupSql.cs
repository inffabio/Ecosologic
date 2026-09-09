namespace Ecosologic.Infrastructure.Persistence.Migrations;

/// <summary>
/// SQL que consolida notificações duplicadas por origem (TaskId ou LeadId) antes de
/// criar os índices únicos. Mantém a notificação mais recente, preferindo a não lida.
/// </summary>
public static class CrmNotificationDedupSql
{
    public const string RemoveDuplicateTaskNotifications = """
        DELETE FROM crm_notifications
        WHERE "Id" IN (
            SELECT "Id"
            FROM (
                SELECT "Id",
                       ROW_NUMBER() OVER (
                           PARTITION BY "TaskId"
                           ORDER BY ("ReadAt" IS NULL) DESC, "CreatedAt" DESC, "Id" ASC
                       ) AS rn
                FROM crm_notifications
                WHERE "TaskId" IS NOT NULL
            ) ranked
            WHERE rn > 1
        );
        """;

    public const string RemoveDuplicateLeadNotifications = """
        DELETE FROM crm_notifications
        WHERE "Id" IN (
            SELECT "Id"
            FROM (
                SELECT "Id",
                       ROW_NUMBER() OVER (
                           PARTITION BY "LeadId"
                           ORDER BY ("ReadAt" IS NULL) DESC, "CreatedAt" DESC, "Id" ASC
                       ) AS rn
                FROM crm_notifications
                WHERE "TaskId" IS NULL
            ) ranked
            WHERE rn > 1
        );
        """;
}
