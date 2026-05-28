using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Main.Database.Migrations;

/// <summary>
///     Phase 4b — adds the <c>payment_reminder_sent_at</c> column to <c>bookings</c>. The
///     post-session payment reminder job uses this timestamp as a re-entrancy guard so
///     repeated polling does not re-send WhatsApp reminders for the same booking.
/// </summary>
[DbContext(typeof(MainDbContext))]
[Migration("20260604000000_AddBookingPaymentReminderTimestamp")]
public sealed class AddBookingPaymentReminderTimestamp : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTimeOffset>(
            "payment_reminder_sent_at",
            "bookings",
            "timestamptz",
            nullable: true
        );
    }
}
