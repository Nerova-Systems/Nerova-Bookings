using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Main.Database.Migrations;

[DbContext(typeof(MainDbContext))]
[Migration("20260607134330_AddWhatsAppConversations")]
public sealed class AddWhatsAppConversations : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            "whats_app_conversations",
            table => new
            {
                tenant_id = table.Column<long>("bigint", nullable: false),
                id = table.Column<string>("text", nullable: false),
                draft_booking_id = table.Column<string>("text", nullable: true),
                created_at = table.Column<DateTimeOffset>("timestamptz", nullable: false),
                modified_at = table.Column<DateTimeOffset>("timestamptz", nullable: true),
                customer_phone_number = table.Column<string>("text", nullable: false),
                state = table.Column<string>("text", nullable: false),
                active_flow_token = table.Column<string>("text", nullable: true),
                last_inbound_at = table.Column<DateTimeOffset>("timestamptz", nullable: false),
                expires_at = table.Column<DateTimeOffset>("timestamptz", nullable: false)
            },
            constraints: table => { table.PrimaryKey("pk_whats_app_conversations", x => x.id); }
        );

        migrationBuilder.CreateIndex("ix_whats_app_conversations_tenant_id", "whats_app_conversations", "tenant_id");

        // One active conversation row per customer phone number within a tenant.
        migrationBuilder.CreateIndex(
            "ix_whats_app_conversations_tenant_id_customer_phone_number",
            "whats_app_conversations",
            ["tenant_id", "customer_phone_number"],
            unique: true
        );
    }
}
