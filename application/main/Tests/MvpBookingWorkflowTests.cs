using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using Main.Database;
using Main.Features.Appointments;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;
using SharedKernel.Domain;
using SharedKernel.Integrations.Email;
using Xunit;

namespace Main.Tests;

public sealed class MvpBookingWorkflowTests : EndpointBaseTest<MainDbContext>
{
    public MvpBookingWorkflowTests()
    {
        FakeNangoClient.Reset();
        FakeWhatsAppClient.Reset();
    }

    [Fact]
    public async Task CreateRescheduleRequest_ShouldNotifyClientWithoutMovingAppointment()
    {
        var appointment = await SeedAndReadFirstAppointmentAsync();
        var originalStart = appointment.StartAt;
        var proposedStart = DateTimeOffset.Parse("2026-05-13T09:00:00+02:00");

        var response = await AuthenticatedOwnerHttpClient.PostAsJsonAsync(
            $"/api/main/app/appointments/{appointment.Id}/reschedule-requests",
            new { proposedStartAt = proposedStart, note = "Can we move this booking?" }
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonObject>();
        body!["approvalUrl"]!.GetValue<string>().Should().Contain("/book/approval/");

        using var scope = Provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MainDbContext>();
        var reloaded = await db.Appointments.IgnoreQueryFilters().SingleAsync(item => item.Id == appointment.Id);
        reloaded.StartAt.Should().Be(originalStart);

        var request = await db.AppointmentRescheduleRequests.IgnoreQueryFilters().SingleAsync(item => item.AppointmentId == appointment.Id);
        request.Status.Should().Be("Pending");
        request.ProposedStartAt.Should().Be(proposedStart.ToUniversalTime());
        FakeWhatsAppClient.LastMessageTo.Should().NotBeNullOrWhiteSpace();
        FakeWhatsAppClient.LastMessageBody.Should().Contain("/book/approval/");
    }

    [Fact]
    public async Task ApproveRescheduleRequest_ShouldMoveAppointmentAndUpdateGoogleCalendar()
    {
        var appointment = await SeedAndReadFirstAppointmentAsync();
        await ConnectGoogleCalendarAsync();
        var proposedStart = DateTimeOffset.Parse("2026-05-13T09:00:00+02:00");
        var create = await AuthenticatedOwnerHttpClient.PostAsJsonAsync(
            $"/api/main/app/appointments/{appointment.Id}/reschedule-requests",
            new { proposedStartAt = proposedStart, note = "Please approve." }
        );
        var createBody = await create.Content.ReadFromJsonAsync<JsonObject>();
        var token = createBody!["approvalToken"]!.GetValue<string>();

        var response = await AnonymousHttpClient.PostAsJsonAsync($"/api/main/public-booking/approvals/{token}/approve", new { });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var scope = Provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MainDbContext>();
        var moved = await db.Appointments.IgnoreQueryFilters().SingleAsync(item => item.Id == appointment.Id);
        moved.StartAt.Should().Be(proposedStart.ToUniversalTime());
        var request = await db.AppointmentRescheduleRequests.IgnoreQueryFilters().SingleAsync(item => item.AppointmentId == appointment.Id);
        request.Status.Should().Be("Approved");
        FakeNangoClient.UpdatedEvents.Should().ContainSingle(update => update.AppointmentId == appointment.Id);
    }

    [Fact]
    public async Task AddGuest_ShouldCreateClientRecordAndAttachParticipant()
    {
        var appointment = await SeedAndReadFirstAppointmentAsync();

        var response = await AuthenticatedOwnerHttpClient.PostAsJsonAsync(
            $"/api/main/app/appointments/{appointment.Id}/participants",
            new { name = "Guest Client", phone = "+27 82 777 0000", email = "guest@example.com" }
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var scope = Provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MainDbContext>();
        var guest = await db.Clients.IgnoreQueryFilters().SingleAsync(client => client.Email == "guest@example.com");
        await db.AppointmentParticipants.IgnoreQueryFilters()
            .SingleAsync(participant => participant.AppointmentId == appointment.Id && participant.ClientId == guest.Id);
    }

    [Fact]
    public async Task ConfirmAppointment_WithConnectedGoogleCalendar_ShouldStoreExternalEventAndMeetUrl()
    {
        var appointment = await SeedAndReadFirstAppointmentAsync();
        await ConnectGoogleCalendarAsync();

        var response = await AuthenticatedOwnerHttpClient.PostAsync($"/api/main/app/appointments/{appointment.Id}/confirm", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var scope = Provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MainDbContext>();
        var external = await db.AppointmentExternalCalendarEvents.IgnoreQueryFilters().SingleAsync(item => item.AppointmentId == appointment.Id);
        external.ExternalEventId.Should().Be("google-event-1");
        external.MeetUrl.Should().Be("https://meet.google.com/test-meet");
    }

    protected override void RegisterMockLoggers(IServiceCollection services)
    {
        base.RegisterMockLoggers(services);
        services.RemoveAll<INangoClient>();
        services.AddScoped<INangoClient, FakeNangoClient>();
        services.RemoveAll<ITwilioWhatsAppClient>();
        services.AddSingleton<ITwilioWhatsAppClient, FakeWhatsAppClient>();
        services.RemoveAll<IEmailClient>();
        services.AddScoped(_ => EmailClient);
    }

    private async Task<Appointment> SeedAndReadFirstAppointmentAsync()
    {
        (await AuthenticatedOwnerHttpClient.GetAsync("/api/main/app/shell")).EnsureSuccessStatusCode();
        using var scope = Provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MainDbContext>();
        var appointments = await db.Appointments.IgnoreQueryFilters().ToListAsync();
        return appointments.OrderBy(item => item.StartAt).First();
    }

    private async Task ConnectGoogleCalendarAsync()
    {
        FakeNangoClient.Connections.Add(new NangoConnection("google-calendar-staff", "google-calendar-staff", DateTimeOffset.Parse("2026-05-01T08:00:00Z")));
        var response = await AuthenticatedOwnerHttpClient.PostAsJsonAsync("/api/main/integrations/sync-connections", new { appSlug = "google-calendar" });
        response.EnsureSuccessStatusCode();
    }

    private sealed class FakeNangoClient : INangoClient
    {
        public static List<NangoConnection> Connections { get; } = [];
        public static List<NangoCalendarEventRequest> UpdatedEvents { get; } = [];

        public static void Reset()
        {
            Connections.Clear();
            UpdatedEvents.Clear();
        }

        public Task<NangoConnectSession> CreateConnectSessionAsync(NangoConnectSessionRequest request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new NangoConnectSession("https://connect.nango.dev/session/test", DateTimeOffset.Parse("2026-05-01T08:30:00Z")));
        }

        public Task<IReadOnlyList<NangoConnection>> ListConnectionsAsync(string integrationKey, IReadOnlyDictionary<string, string> tags, CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<NangoConnection>>(Connections);
        }

        public Task<IReadOnlyList<NangoCalendar>> ListCalendarsAsync(string integrationKey, string connectionId, CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<NangoCalendar>>([new NangoCalendar("primary", "Primary calendar", true, true)]);
        }

        public Task<NangoCalendarEvent> CreateCalendarEventAsync(string integrationKey, string connectionId, NangoCalendarEventRequest request, CancellationToken cancellationToken)
        {
            UpdatedEvents.Add(request);
            return Task.FromResult(new NangoCalendarEvent("google-event-1", "https://meet.google.com/test-meet"));
        }

        public Task<NangoCalendarEvent> UpdateCalendarEventAsync(string integrationKey, string connectionId, string calendarId, string eventId, NangoCalendarEventRequest request, CancellationToken cancellationToken)
        {
            UpdatedEvents.Add(request);
            return Task.FromResult(new NangoCalendarEvent(eventId, "https://meet.google.com/test-meet"));
        }

        public Task DeleteCalendarEventAsync(string integrationKey, string connectionId, string calendarId, string eventId, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class FakeWhatsAppClient : ITwilioWhatsAppClient
    {
        public static string? LastMessageTo { get; private set; }
        public static string? LastMessageBody { get; private set; }

        public static void Reset()
        {
            LastMessageTo = null;
            LastMessageBody = null;
        }

        public Task SendAsync(TenantId tenantId, string toPhone, string message, CancellationToken cancellationToken)
        {
            LastMessageTo = toPhone;
            LastMessageBody = message;
            return Task.CompletedTask;
        }
    }
}
