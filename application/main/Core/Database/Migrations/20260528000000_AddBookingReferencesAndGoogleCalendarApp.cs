using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Main.Database.Migrations;

[DbContext(typeof(MainDbContext))]
[Migration("20260528000000_AddBookingReferencesAndGoogleCalendarApp")]
public sealed class AddBookingReferencesAndGoogleCalendarApp : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            "booking_references",
            table => new
            {
                tenant_id = table.Column<long>("bigint", nullable: false),
                id = table.Column<string>("text", nullable: false),
                created_at = table.Column<DateTimeOffset>("timestamptz", nullable: false),
                modified_at = table.Column<DateTimeOffset>("timestamptz", nullable: true),
                booking_id = table.Column<string>("text", nullable: false),
                app_slug = table.Column<string>("text", nullable: false),
                external_id = table.Column<string>("character varying(400)", maxLength: 400, nullable: false),
                external_url = table.Column<string>("character varying(1000)", maxLength: 1000, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_booking_references", reference => reference.id);
                table.ForeignKey(
                    "fk_booking_references_bookings_booking_id",
                    reference => reference.booking_id,
                    "bookings",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade
                );
            }
        );

        migrationBuilder.CreateIndex(
            "ix_booking_references_booking_id_app_slug",
            "booking_references",
            ["booking_id", "app_slug"],
            unique: true
        );
        migrationBuilder.CreateIndex("ix_booking_references_app_slug", "booking_references", "app_slug");

        // Seed the google-calendar app row so it appears in GET /api/apps.
        migrationBuilder.Sql(
            """
            INSERT INTO apps (id, created_at, name, category, description, logo_url, is_active)
            VALUES ('google-calendar', '2026-05-28 00:00:00+00', 'Google Calendar', 'Calendar',
                    'Sync bookings to a connected Google Calendar and respect existing busy time when offering availability.',
                    '/apps/google-calendar/logo.svg', true)
            ON CONFLICT (id) DO NOTHING;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DELETE FROM apps WHERE id = 'google-calendar';");
        migrationBuilder.DropTable("booking_references");
    }
}
