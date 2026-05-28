using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Main.Database.Migrations;

[DbContext(typeof(MainDbContext))]
[Migration("20260523120000_ExpandBookingForCalcomParity")]
public sealed class ExpandBookingForCalcomParity : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // --- 1. New columns on bookings (cal.com parity) -- idempotent via IF NOT EXISTS ---
        migrationBuilder.Sql("ALTER TABLE bookings ADD COLUMN IF NOT EXISTS cancellation_reason text");
        migrationBuilder.Sql("ALTER TABLE bookings ADD COLUMN IF NOT EXISTS rejection_reason text");
        migrationBuilder.Sql("ALTER TABLE bookings ADD COLUMN IF NOT EXISTS reassign_reason text");
        migrationBuilder.Sql("ALTER TABLE bookings ADD COLUMN IF NOT EXISTS reassign_by_user_id text");
        migrationBuilder.Sql("ALTER TABLE bookings ADD COLUMN IF NOT EXISTS rescheduled boolean NOT NULL DEFAULT FALSE");
        migrationBuilder.Sql("ALTER TABLE bookings ADD COLUMN IF NOT EXISTS from_reschedule_uid text");
        migrationBuilder.Sql("ALTER TABLE bookings ADD COLUMN IF NOT EXISTS cancelled_by_user_uid text");
        migrationBuilder.Sql("ALTER TABLE bookings ADD COLUMN IF NOT EXISTS rescheduled_by_user_uid text");
        migrationBuilder.Sql("ALTER TABLE bookings ADD COLUMN IF NOT EXISTS sms_reminder_number text");
        migrationBuilder.Sql("ALTER TABLE bookings ADD COLUMN IF NOT EXISTS i_cal_uid text");
        migrationBuilder.Sql("ALTER TABLE bookings ADD COLUMN IF NOT EXISTS i_cal_sequence integer NOT NULL DEFAULT 0");
        migrationBuilder.Sql("ALTER TABLE bookings ADD COLUMN IF NOT EXISTS rating integer");
        migrationBuilder.Sql("ALTER TABLE bookings ADD COLUMN IF NOT EXISTS rating_feedback text");
        migrationBuilder.Sql("ALTER TABLE bookings ADD COLUMN IF NOT EXISTS no_show_host boolean");
        migrationBuilder.Sql("ALTER TABLE bookings ADD COLUMN IF NOT EXISTS one_time_password text");
        migrationBuilder.Sql("ALTER TABLE bookings ADD COLUMN IF NOT EXISTS is_recorded boolean NOT NULL DEFAULT FALSE");
        migrationBuilder.Sql("ALTER TABLE bookings ADD COLUMN IF NOT EXISTS custom_inputs_json jsonb");
        migrationBuilder.Sql("ALTER TABLE bookings ADD COLUMN IF NOT EXISTS metadata_json jsonb");
        migrationBuilder.Sql("ALTER TABLE bookings ADD COLUMN IF NOT EXISTS location_type text");
        migrationBuilder.Sql("ALTER TABLE bookings ADD COLUMN IF NOT EXISTS location_value text");

        // Backfill iCalUid for existing bookings so the unique index can be built.
        migrationBuilder.Sql("UPDATE bookings SET i_cal_uid = id || '@nerova' WHERE i_cal_uid IS NULL");

        migrationBuilder.Sql(
            "CREATE UNIQUE INDEX IF NOT EXISTS ix_bookings_tenant_id_i_cal_uid ON bookings (tenant_id, i_cal_uid)"
        );

        // --- 2. booking_attendees table ---

        migrationBuilder.CreateTable(
            "booking_attendees",
            table => new
            {
                tenant_id = table.Column<long>("bigint", nullable: false),
                id = table.Column<string>("text", nullable: false),
                booking_id = table.Column<string>("text", nullable: false),
                created_at = table.Column<DateTimeOffset>("timestamptz", nullable: false),
                modified_at = table.Column<DateTimeOffset>("timestamptz", nullable: true),
                name = table.Column<string>("text", nullable: false),
                email = table.Column<string>("text", nullable: false),
                time_zone = table.Column<string>("text", nullable: false),
                locale = table.Column<string>("text", nullable: false),
                no_show = table.Column<bool>("boolean", nullable: false, defaultValue: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_booking_attendees", attendee => attendee.id);
                table.ForeignKey(
                    "fk_booking_attendees_bookings_booking_id",
                    attendee => attendee.booking_id,
                    "bookings",
                    "id",
                    onDelete: ReferentialAction.Cascade
                );
            }
        );

        migrationBuilder.CreateIndex("ix_booking_attendees_booking_id", "booking_attendees", "booking_id");
        migrationBuilder.CreateIndex("ix_booking_attendees_tenant_id_email", "booking_attendees", ["tenant_id", "email"]);

        // --- 3. booking_seats table ---

        migrationBuilder.CreateTable(
            "booking_seats",
            table => new
            {
                tenant_id = table.Column<long>("bigint", nullable: false),
                id = table.Column<string>("text", nullable: false),
                booking_id = table.Column<string>("text", nullable: false),
                attendee_id = table.Column<string>("text", nullable: false),
                created_at = table.Column<DateTimeOffset>("timestamptz", nullable: false),
                modified_at = table.Column<DateTimeOffset>("timestamptz", nullable: true),
                reference_uid = table.Column<string>("text", nullable: false),
                data = table.Column<string>("jsonb", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_booking_seats", seat => seat.id);
                table.ForeignKey(
                    "fk_booking_seats_bookings_booking_id",
                    seat => seat.booking_id,
                    "bookings",
                    "id",
                    onDelete: ReferentialAction.Cascade
                );
            }
        );

        migrationBuilder.CreateIndex("ix_booking_seats_booking_id", "booking_seats", "booking_id");
        migrationBuilder.CreateIndex(
            "ix_booking_seats_booking_id_reference_uid",
            "booking_seats",
            ["booking_id", "reference_uid"],
            unique: true
        );

        // --- 4. booking_history_entries table ---

        migrationBuilder.CreateTable(
            "booking_history_entries",
            table => new
            {
                tenant_id = table.Column<long>("bigint", nullable: false),
                id = table.Column<string>("text", nullable: false),
                booking_id = table.Column<string>("text", nullable: false),
                created_at = table.Column<DateTimeOffset>("timestamptz", nullable: false),
                modified_at = table.Column<DateTimeOffset>("timestamptz", nullable: true),
                event_type = table.Column<string>("text", nullable: false),
                actor_user_id = table.Column<string>("text", nullable: true),
                payload_json = table.Column<string>("jsonb", nullable: true),
                occurred_at = table.Column<DateTimeOffset>("timestamptz", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_booking_history_entries", entry => entry.id);
                table.ForeignKey(
                    "fk_booking_history_entries_bookings_booking_id",
                    entry => entry.booking_id,
                    "bookings",
                    "id",
                    onDelete: ReferentialAction.Cascade
                );
            }
        );

        migrationBuilder.CreateIndex("ix_booking_history_entries_booking_id", "booking_history_entries", "booking_id");
        migrationBuilder.CreateIndex("ix_booking_history_entries_occurred_at", "booking_history_entries", "occurred_at");

        // --- 5. booking_internal_notes table ---

        migrationBuilder.CreateTable(
            "booking_internal_notes",
            table => new
            {
                tenant_id = table.Column<long>("bigint", nullable: false),
                id = table.Column<string>("text", nullable: false),
                booking_id = table.Column<string>("text", nullable: false),
                author_user_id = table.Column<string>("text", nullable: false),
                created_at = table.Column<DateTimeOffset>("timestamptz", nullable: false),
                modified_at = table.Column<DateTimeOffset>("timestamptz", nullable: true),
                body = table.Column<string>("text", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_booking_internal_notes", note => note.id);
                table.ForeignKey(
                    "fk_booking_internal_notes_bookings_booking_id",
                    note => note.booking_id,
                    "bookings",
                    "id",
                    onDelete: ReferentialAction.Cascade
                );
            }
        );

        migrationBuilder.CreateIndex("ix_booking_internal_notes_booking_id", "booking_internal_notes", "booking_id");
    }
}
