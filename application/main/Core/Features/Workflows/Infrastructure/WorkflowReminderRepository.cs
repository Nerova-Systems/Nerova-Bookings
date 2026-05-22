using Main.Database;
using Main.Features.Scheduling.Domain;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Domain;
using SharedKernel.Persistence;

namespace Main.Features.Workflows.Domain;

public interface IWorkflowReminderRepository : ICrudRepository<WorkflowReminder, WorkflowReminderId>
{
    Task<WorkflowReminder[]> GetPendingDueAsync(DateTimeOffset now, int batchSize, CancellationToken cancellationToken);

    Task<bool> HasRemindersForBookingAsync(BookingId bookingId, CancellationToken cancellationToken);

    Task<WorkflowReminder[]> GetPendingForBookingAsync(BookingId bookingId, CancellationToken cancellationToken);

    void UpdateRange(WorkflowReminder[] reminders);
}

public sealed class WorkflowReminderRepository(MainDbContext context)
    : RepositoryBase<WorkflowReminder, WorkflowReminderId>(context), IWorkflowReminderRepository
{
    /// <summary>
    ///     Returns pending reminders due for dispatch across all tenants.
    ///     Uses IgnoreQueryFilters() because this runs in a background job without a tenant context.
    /// </summary>
    public async Task<WorkflowReminder[]> GetPendingDueAsync(DateTimeOffset now, int batchSize, CancellationToken cancellationToken)
    {
        return await DbSet
            .IgnoreQueryFilters()
            .Where(r => r.Status == WorkflowReminderStatus.Pending && r.ScheduledDate <= now)
            .OrderBy(r => r.ScheduledDate)
            .Take(batchSize)
            .ToArrayAsync(cancellationToken);
    }

    public async Task<bool> HasRemindersForBookingAsync(BookingId bookingId, CancellationToken cancellationToken)
    {
        return await DbSet
            .IgnoreQueryFilters()
            .AnyAsync(r => r.BookingId == bookingId, cancellationToken);
    }

    public async Task<WorkflowReminder[]> GetPendingForBookingAsync(BookingId bookingId, CancellationToken cancellationToken)
    {
        return await DbSet
            .IgnoreQueryFilters()
            .Where(r => r.BookingId == bookingId && r.Status == WorkflowReminderStatus.Pending)
            .ToArrayAsync(cancellationToken);
    }

    public new void UpdateRange(WorkflowReminder[] reminders)
    {
        base.UpdateRange(reminders);
    }
}
