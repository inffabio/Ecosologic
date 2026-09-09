using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ecosologic.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCrmNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "crm_notifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    LeadId = table.Column<Guid>(type: "uuid", nullable: false),
                    TaskId = table.Column<Guid>(type: "uuid", nullable: true),
                    Title = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    DueAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DueDay = table.Column<DateOnly>(type: "date", nullable: false),
                    ReadAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_crm_notifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_crm_notifications_lead_tasks_TaskId",
                        column: x => x.TaskId,
                        principalTable: "lead_tasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_crm_notifications_leads_LeadId",
                        column: x => x.LeadId,
                        principalTable: "leads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_crm_notifications_CreatedAt",
                table: "crm_notifications",
                column: "CreatedAt");

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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "crm_notifications");
        }
    }
}
