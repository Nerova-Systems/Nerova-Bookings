using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Main.Database.Migrations;

[DbContext(typeof(MainDbContext))]
[Migration("20260612115800_AddReceptionistAgent")]
public sealed class AddReceptionistAgent : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            "receptionist_sessions",
            table => new
            {
                tenant_id = table.Column<long>("bigint", nullable: false),
                id = table.Column<string>("text", nullable: false),
                whats_app_conversation_id = table.Column<string>("text", nullable: false),
                created_at = table.Column<DateTimeOffset>("timestamptz", nullable: false),
                modified_at = table.Column<DateTimeOffset>("timestamptz", nullable: true),
                agent_thread = table.Column<string>("jsonb", nullable: true),
                state = table.Column<string>("text", nullable: false),
                turn_count = table.Column<int>("integer", nullable: false),
                input_tokens = table.Column<long>("bigint", nullable: false),
                output_tokens = table.Column<long>("bigint", nullable: false),
                last_turn_at = table.Column<DateTimeOffset>("timestamptz", nullable: true),
                escalation_hold_notified = table.Column<bool>("boolean", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_receptionist_sessions", x => x.id);
                table.ForeignKey("fk_receptionist_sessions_whats_app_conversations_whats_app_conversation_id", x => x.whats_app_conversation_id, "whats_app_conversations", "id");
            }
        );

        migrationBuilder.CreateIndex("ix_receptionist_sessions_tenant_id", "receptionist_sessions", "tenant_id");
        migrationBuilder.CreateIndex("ix_receptionist_sessions_whats_app_conversation_id", "receptionist_sessions", "whats_app_conversation_id", unique: true);

        migrationBuilder.CreateTable(
            "escalations",
            table => new
            {
                tenant_id = table.Column<long>("bigint", nullable: false),
                id = table.Column<string>("text", nullable: false),
                whats_app_conversation_id = table.Column<string>("text", nullable: false),
                client_id = table.Column<string>("text", nullable: true),
                created_at = table.Column<DateTimeOffset>("timestamptz", nullable: false),
                modified_at = table.Column<DateTimeOffset>("timestamptz", nullable: true),
                reason = table.Column<string>("text", nullable: false),
                summary = table.Column<string>("text", nullable: false),
                status = table.Column<string>("text", nullable: false),
                resolved_by_user_id = table.Column<string>("text", nullable: true),
                resolution_note = table.Column<string>("text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_escalations", x => x.id);
                table.ForeignKey("fk_escalations_whats_app_conversations_whats_app_conversation_id", x => x.whats_app_conversation_id, "whats_app_conversations", "id");
            }
        );

        migrationBuilder.CreateIndex("ix_escalations_tenant_id", "escalations", "tenant_id");
        migrationBuilder.CreateIndex("ix_escalations_whats_app_conversation_id", "escalations", "whats_app_conversation_id");

        migrationBuilder.CreateTable(
            "receptionist_settings",
            table => new
            {
                tenant_id = table.Column<long>("bigint", nullable: false),
                id = table.Column<string>("text", nullable: false),
                created_at = table.Column<DateTimeOffset>("timestamptz", nullable: false),
                modified_at = table.Column<DateTimeOffset>("timestamptz", nullable: true),
                is_enabled = table.Column<bool>("boolean", nullable: false),
                tone = table.Column<string>("text", nullable: false),
                languages = table.Column<string>("jsonb", nullable: false),
                faq_notes = table.Column<string>("text", nullable: true),
                owner_phone_number = table.Column<string>("text", nullable: true)
            },
            constraints: table => { table.PrimaryKey("pk_receptionist_settings", x => x.id); }
        );

        migrationBuilder.CreateIndex("ix_receptionist_settings_tenant_id", "receptionist_settings", "tenant_id", unique: true);

        migrationBuilder.AddColumn<string>("paystack_subaccount_code", "scheduling_profiles", "text", nullable: true);

        migrationBuilder.AddColumn<string>("notes", "clients", "text", nullable: true);

        migrationBuilder.CreateTable(
            "import_jobs",
            table => new
            {
                tenant_id = table.Column<long>("bigint", nullable: false),
                id = table.Column<string>("text", nullable: false),
                created_at = table.Column<DateTimeOffset>("timestamptz", nullable: false),
                modified_at = table.Column<DateTimeOffset>("timestamptz", nullable: true),
                file_name = table.Column<string>("text", nullable: false),
                file_content = table.Column<string>("text", nullable: false),
                status = table.Column<string>("text", nullable: false),
                column_mapping = table.Column<string>("jsonb", nullable: true),
                rows = table.Column<string>("jsonb", nullable: false),
                rows_total = table.Column<int>("integer", nullable: false),
                rows_valid = table.Column<int>("integer", nullable: false),
                rows_duplicate = table.Column<int>("integer", nullable: false),
                rows_invalid = table.Column<int>("integer", nullable: false),
                rows_committed = table.Column<int>("integer", nullable: false),
                approved_by_user_id = table.Column<string>("text", nullable: true),
                error_message = table.Column<string>("text", nullable: true)
            },
            constraints: table => { table.PrimaryKey("pk_import_jobs", x => x.id); }
        );

        migrationBuilder.CreateIndex("ix_import_jobs_tenant_id", "import_jobs", "tenant_id");

        migrationBuilder.CreateTable(
            "job_runs",
            table => new
            {
                tenant_id = table.Column<long>("bigint", nullable: false),
                id = table.Column<string>("text", nullable: false),
                created_at = table.Column<DateTimeOffset>("timestamptz", nullable: false),
                modified_at = table.Column<DateTimeOffset>("timestamptz", nullable: true),
                job_type = table.Column<string>("text", nullable: false),
                trigger_reference = table.Column<string>("text", nullable: false),
                summary = table.Column<string>("text", nullable: false),
                payload_json = table.Column<string>("jsonb", nullable: true),
                status = table.Column<string>("text", nullable: false),
                level_at_run = table.Column<int>("integer", nullable: false),
                receipt = table.Column<string>("text", nullable: true),
                error_message = table.Column<string>("text", nullable: true),
                executed_at = table.Column<DateTimeOffset>("timestamptz", nullable: true)
            },
            constraints: table => { table.PrimaryKey("pk_job_runs", x => x.id); }
        );

        migrationBuilder.CreateIndex("ix_job_runs_tenant_id", "job_runs", "tenant_id");
        migrationBuilder.CreateIndex("ix_job_runs_tenant_id_job_type_trigger_reference", "job_runs", ["tenant_id", "job_type", "trigger_reference"], unique: true);

        migrationBuilder.CreateTable(
            "tenant_job_policies",
            table => new
            {
                tenant_id = table.Column<long>("bigint", nullable: false),
                id = table.Column<string>("text", nullable: false),
                created_at = table.Column<DateTimeOffset>("timestamptz", nullable: false),
                modified_at = table.Column<DateTimeOffset>("timestamptz", nullable: true),
                job_type = table.Column<string>("text", nullable: false),
                level = table.Column<int>("integer", nullable: false),
                approvals_streak = table.Column<int>("integer", nullable: false),
                daily_action_cap = table.Column<int>("integer", nullable: false)
            },
            constraints: table => { table.PrimaryKey("pk_tenant_job_policies", x => x.id); }
        );

        migrationBuilder.CreateIndex("ix_tenant_job_policies_tenant_id", "tenant_job_policies", "tenant_id");
        migrationBuilder.CreateIndex("ix_tenant_job_policies_tenant_id_job_type", "tenant_job_policies", ["tenant_id", "job_type"], unique: true);
    }
}
