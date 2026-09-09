using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecosologic.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCrmNotificationDedupAndOrderIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_crm_notifications_Kind_LeadId_DueDay",
                table: "crm_notifications");

            migrationBuilder.DropIndex(
                name: "IX_crm_notifications_Kind_TaskId_DueDay",
                table: "crm_notifications");

            migrationBuilder.DropIndex(
                name: "IX_crm_notifications_LeadId",
                table: "crm_notifications");

            migrationBuilder.DropIndex(
                name: "IX_crm_notifications_TaskId",
                table: "crm_notifications");

            migrationBuilder.Sql(CrmNotificationDedupSql.RemoveDuplicateTaskNotifications);
            migrationBuilder.Sql(CrmNotificationDedupSql.RemoveDuplicateLeadNotifications);

            migrationBuilder.CreateIndex(
                name: "IX_crm_notifications_LeadId",
                table: "crm_notifications",
                column: "LeadId",
                unique: true,
                filter: "\"TaskId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_crm_notifications_ReadAt_DueAt",
                table: "crm_notifications",
                columns: new[] { "ReadAt", "DueAt" });

            migrationBuilder.CreateIndex(
                name: "IX_crm_notifications_TaskId",
                table: "crm_notifications",
                column: "TaskId",
                unique: true,
                filter: "\"TaskId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_crm_notifications_LeadId",
                table: "crm_notifications");

            migrationBuilder.DropIndex(
                name: "IX_crm_notifications_ReadAt_DueAt",
                table: "crm_notifications");

            migrationBuilder.DropIndex(
                name: "IX_crm_notifications_TaskId",
                table: "crm_notifications");

            migrationBuilder.CreateIndex(
                name: "IX_crm_notifications_Kind_LeadId_DueDay",
                table: "crm_notifications",
                columns: new[] { "Kind", "LeadId", "DueDay" },
                unique: true,
                filter: "\"TaskId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_crm_notifications_Kind_TaskId_DueDay",
                table: "crm_notifications",
                columns: new[] { "Kind", "TaskId", "DueDay" },
                unique: true,
                filter: "\"TaskId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_crm_notifications_LeadId",
                table: "crm_notifications",
                column: "LeadId");

            migrationBuilder.CreateIndex(
                name: "IX_crm_notifications_TaskId",
                table: "crm_notifications",
                column: "TaskId");
        }
    }
}
