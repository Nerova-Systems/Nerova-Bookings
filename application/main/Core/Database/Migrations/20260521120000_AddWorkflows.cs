using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Main.Database.Migrations;

[DbContext(typeof(MainDbContext))]
[Migration("20260521120000_AddWorkflows")]
public sealed class AddWorkflows : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            "workflows",
            table => new
            {
                id = table.Column<string>("text", nullable: false),
                tenant_id = table.Column<long>("bigint", nullable: false),
                owner_user_id = table.Column<string>("text", nullable: false),
                name = table.Column<string>("character varying(100)", maxLength: 100, nullable: false),
                trigger = table.Column<string>("text", nullable: false),
                created_at = table.Column<DateTimeOffset>("timestamp with time zone", nullable: false),
                modified_at = table.Column<DateTimeOffset>("timestamp with time zone", nullable: true),
                deleted_at = table.Column<DateTimeOffset>("timestamp with time zone", nullable: true)
            },
            constraints: table => { table.PrimaryKey("pk_workflows", w => w.id); }
        );

        migrationBuilder.CreateTable(
            "workflow_steps",
            table => new
            {
                id = table.Column<string>("text", nullable: false),
                workflow_id = table.Column<string>("text", nullable: false),
                action = table.Column<string>("text", nullable: false),
                template = table.Column<string>("text", nullable: false),
                reminder_time = table.Column<int>("integer", nullable: true),
                time_unit = table.Column<string>("text", nullable: true),
                send_to = table.Column<string>("character varying(320)", maxLength: 320, nullable: true),
                email_subject = table.Column<string>("character varying(500)", maxLength: 500, nullable: true),
                email_body = table.Column<string>("character varying(5000)", maxLength: 5000, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_workflow_steps", s => s.id);
                table.ForeignKey(
                    "fk_workflow_steps_workflow_workflow_id",
                    s => s.workflow_id,
                    "workflows",
                    "id",
                    onDelete: ReferentialAction.Cascade
                );
            }
        );

        migrationBuilder.CreateTable(
            "workflow_event_type_bindings",
            table => new
            {
                id = table.Column<string>("text", nullable: false),
                tenant_id = table.Column<long>("bigint", nullable: false),
                workflow_id = table.Column<string>("text", nullable: false),
                event_type_id = table.Column<string>("text", nullable: false),
                created_at = table.Column<DateTimeOffset>("timestamp with time zone", nullable: false),
                modified_at = table.Column<DateTimeOffset>("timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_workflow_event_type_bindings", b => b.id);
                table.ForeignKey(
                    "fk_workflow_event_type_bindings_workflows_workflow_id",
                    b => b.workflow_id,
                    "workflows",
                    "id",
                    onDelete: ReferentialAction.Cascade
                );
                table.ForeignKey(
                    "fk_workflow_event_type_bindings_event_types_event_type_id",
                    b => b.event_type_id,
                    "event_types",
                    "id",
                    onDelete: ReferentialAction.Cascade
                );
            }
        );

        migrationBuilder.CreateTable(
            "workflow_reminders",
            table => new
            {
                id = table.Column<string>("text", nullable: false),
                tenant_id = table.Column<long>("bigint", nullable: false),
                workflow_id = table.Column<string>("text", nullable: false),
                step_id = table.Column<string>("text", nullable: true),
                booking_id = table.Column<string>("text", nullable: false),
                booking_start_time = table.Column<DateTimeOffset>("timestamp with time zone", nullable: false),
                scheduled_date = table.Column<DateTimeOffset>("timestamp with time zone", nullable: false),
                status = table.Column<string>("text", nullable: false),
                action = table.Column<string>("text", nullable: false),
                template = table.Column<string>("text", nullable: false),
                send_to = table.Column<string>("character varying(320)", maxLength: 320, nullable: true),
                email_subject = table.Column<string>("character varying(500)", maxLength: 500, nullable: true),
                email_body = table.Column<string>("character varying(5000)", maxLength: 5000, nullable: true),
                reference_id = table.Column<string>("character varying(200)", maxLength: 200, nullable: true),
                error_message = table.Column<string>("character varying(2000)", maxLength: 2000, nullable: true),
                retry_count = table.Column<int>("integer", nullable: false),
                created_at = table.Column<DateTimeOffset>("timestamp with time zone", nullable: false),
                modified_at = table.Column<DateTimeOffset>("timestamp with time zone", nullable: true)
            },
            constraints: table => { table.PrimaryKey("pk_workflow_reminders", r => r.id); }
        );

        migrationBuilder.CreateIndex("ix_workflows_tenant_id_owner_user_id", "workflows", new[] { "tenant_id", "owner_user_id" });
        migrationBuilder.CreateIndex("ix_workflow_steps_workflow_id", "workflow_steps", "workflow_id");
        migrationBuilder.CreateIndex("ix_workflow_event_type_bindings_workflow_id_event_type_id", "workflow_event_type_bindings", new[] { "workflow_id", "event_type_id" }, unique: true);
        migrationBuilder.CreateIndex("ix_workflow_event_type_bindings_event_type_id", "workflow_event_type_bindings", "event_type_id");
        migrationBuilder.CreateIndex("ix_workflow_event_type_bindings_tenant_id_event_type_id", "workflow_event_type_bindings", new[] { "tenant_id", "event_type_id" });
        migrationBuilder.CreateIndex("ix_workflow_reminders_booking_id", "workflow_reminders", "booking_id");
        migrationBuilder.CreateIndex("ix_workflow_reminders_tenant_id_scheduled_date_status", "workflow_reminders", new[] { "tenant_id", "scheduled_date", "status" });
    }
}
