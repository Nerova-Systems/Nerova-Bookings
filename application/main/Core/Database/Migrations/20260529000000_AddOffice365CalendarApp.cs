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
        migrationBuilder.InsertData(
            "apps",
            ["id", "created_at", "name", "category", "description", "logo_url", "is_active"],
            [
                "office365-calendar",
                new DateTimeOffset(2026, 5, 29, 0, 0, 0, TimeSpan.Zero),
                "Microsoft Office 365 Calendar",
                "Calendar",
                "Sync bookings to a connected Outlook calendar and respect existing busy time when offering availability.",
                "/apps/office365-calendar/logo.svg",
                true
            ]
        );
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DeleteData("apps", "id", "office365-calendar");
    }
}
