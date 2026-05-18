using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Main.Database.Migrations;

[DbContext(typeof(MainDbContext))]
[Migration("20260518120000_AddCalComBookingLifecycleFields")]
public sealed class AddCalComBookingLifecycleFields : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>("title", "bookings", "text", nullable: false, defaultValue: "");
        migrationBuilder.AddColumn<string>("description", "bookings", "text", nullable: true);
        migrationBuilder.AddColumn<string>("location_type", "bookings", "text", nullable: true);
        migrationBuilder.AddColumn<string>("location_value", "bookings", "text", nullable: true);
        migrationBuilder.AddColumn<string>("metadata_json", "bookings", "jsonb", nullable: false, defaultValue: "{}");
        migrationBuilder.AddColumn<string>("attendees_json", "bookings", "jsonb", nullable: false, defaultValue: "[]");
        migrationBuilder.AddColumn<string>("references_json", "bookings", "jsonb", nullable: false, defaultValue: "[]");
        migrationBuilder.AddColumn<string>("seat_references_json", "bookings", "jsonb", nullable: false, defaultValue: "[]");
        migrationBuilder.AddColumn<string>("cancellation_reason", "bookings", "text", nullable: true);
        migrationBuilder.AddColumn<string>("rejection_reason", "bookings", "text", nullable: true);
        migrationBuilder.AddColumn<string>("reschedule_reason", "bookings", "text", nullable: true);
        migrationBuilder.AddColumn<bool>("rescheduled", "bookings", "boolean", nullable: false, defaultValue: false);
        migrationBuilder.AddColumn<string>("from_reschedule", "bookings", "text", nullable: true);
        migrationBuilder.AddColumn<string>("cancelled_by", "bookings", "text", nullable: true);
        migrationBuilder.AddColumn<string>("rescheduled_by", "bookings", "text", nullable: true);

        migrationBuilder.Sql(
            """
            UPDATE bookings
            SET title = event_types.title,
                description = event_types.description,
                location_type = event_types.location_type,
                location_value = event_types.location_value,
                attendees_json = jsonb_build_array(jsonb_build_object(
                    'Name', bookings.booker_name,
                    'Email', bookings.booker_email,
                    'TimeZone', bookings.time_zone,
                    'PhoneNumber', NULL,
                    'Locale', NULL,
                    'NoShow', false
                ))
            FROM event_types
            WHERE bookings.event_type_id = event_types.id
            """
        );

        migrationBuilder.CreateIndex("ix_bookings_tenant_id_status_start_time", "bookings", ["tenant_id", "status", "start_time"]);
        migrationBuilder.CreateIndex("ix_bookings_tenant_id_booker_email", "bookings", ["tenant_id", "booker_email"]);
    }
}
