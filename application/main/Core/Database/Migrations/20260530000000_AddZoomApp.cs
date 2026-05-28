using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Main.Database.Migrations;

[DbContext(typeof(MainDbContext))]
[Migration("20260530000000_AddZoomApp")]
public sealed class AddZoomApp : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Seed the zoom app row so it appears in GET /api/apps. The booking_references table
        // is shared from the earlier AddBookingReferencesAndGoogleCalendarApp migration —
        // the Zoom connector reuses it to persist meeting ids alongside the booking.
        migrationBuilder.Sql(
            """
            INSERT INTO apps (id, created_at, name, category, description, logo_url, is_active)
            VALUES ('zoom', '2026-05-30 00:00:00+00', 'Zoom', 'Conferencing',
                    'Generate a Zoom meeting link for every booking and attach it to the calendar invite.',
                    '/apps/zoom/logo.svg', true)
            ON CONFLICT (id) DO NOTHING;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DELETE FROM apps WHERE id = 'zoom';");
    }
}
