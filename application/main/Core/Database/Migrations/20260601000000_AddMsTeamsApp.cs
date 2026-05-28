using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Main.Database.Migrations;

[DbContext(typeof(MainDbContext))]
[Migration("20260601000000_AddMsTeamsApp")]
public sealed class AddMsTeamsApp : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Seed the ms-teams app row so it appears in GET /api/apps. No new credentials table:
        // Microsoft Teams piggy-backs on the office365-calendar credential (see
        // MsTeamsInstaller), so the connector creates an AppInstallation row only — no
        // Credential row is written.
        migrationBuilder.Sql(
            """
            INSERT INTO apps (id, created_at, name, category, description, logo_url, is_active)
            VALUES ('ms-teams', '2026-06-01 00:00:00+00', 'Microsoft Teams', 'Conferencing',
                    'Generate a Microsoft Teams meeting link for every booking — reuses your Office 365 connection (no separate sign-in).',
                    '/apps/ms-teams/logo.svg', true)
            ON CONFLICT (id) DO NOTHING;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DELETE FROM apps WHERE id = 'ms-teams';");
    }
}
