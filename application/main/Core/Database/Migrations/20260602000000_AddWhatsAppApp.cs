using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Main.Database.Migrations;

[DbContext(typeof(MainDbContext))]
[Migration("20260602000000_AddWhatsAppApp")]
public sealed class AddWhatsAppApp : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Seed the whatsapp app row so it appears in GET /api/apps. No duplicate credentials table needed.
        migrationBuilder.Sql(
            """
            INSERT INTO apps (id, created_at, name, category, description, logo_url, is_active)
            VALUES ('whatsapp', '2026-06-02 00:00:00+00', 'WhatsApp Business', 'Other',
                    'Enable secure Meta Embedded Signup, WhatsApp Business Profile configuration, and pre-built booking workflows.',
                    '/apps/whatsapp/logo.svg', true)
            ON CONFLICT (id) DO NOTHING;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DELETE FROM apps WHERE id = 'whatsapp';");
    }
}
