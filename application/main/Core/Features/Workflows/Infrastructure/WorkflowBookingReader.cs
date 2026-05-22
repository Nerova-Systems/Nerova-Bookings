using Main.Database;
using Main.Features.Scheduling.Domain;
using Microsoft.EntityFrameworkCore;

namespace Main.Features.Workflows.Domain;

/// <summary>
///     Read-only cross-tenant booking reader for the workflow scheduler job.
///     Bypasses all query filters to enable background processing across all tenants.
/// </summary>
public sealed class WorkflowBookingReader(MainDbContext context)
{
    private const int BatchSize = 100;

    /// <summary>
    ///     Returns bookings that are bound to an active workflow but have no reminders created yet.
    /// </summary>
    public async Task<BookingWithWorkflows[]> GetNewBookingsAsync(CancellationToken ct)
    {
        var activeEventTypeIds = await context.Set<WorkflowEventTypeBinding>()
            .IgnoreQueryFilters()
            .Join(
                context.Set<Workflow>().IgnoreQueryFilters().Where(w => w.DeletedAt == null),
                b => b.WorkflowId,
                w => w.Id,
                (b, _) => b.EventTypeId
            )
            .Distinct()
            .ToArrayAsync(ct);

        if (activeEventTypeIds.Length == 0) return [];

        var newBookings = await context.Set<Booking>()
            .IgnoreQueryFilters()
            .Where(b => activeEventTypeIds.Contains(b.EventTypeId))
            .Where(b => b.Status != "cancelled" && b.Status != "rejected")
            .Where(b => !context.Set<WorkflowReminder>()
                .IgnoreQueryFilters()
                .Any(r => r.BookingId == b.Id)
            )
            .Take(BatchSize)
            .ToArrayAsync(ct);

        if (newBookings.Length == 0) return [];

        return await EnrichWithWorkflows(newBookings, ct);
    }

    /// <summary>
    ///     Returns bookings that have been cancelled but still have pending reminders.
    /// </summary>
    public async Task<Booking[]> GetCancelledBookingsWithPendingRemindersAsync(CancellationToken ct)
    {
        var bookingIdsWithPendingReminders = await context.Set<WorkflowReminder>()
            .IgnoreQueryFilters()
            .Where(r => r.Status == WorkflowReminderStatus.Pending)
            .Select(r => r.BookingId)
            .Distinct()
            .ToArrayAsync(ct);

        if (bookingIdsWithPendingReminders.Length == 0) return [];

        return await context.Set<Booking>()
            .IgnoreQueryFilters()
            .Where(b => bookingIdsWithPendingReminders.Contains(b.Id))
            .Where(b => b.Status == "cancelled")
            .Take(BatchSize)
            .ToArrayAsync(ct);
    }

    /// <summary>
    ///     Returns booking/reminder pairs where the booking start time has changed since the reminder was created.
    /// </summary>
    public async Task<RescheduledBooking[]> GetRescheduledBookingsAsync(CancellationToken ct)
    {
        var pendingReminderData = await context.Set<WorkflowReminder>()
            .IgnoreQueryFilters()
            .Where(r => r.Status == WorkflowReminderStatus.Pending)
            .Select(r => new { r.BookingId, r.BookingStartTime })
            .Distinct()
            .ToArrayAsync(ct);

        if (pendingReminderData.Length == 0) return [];

        var bookingIds = pendingReminderData.Select(r => r.BookingId).Distinct().ToArray();

        var bookings = await context.Set<Booking>()
            .IgnoreQueryFilters()
            .Where(b => bookingIds.Contains(b.Id))
            .Where(b => b.Status != "cancelled")
            .ToArrayAsync(ct);

        var bookingById = bookings.ToDictionary(b => b.Id);
        var rescheduled = new List<RescheduledBooking>();

        foreach (var reminder in pendingReminderData)
        {
            if (!bookingById.TryGetValue(reminder.BookingId, out var booking)) continue;
            if (booking.StartTime != reminder.BookingStartTime)
            {
                rescheduled.Add(new RescheduledBooking(booking, reminder.BookingStartTime));
            }
        }

        return rescheduled
            .DistinctBy(r => r.Booking.Id)
            .Take(BatchSize)
            .ToArray();
    }

    private async Task<BookingWithWorkflows[]> EnrichWithWorkflows(Booking[] bookings, CancellationToken ct)
    {
        var eventTypeIds = bookings.Select(b => b.EventTypeId).Distinct().ToArray();

        var bindingsWithWorkflows = await context.Set<WorkflowEventTypeBinding>()
            .IgnoreQueryFilters()
            .Where(b => eventTypeIds.Contains(b.EventTypeId))
            .Join(
                context.Set<Workflow>().IgnoreQueryFilters()
                    .Where(w => w.DeletedAt == null)
                    .Include("Steps"),
                b => b.WorkflowId,
                w => w.Id,
                (b, w) => new { Binding = b, Workflow = w }
            )
            .ToArrayAsync(ct);

        return bookings.Select(booking =>
            {
                var pairs = bindingsWithWorkflows
                    .Where(bw => bw.Binding.EventTypeId == booking.EventTypeId)
                    .Select(bw => new WorkflowWithBinding(bw.Workflow, bw.Binding))
                    .ToArray();
                return new BookingWithWorkflows(booking, pairs);
            }
        ).ToArray();
    }
}

public sealed record BookingWithWorkflows(Booking Booking, WorkflowWithBinding[] WorkflowBindings);

public sealed record WorkflowWithBinding(Workflow Workflow, WorkflowEventTypeBinding Binding);

public sealed record RescheduledBooking(Booking Booking, DateTimeOffset OldStartTime);
