using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Main.Database.Migrations;

[DbContext(typeof(MainDbContext))]
[Migration("20260526000000_AddWebhooks")]
public sealed class AddWebhooks : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // --- 1. webhooks table (per-tenant subscription registry) ---

        migrationBuilder.CreateTable(
            "webhooks",
            table => new
            {
                tenant_id = table.Column<long>("bigint", nullable: false),
                id = table.Column<string>("text", nullable: false),
                created_at = table.Column<DateTimeOffset>("timestamptz", nullable: false),
                modified_at = table.Column<DateTimeOffset>("timestamptz", nullable: true),
                user_id = table.Column<string>("text", nullable: true),
                event_type_id = table.Column<string>("text", nullable: true),
                target_url = table.Column<string>("character varying(2000)", maxLength: 2000, nullable: false),
                secret = table.Column<string>("character varying(200)", maxLength: 200, nullable: false),
                event_subscriptions_json = table.Column<string>("text", nullable: false),
                active = table.Column<bool>("boolean", nullable: false)
            },
            constraints: table => table.PrimaryKey("pk_webhooks", webhook => webhook.id)
        );

        migrationBuilder.CreateIndex("ix_webhooks_tenant_id_active", "webhooks", ["tenant_id", "active"]);

        // --- 2. webhook_deliveries table (per-attempt delivery log) ---

        migrationBuilder.CreateTable(
            "webhook_deliveries",
            table => new
            {
                tenant_id = table.Column<long>("bigint", nullable: false),
                id = table.Column<string>("text", nullable: false),
                created_at = table.Column<DateTimeOffset>("timestamptz", nullable: false),
                modified_at = table.Column<DateTimeOffset>("timestamptz", nullable: true),
                webhook_id = table.Column<string>("text", nullable: false),
                event_type = table.Column<string>("character varying(40)", maxLength: 40, nullable: false),
                payload_json = table.Column<string>("text", nullable: false),
                request_url = table.Column<string>("character varying(2000)", maxLength: 2000, nullable: false),
                request_headers_json = table.Column<string>("text", nullable: false),
                attempt_count = table.Column<int>("integer", nullable: false),
                last_attempt_at = table.Column<DateTimeOffset>("timestamptz", nullable: true),
                next_attempt_at = table.Column<DateTimeOffset>("timestamptz", nullable: true),
                status = table.Column<string>("character varying(20)", maxLength: 20, nullable: false),
                response_status_code = table.Column<int>("integer", nullable: true),
                response_body = table.Column<string>("text", nullable: true)
            },
            constraints: table => table.PrimaryKey("pk_webhook_deliveries", delivery => delivery.id)
        );

        migrationBuilder.CreateIndex(
            "ix_webhook_deliveries_status_next_attempt_at",
            "webhook_deliveries",
            ["status", "next_attempt_at"]
        );
        migrationBuilder.CreateIndex("ix_webhook_deliveries_webhook_id", "webhook_deliveries", "webhook_id");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("webhook_deliveries");
        migrationBuilder.DropTable("webhooks");
    }
}
