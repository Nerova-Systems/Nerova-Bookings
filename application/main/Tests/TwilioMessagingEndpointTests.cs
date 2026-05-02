using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using Main.Database;
using Main.Features.Appointments;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace Main.Tests;

public sealed class TwilioMessagingEndpointTests : EndpointBaseTest<MainDbContext>
{
    public TwilioMessagingEndpointTests()
    {
        FakeTwilioMessagingProvisioningClient.Reset();
    }

    [Fact]
    public async Task Status_WhenTenantHasNoMessagingProfile_ShouldReturnWhatsappReadinessShell()
    {
        await SeedShellAsync();

        var response = await AuthenticatedOwnerHttpClient.GetAsync("/api/main/messaging/whatsapp/status");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonObject>();
        body!["appSlug"]!.GetValue<string>().Should().Be("whatsapp");
        body["provider"]!.GetValue<string>().Should().Be("Twilio");
        body["countryCode"]!.GetValue<string>().Should().Be("ZA");
        body["status"]!.GetValue<string>().Should().Be("NotProvisioned");
        body["canSendMessages"]!.GetValue<bool>().Should().BeFalse();
    }

    [Fact]
    public async Task ProvisionSubaccount_ShouldCreateTenantMessagingProfileAndLifecycleTemplates()
    {
        await SeedShellAsync();

        var response = await AuthenticatedOwnerHttpClient.PostAsJsonAsync("/api/main/messaging/whatsapp/provision-subaccount", new { });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        FakeTwilioMessagingProvisioningClient.LastFriendlyName.Should().Contain("WhatsApp tenant");

        using var scope = Provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MainDbContext>();
        var profile = await db.TenantMessagingProfiles.IgnoreQueryFilters().SingleAsync();
        profile.Provider.Should().Be("Twilio");
        profile.AppSlug.Should().Be("whatsapp");
        profile.CountryCode.Should().Be("ZA");
        profile.TwilioSubaccountSid.Should().Be("AC_tenant_test");
        profile.ProvisioningStatus.Should().Be("SubaccountProvisioned");
        profile.WhatsAppApprovalStatus.Should().Be("NotSubmitted");

        var templates = await db.TenantMessageTemplates.IgnoreQueryFilters().ToListAsync();
        templates.Select(template => template.TemplateKey).Should().BeEquivalentTo(
            "booking_confirmation",
            "booking_reminder",
            "reschedule_approval",
            "booking_cancellation",
            "payment_link",
            "no_show",
            "completion_follow_up",
            "review_request",
            "waitlist",
            "loyalty_marketing"
        );
    }

    [Fact]
    public async Task ClaimNumber_ShouldAssignSouthAfricanNumberToProvisionedTenant()
    {
        await SeedShellAsync();
        await AuthenticatedOwnerHttpClient.PostAsJsonAsync("/api/main/messaging/whatsapp/provision-subaccount", new { });

        var response = await AuthenticatedOwnerHttpClient.PostAsJsonAsync("/api/main/messaging/whatsapp/claim-number", new { });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        FakeTwilioMessagingProvisioningClient.LastClaimAccountSid.Should().Be("AC_tenant_test");
        using var scope = Provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MainDbContext>();
        var assignment = await db.TenantPhoneNumberAssignments.IgnoreQueryFilters().SingleAsync();
        assignment.PhoneNumber.Should().Be("+27870000001");
        assignment.TwilioPhoneNumberSid.Should().Be("PN_tenant_test");
        assignment.CountryCode.Should().Be("ZA");
        assignment.AssignmentStatus.Should().Be("Assigned");
    }

    [Fact]
    public async Task RescheduleRequest_WhenWhatsappSenderIsNotApproved_ShouldKeepRequestPendingAndReturnGatewayError()
    {
        var appointment = await SeedAndReadFirstAppointmentAsync();
        var proposedStart = DateTimeOffset.Parse("2026-05-13T09:00:00+02:00");

        var response = await AuthenticatedOwnerHttpClient.PostAsJsonAsync(
            $"/api/main/app/appointments/{appointment.Id}/reschedule-requests",
            new { proposedStartAt = proposedStart, note = "Can we move this booking?" }
        );

        response.StatusCode.Should().Be(HttpStatusCode.BadGateway);
        var problem = await response.Content.ReadFromJsonAsync<JsonObject>();
        problem!["detail"]!.GetValue<string>().Should().Contain("WhatsApp sender is not approved");

        using var scope = Provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MainDbContext>();
        var request = await db.AppointmentRescheduleRequests.IgnoreQueryFilters().SingleAsync(item => item.AppointmentId == appointment.Id);
        request.Status.Should().Be("Pending");
    }

    protected override void RegisterMockLoggers(IServiceCollection services)
    {
        base.RegisterMockLoggers(services);
        services.RemoveAll<ITwilioMessagingProvisioningClient>();
        services.AddSingleton<ITwilioMessagingProvisioningClient, FakeTwilioMessagingProvisioningClient>();
    }

    private async Task SeedShellAsync()
    {
        (await AuthenticatedOwnerHttpClient.GetAsync("/api/main/app/shell")).EnsureSuccessStatusCode();
    }

    private async Task<Appointment> SeedAndReadFirstAppointmentAsync()
    {
        await SeedShellAsync();
        using var scope = Provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MainDbContext>();
        var appointments = await db.Appointments.IgnoreQueryFilters().ToListAsync();
        return appointments.OrderBy(item => item.StartAt).First();
    }

    private sealed class FakeTwilioMessagingProvisioningClient : ITwilioMessagingProvisioningClient
    {
        public static string? LastFriendlyName { get; private set; }
        public static string? LastClaimAccountSid { get; private set; }

        public static void Reset()
        {
            LastFriendlyName = null;
            LastClaimAccountSid = null;
        }

        public Task<TwilioSubaccountProvisioningResult> CreateSubaccountAsync(string friendlyName, CancellationToken cancellationToken)
        {
            LastFriendlyName = friendlyName;
            return Task.FromResult(new TwilioSubaccountProvisioningResult("AC_tenant_test", "active"));
        }

        public Task<TwilioPhoneNumberClaimResult> ClaimSouthAfricanNumberAsync(string accountSid, CancellationToken cancellationToken)
        {
            LastClaimAccountSid = accountSid;
            return Task.FromResult(new TwilioPhoneNumberClaimResult("PN_tenant_test", "+27870000001", true, true, "https://localhost/api/main/webhooks/twilio/messaging"));
        }
    }
}
