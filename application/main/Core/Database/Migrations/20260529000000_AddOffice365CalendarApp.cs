using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Main.Database.Migrations;

[DbContext(typeof(MainDbContext))]
[Migration("20260529000000_AddOffice365CalendarApp")]
public sealed class AddOffice365CalendarApp : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Seed the office365-calendar app row so it appears in GET /api/apps. The
        // booking_references table itself was created by the earlier
        // AddBookingReferencesAndGoogleCalendarApp migration and is shared across all
        // calendar/video connectors.
        migrationBuilder.Sql(
            """
            INSERT INTO apps (id, created_at, name, category, description, logo_url, is_active)
            VALUES ('office365-calendar', '2026-05-29 00:00:00+00', 'Microsoft Office 365 Calendar', 'Calendar',
                    'Sync bookings to a connected Outlook calendar and respect existing busy time when offering availability.',
                    '/apps/office365-calendar/logo.svg', true)
            ON CONFLICT (id) DO NOTHING;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DELETE FROM apps WHERE id = 'office365-calendar';");
    }
}
