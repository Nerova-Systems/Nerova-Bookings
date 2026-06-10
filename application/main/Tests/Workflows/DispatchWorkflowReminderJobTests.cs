using FluentAssertions;
using Main.Database;
using Main.Features.EventTypes.Domain;
using Main.Features.Schedules.Domain;
using Main.Features.Scheduling.Domain;
using Main.Features.Workflows.Domain;
using Main.Features.Workflows.Infrastructure;
using Main.Features.Workflows.Jobs;
using Main.Features.Workflows.Senders;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using SharedKernel.Integrations.Email;
using SharedKernel.Persistence;
using Xunit;

namespace Main.Tests.Workflows;

/// <summary>
///     Exercises <see cref="DispatchWorkflowReminderJob" /> against real SMS / WhatsApp provider
///     substitutes. The reminder repository is substituted (its EF query uses a
///     <see cref="DateTimeOffset" /> filter that the SQLite test provider cannot translate); the
///     real <see cref="MainDbContext" /> from <see cref="EndpointBaseTest{TContext}" /> is used
///     only for the booking lookup the job performs.
/// </summary>
public sealed class DispatchWorkflowReminderJobTests : EndpointBaseTest<MainDbContext>
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ExecuteAsync_WhenSmsActionDue_ShouldInvokeSmsProviderAndPersistMessageId()
    {
        var bookingId = await SeedBookingAsync();
        var reminder = CreateReminder(bookingId, WorkflowAction.SmsNumber, "+15551234567", "see you soon");

        var sms = Substitute.For<ISmsProvider>();
        sms.SendAsync("+15551234567", "see you soon", Arg.Any<CancellationToken>())
            .Returns(SmsResult.Sent("SM_abc"));

        var whatsApp = Substitute.For<IWhatsAppProvider>();

        await BuildJob(sms, whatsApp, reminder).ExecuteAsync(null!, CancellationToken.None);

        await whatsApp.DidNotReceive().SendAsync(Arg.Any<TenantId>(), null!, null!, null!, CancellationToken.None);
        reminder.Status.Should().Be(WorkflowReminderStatus.Dispatched);
        reminder.ReferenceId.Should().Be("SM_abc");
        reminder.RetryCount.Should().Be(0);
    }

    [Fact]
    public async Task ExecuteAsync_WhenWhatsAppActionDue_ShouldInvokeWhatsAppProviderWithTemplate()
    {
        var bookingId = await SeedBookingAsync();
        var reminder = CreateReminder(bookingId, WorkflowAction.WhatsappNumber, "+15551234567", "rendered body");

        var whatsApp = Substitute.For<IWhatsAppProvider>();
        whatsApp.SendAsync(
                Arg.Any<TenantId>(),
                "+15551234567",
                "booking_reminder",
                Arg.Is<IReadOnlyDictionary<string, string>>(d => d["1"] == "rendered body"),
                Arg.Any<CancellationToken>()
            )
            .Returns(WhatsAppResult.Sent("wamid.x"));

        var sms = Substitute.For<ISmsProvider>();

        await BuildJob(sms, whatsApp, reminder).ExecuteAsync(null!, CancellationToken.None);

        await sms.DidNotReceive().SendAsync(null!, null!, CancellationToken.None);
        await whatsApp.Received(1).SendAsync(Arg.Any<TenantId>(), "+15551234567", "booking_reminder", Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<CancellationToken>());
        reminder.Status.Should().Be(WorkflowReminderStatus.Dispatched);
        reminder.ReferenceId.Should().Be("wamid.x");
    }

    [Fact]
    public async Task ExecuteAsync_WhenSmsProviderReturnsTransient_ShouldKeepPendingAndIncrementRetry()
    {
        var bookingId = await SeedBookingAsync();
        var reminder = CreateReminder(bookingId, WorkflowAction.SmsNumber, "+15550000001", "x");

        var sms = Substitute.For<ISmsProvider>();
        sms.SendAsync(null!, null!, CancellationToken.None).ReturnsForAnyArgs(SmsResult.Transient("HTTP 503"));

        await BuildJob(sms, Substitute.For<IWhatsAppProvider>(), reminder)
            .ExecuteAsync(null!, CancellationToken.None);

        reminder.Status.Should().Be(WorkflowReminderStatus.Pending);
        reminder.RetryCount.Should().Be(1);
        reminder.ErrorMessage.Should().Contain("503");
    }

    [Fact]
    public async Task ExecuteAsync_WhenTransientFailuresExceedMaxRetries_ShouldMarkFailed()
    {
        var bookingId = await SeedBookingAsync();
        var reminder = CreateReminder(bookingId, WorkflowAction.SmsNumber, "+15550000001", "x");

        var sms = Substitute.For<ISmsProvider>();
        sms.SendAsync(null!, null!, CancellationToken.None).ReturnsForAnyArgs(SmsResult.Transient("HTTP 503"));

        var job = BuildJob(sms, Substitute.For<IWhatsAppProvider>(), reminder);

        // Three consecutive ticks all return transient → final tick exceeds MaxRetries (3) and marks Failed.
        for (var i = 0; i < DispatchWorkflowReminderJob.MaxRetries; i++)
        {
            await job.ExecuteAsync(null!, CancellationToken.None);
        }

        reminder.Status.Should().Be(WorkflowReminderStatus.Failed);
        reminder.RetryCount.Should().Be(DispatchWorkflowReminderJob.MaxRetries);
    }

    [Fact]
    public async Task ExecuteAsync_WhenSmsNotConfigured_ShouldCancelGracefullyWithoutRetry()
    {
        var bookingId = await SeedBookingAsync();
        var reminder = CreateReminder(bookingId, WorkflowAction.SmsNumber, "+15550000001", "x");

        var sms = Substitute.For<ISmsProvider>();
        sms.SendAsync(null!, null!, CancellationToken.None).ReturnsForAnyArgs(SmsResult.NotConfigured("missing creds"));

        await BuildJob(sms, Substitute.For<IWhatsAppProvider>(), reminder)
            .ExecuteAsync(null!, CancellationToken.None);

        reminder.Status.Should().Be(WorkflowReminderStatus.Cancelled);
        reminder.RetryCount.Should().Be(0);
    }

    [Fact]
    public async Task ExecuteAsync_WhenSmsPermanentFailure_ShouldMarkFailedImmediately()
    {
        var bookingId = await SeedBookingAsync();
        var reminder = CreateReminder(bookingId, WorkflowAction.SmsNumber, "+0", "x");

        var sms = Substitute.For<ISmsProvider>();
        sms.SendAsync(null!, null!, CancellationToken.None).ReturnsForAnyArgs(SmsResult.Permanent("HTTP 400 bad number"));

        await BuildJob(sms, Substitute.For<IWhatsAppProvider>(), reminder)
            .ExecuteAsync(null!, CancellationToken.None);

        reminder.Status.Should().Be(WorkflowReminderStatus.Failed);
        reminder.ErrorMessage.Should().Contain("400");
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private async Task<BookingId> SeedBookingAsync()
    {
        using var scope = Provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MainDbContext>();
        var schedule = Schedule.Create(
            DatabaseSeeder.TenantId, DatabaseSeeder.Tenant1Owner.Id!, "Default", "UTC", true, [], []
        );
        db.Set<Schedule>().Add(schedule);
        var eventType = EventType.Create(
            DatabaseSeeder.TenantId, DatabaseSeeder.Tenant1Owner.Id!, "Reminder Test", $"reminder-{Guid.NewGuid():N}".Substring(0, 20), null, 30,
            false, schedule.Id, 0, 0, 30, 60, null, null, null
        );
        db.Set<EventType>().Add(eventType);
        var booking = Booking.Create(
            DatabaseSeeder.TenantId,
            DatabaseSeeder.Tenant1Owner.Id!,
            eventType.Id,
            Now.AddHours(2),
            30,
            0,
            0,
            "Jane Booker",
            "jane@example.com",
            "UTC",
            BookingStatus.Accepted,
            new Dictionary<string, string>()
        );
        db.Set<Booking>().Add(booking);
        await db.SaveChangesAsync();
        return booking.Id;
    }

    private WorkflowReminder CreateReminder(BookingId bookingId, WorkflowAction action, string? sendTo, string body)
    {
        return WorkflowReminder.Create(
            DatabaseSeeder.TenantId,
            WorkflowId.NewId(),
            WorkflowStepId.NewId(),
            bookingId,
            Now.AddHours(2),
            Now.AddMinutes(-1),
            action,
            WorkflowReminderTemplate.Reminder,
            sendTo,
            null,
            body
        );
    }

    private DispatchWorkflowReminderJob BuildJob(ISmsProvider sms, IWhatsAppProvider whatsApp, WorkflowReminder reminder)
    {
        var scope = Provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MainDbContext>();
        var reminderRepository = Substitute.For<IWorkflowReminderRepository>();
        // Each ExecuteAsync tick returns the same in-memory reminder (state mutates across calls).
        reminderRepository
            .GetPendingDueAsync(Arg.Any<DateTimeOffset>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(_ => reminder.Status == WorkflowReminderStatus.Pending ? [reminder] : []);
        var unitOfWork = Substitute.For<IUnitOfWork>();
        var emailClient = Substitute.For<IEmailClient>();
        var hostEmailProvider = Substitute.For<IHostEmailProvider>();
        return new DispatchWorkflowReminderJob(
            reminderRepository,
            db,
            emailClient,
            sms,
            whatsApp,
            hostEmailProvider,
            unitOfWork,
            NullLogger<DispatchWorkflowReminderJob>.Instance
        );
    }
}
