using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Main.Database.Migrations;

[DbContext(typeof(MainDbContext))]
[Migration("20260519120000_AddCalComBookingSideEffects")]
public sealed class AddCalComBookingSideEffects : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            "workflows",
            table => new
            {
                tenant_id = table.Column<long>("bigint", nullable: false),
                id = table.Column<string>("text", nullable: false),
                owner_user_id = table.Column<string>("text", nullable: false),
                event_type_id = table.Column<string>("text", nullable: false),
                created_at = table.Column<DateTimeOffset>("timestamptz", nullable: false),
                modified_at = table.Column<DateTimeOffset>("timestamptz", nullable: true),
                name = table.Column<string>("text", nullable: false),
                active = table.Column<bool>("boolean", nullable: false),
                trigger = table.Column<string>("text", nullable: false),
                scheduled_offset_minutes = table.Column<int>("integer", nullable: true),
                steps_json = table.Column<string>("jsonb", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_workflows", x => x.id);
                table.ForeignKey("fk_workflows_event_types_event_type_id", x => x.event_type_id, "event_types", "id", onDelete: ReferentialAction.Restrict);
            }
        );

        migrationBuilder.CreateTable(
            "webhook_subscriptions",
            table => new
            {
                tenant_id = table.Column<long>("bigint", nullable: false),
                id = table.Column<string>("text", nullable: false),
                owner_user_id = table.Column<string>("text", nullable: false),
                event_type_id = table.Column<string>("text", nullable: false),
                created_at = table.Column<DateTimeOffset>("timestamptz", nullable: false),
                modified_at = table.Column<DateTimeOffset>("timestamptz", nullable: true),
                active = table.Column<bool>("boolean", nullable: false),
                subscriber_url = table.Column<string>("text", nullable: false),
                secret = table.Column<string>("text", nullable: true),
                triggers_json = table.Column<string>("jsonb", nullable: false),
                payload_format = table.Column<string>("text", nullable: false),
                payload_version = table.Column<string>("text", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_webhook_subscriptions", x => x.id);
                table.ForeignKey("fk_webhook_subscriptions_event_types_event_type_id", x => x.event_type_id, "event_types", "id", onDelete: ReferentialAction.Restrict);
            }
        );

        migrationBuilder.CreateTable(
            "booking_side_effect_deliveries",
            table => new
            {
                tenant_id = table.Column<long>("bigint", nullable: false),
                id = table.Column<string>("text", nullable: false),
                booking_id = table.Column<string>("text", nullable: false),
                event_type_id = table.Column<string>("text", nullable: false),
                created_at = table.Column<DateTimeOffset>("timestamptz", nullable: false),
                modified_at = table.Column<DateTimeOffset>("timestamptz", nullable: true),
                trigger = table.Column<string>("text", nullable: false),
                kind = table.Column<string>("text", nullable: false),
                status = table.Column<string>("text", nullable: false),
                attempts = table.Column<int>("integer", nullable: false),
                next_retry_at = table.Column<DateTimeOffset>("timestamptz", nullable: true),
                last_error = table.Column<string>("text", nullable: true),
                payload_json = table.Column<string>("jsonb", nullable: false),
                dedupe_key = table.Column<string>("text", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_booking_side_effect_deliveries", x => x.id);
                table.ForeignKey("fk_booking_side_effect_deliveries_event_types_event_type_id", x => x.event_type_id, "event_types", "id", onDelete: ReferentialAction.Restrict);
            }
        );

        migrationBuilder.CreateIndex("ix_workflows_tenant_id_event_type_id_trigger", "workflows", ["tenant_id", "event_type_id", "trigger"]);
        migrationBuilder.CreateIndex("ix_workflows_event_type_id", "workflows", "event_type_id");
        migrationBuilder.CreateIndex("ix_webhook_subscriptions_tenant_id_event_type_id", "webhook_subscriptions", ["tenant_id", "event_type_id"]);
        migrationBuilder.CreateIndex("ix_webhook_subscriptions_event_type_id", "webhook_subscriptions", "event_type_id");
        migrationBuilder.CreateIndex("ix_booking_side_effect_deliveries_status_next_retry_at", "booking_side_effect_deliveries", ["status", "next_retry_at"]);
        migrationBuilder.CreateIndex("ix_booking_side_effect_deliveries_dedupe_key", "booking_side_effect_deliveries", "dedupe_key", unique: true);
        migrationBuilder.CreateIndex("ix_booking_side_effect_deliveries_event_type_id", "booking_side_effect_deliveries", "event_type_id");
    }
}
