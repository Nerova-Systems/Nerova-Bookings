using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Main.Database.Migrations;

[DbContext(typeof(MainDbContext))]
[Migration("20260601104927_AddWhatsAppBusinessAccounts")]
public sealed class AddWhatsAppBusinessAccounts : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            "whats_app_business_accounts",
            table => new
            {
                tenant_id = table.Column<long>("bigint", nullable: false),
                id = table.Column<string>("text", nullable: false),
                created_at = table.Column<DateTimeOffset>("timestamptz", nullable: false),
                modified_at = table.Column<DateTimeOffset>("timestamptz", nullable: true),
                meta_waba_id = table.Column<string>("text", nullable: false),
                business_name = table.Column<string>("text", nullable: false),
                access_token = table.Column<string>("text", nullable: false),
                status = table.Column<string>("text", nullable: false),
                phone_number = table.Column<string>("jsonb", nullable: false)
            },
            constraints: table => { table.PrimaryKey("pk_whats_app_business_accounts", x => x.id); }
        );

        // One WhatsApp Business Account per tenant.
        migrationBuilder.CreateIndex("ix_whats_app_business_accounts_tenant_id", "whats_app_business_accounts", "tenant_id", unique: true);
    }
}
