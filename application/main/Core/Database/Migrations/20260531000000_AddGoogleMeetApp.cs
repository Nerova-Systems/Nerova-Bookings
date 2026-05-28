using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Main.Database.Migrations;

[DbContext(typeof(MainDbContext))]
[Migration("20260531000000_AddGoogleMeetApp")]
public sealed class AddGoogleMeetApp : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Seed the google-meet app row so it appears in GET /api/apps. No new credentials table:
        // Google Meet piggy-backs on the google-calendar credential (see GoogleMeetInstaller),
        // so the connector creates an AppInstallation row only — no Credential row is written.
        migrationBuilder.Sql(
            """
            INSERT INTO apps (id, created_at, name, category, description, logo_url, is_active)
            VALUES ('google-meet', '2026-05-31 00:00:00+00', 'Google Meet', 'Conferencing',
                    'Generate a Google Meet link for every booking — reuses your Google Calendar connection (no separate sign-in).',
                    '/apps/google-meet/logo.svg', true)
            ON CONFLICT (id) DO NOTHING;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DELETE FROM apps WHERE id = 'google-meet';");
    }
}
