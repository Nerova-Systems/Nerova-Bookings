using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Main.Database.Migrations;

[DbContext(typeof(MainDbContext))]
[Migration("20260601120338_AddWhatsAppMessaging")]
public sealed class AddWhatsAppMessaging : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            "whats_app_messages",
            table => new
            {
                tenant_id = table.Column<long>("bigint", nullable: false),
                id = table.Column<string>("text", nullable: false),
                created_at = table.Column<DateTimeOffset>("timestamptz", nullable: false),
                modified_at = table.Column<DateTimeOffset>("timestamptz", nullable: true),
                meta_message_id = table.Column<string>("text", nullable: false),
                direction = table.Column<string>("text", nullable: false),
                from_phone_number = table.Column<string>("text", nullable: false),
                to_phone_number = table.Column<string>("text", nullable: false),
                text = table.Column<string>("text", nullable: false),
                status = table.Column<string>("text", nullable: false),
                timestamp = table.Column<DateTimeOffset>("timestamptz", nullable: false)
            },
            constraints: table => { table.PrimaryKey("pk_whats_app_messages", x => x.id); }
        );

        migrationBuilder.CreateIndex("ix_whats_app_messages_tenant_id", "whats_app_messages", "tenant_id");
        migrationBuilder.CreateIndex("ix_whats_app_messages_meta_message_id", "whats_app_messages", "meta_message_id");

        migrationBuilder.CreateTable(
            "whats_app_events",
            table => new
            {
                id = table.Column<string>("text", nullable: false),
                created_at = table.Column<DateTimeOffset>("timestamptz", nullable: false),
                modified_at = table.Column<DateTimeOffset>("timestamptz", nullable: true),
                meta_event_id = table.Column<string>("text", nullable: false),
                status = table.Column<string>("text", nullable: false),
                payload = table.Column<string>("jsonb", nullable: false),
                processed_at = table.Column<DateTimeOffset>("timestamptz", nullable: true),
                error = table.Column<string>("text", nullable: true)
            },
            constraints: table => { table.PrimaryKey("pk_whats_app_events", x => x.id); }
        );

        // Unique index on meta_event_id ensures duplicate webhook deliveries are rejected at the database level
        migrationBuilder.CreateIndex("ix_whats_app_events_meta_event_id", "whats_app_events", "meta_event_id", unique: true);
    }
}
