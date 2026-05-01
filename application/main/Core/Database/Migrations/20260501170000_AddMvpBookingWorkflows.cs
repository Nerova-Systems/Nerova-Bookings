using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Main.Database.Migrations;

[DbContext(typeof(MainDbContext))]
[Migration("20260501170000_AddMvpBookingWorkflows")]
public sealed class AddMvpBookingWorkflows : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "appointment_participants",
            columns: table => new
            {
                id = table.Column<string>(type: "text", nullable: false),
                tenant_id = table.Column<long>(type: "bigint", nullable: false),
                appointment_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                client_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                role = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_appointment_participants", x => x.id);
            }
        );

        migrationBuilder.CreateTable(
            name: "appointment_reschedule_requests",
            columns: table => new
            {
                id = table.Column<string>(type: "text", nullable: false),
                tenant_id = table.Column<long>(type: "bigint", nullable: false),
                appointment_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                token_hash = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                proposed_start_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                proposed_end_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                note = table.Column<string>(type: "character varying(800)", maxLength: 800, nullable: false),
                status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                notification_channel = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                responded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_appointment_reschedule_requests", x => x.id);
            }
        );

        migrationBuilder.CreateTable(
            name: "appointment_external_calendar_events",
            columns: table => new
            {
                id = table.Column<string>(type: "text", nullable: false),
                tenant_id = table.Column<long>(type: "bigint", nullable: false),
                appointment_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                provider = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                calendar_id = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                external_event_id = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                meet_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                last_synced_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_appointment_external_calendar_events", x => x.id);
            }
        );

        migrationBuilder.CreateTable(
            name: "integration_calendars",
            columns: table => new
            {
                id = table.Column<string>(type: "text", nullable: false),
                tenant_id = table.Column<long>(type: "bigint", nullable: false),
                integration_connection_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                external_calendar_id = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                name = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                is_primary = table.Column<bool>(type: "boolean", nullable: false),
                can_write = table.Column<bool>(type: "boolean", nullable: false),
                add_events_to_calendar = table.Column<bool>(type: "boolean", nullable: false),
                check_for_conflicts = table.Column<bool>(type: "boolean", nullable: false),
                last_synced_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_integration_calendars", x => x.id);
            }
        );

        migrationBuilder.CreateIndex("ix_appointment_participants_tenant_id_appointment_id_client_id", "appointment_participants", ["tenant_id", "appointment_id", "client_id"], unique: true);
        migrationBuilder.CreateIndex("ix_appointment_reschedule_requests_tenant_id_appointment_id_status", "appointment_reschedule_requests", ["tenant_id", "appointment_id", "status"]);
        migrationBuilder.CreateIndex("ix_appointment_reschedule_requests_token_hash", "appointment_reschedule_requests", "token_hash", unique: true);
        migrationBuilder.CreateIndex("ix_appointment_external_calendar_events_tenant_id_appointment_id_provider", "appointment_external_calendar_events", ["tenant_id", "appointment_id", "provider"], unique: true);
        migrationBuilder.CreateIndex("ix_integration_calendars_tenant_id_integration_connection_id_external_calendar_id", "integration_calendars", ["tenant_id", "integration_connection_id", "external_calendar_id"], unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("integration_calendars");
        migrationBuilder.DropTable("appointment_external_calendar_events");
        migrationBuilder.DropTable("appointment_reschedule_requests");
        migrationBuilder.DropTable("appointment_participants");
    }
}
