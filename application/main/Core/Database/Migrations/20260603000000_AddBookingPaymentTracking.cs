using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Main.Database.Migrations;

/// <summary>
///     Phase 4 — adds payment-tracking columns to <c>bookings</c> and creates the
///     <c>processed_payment_events</c> table used for Paystack webhook idempotency.
/// </summary>
[DbContext(typeof(MainDbContext))]
[Migration("20260603000000_AddBookingPaymentTracking")]
public sealed class AddBookingPaymentTracking : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            "payment_reference",
            "bookings",
            "character varying(120)",
            maxLength: 120,
            nullable: true
        );

        migrationBuilder.AddColumn<string>(
            "payment_link_url",
            "bookings",
            "character varying(2000)",
            maxLength: 2000,
            nullable: true
        );

        migrationBuilder.AddColumn<string>(
            "payment_status",
            "bookings",
            "character varying(40)",
            maxLength: 40,
            nullable: false,
            defaultValue: "NotRequired"
        );

        migrationBuilder.AddColumn<DateTimeOffset>(
            "payment_state_changed_at",
            "bookings",
            "timestamptz",
            nullable: true
        );

        // Idempotency table — populated as Paystack webhooks are processed. The event id is unique
        // per Paystack event (or, when absent, a SHA-256 of the payload) so re-deliveries are no-ops.
        migrationBuilder.CreateTable(
            "processed_payment_events",
            table => new
            {
                event_id = table.Column<string>("character varying(128)", maxLength: 128, nullable: false),
                processed_at = table.Column<DateTimeOffset>("timestamptz", nullable: false)
            },
            constraints: table => { table.PrimaryKey("pk_processed_payment_events", x => x.event_id); }
        );

        // Speeds up the unpaid-booking release polling job.
        migrationBuilder.CreateIndex(
            "ix_bookings_payment_status_payment_state_changed_at",
            "bookings",
            new[] { "payment_status", "payment_state_changed_at" }
        );
    }
}
