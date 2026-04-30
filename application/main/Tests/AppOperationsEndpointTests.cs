using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using Main.Database;
using Main.Features.Appointments;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Main.Tests;

public sealed class AppOperationsEndpointTests : EndpointBaseTest<MainDbContext>
{
    [Fact]
    public async Task ServiceLifecycle_ShouldCreateUpdateArchiveAndRestoreService()
    {
        await SeedShellAsync();

        var create = await AuthenticatedOwnerHttpClient.PostAsJsonAsync("/api/main/app/services", NewServiceRequest("Deep tissue", 60000));
        create.StatusCode.Should().Be(HttpStatusCode.OK);
        var service = await ReadServiceByNameAsync("Deep tissue");
        service.PriceCents.Should().Be(60000);
        (await ReadServiceVersionsAsync(service.Id)).Should().ContainSingle(version => version.VersionNumber == 1);

        var update = await AuthenticatedOwnerHttpClient.PutAsJsonAsync($"/api/main/app/services/{service.Id}", NewServiceRequest("Deep tissue plus", 75000));
        update.StatusCode.Should().Be(HttpStatusCode.OK);
        service = await ReadServiceByNameAsync("Deep tissue plus");
        service.PriceCents.Should().Be(75000);
        (await ReadServiceVersionsAsync(service.Id)).Should().Contain(version => version.VersionNumber == 2 && version.PriceCents == 75000);

        var archive = await AuthenticatedOwnerHttpClient.PostAsync($"/api/main/app/services/{service.Id}/archive", null);
        archive.StatusCode.Should().Be(HttpStatusCode.OK);
        service = await ReadServiceByNameAsync("Deep tissue plus");
        service.IsActive.Should().BeFalse();
        (await ReadServiceVersionsAsync(service.Id)).Should().Contain(version => version.VersionNumber == 3 && !version.IsActive);

        var restore = await AuthenticatedOwnerHttpClient.PostAsync($"/api/main/app/services/{service.Id}/restore", null);
        restore.StatusCode.Should().Be(HttpStatusCode.OK);
        service = await ReadServiceByNameAsync("Deep tissue plus");
        service.IsActive.Should().BeTrue();
        (await ReadServiceVersionsAsync(service.Id)).Should().Contain(version => version.VersionNumber == 4 && version.IsActive);
    }

    [Fact]
    public async Task Shell_ShouldExposeLatestServiceVersionNumber()
    {
        await SeedShellAsync();
        var service = await ReadServiceByNameAsync("Express session");

        var update = await AuthenticatedOwnerHttpClient.PutAsJsonAsync(
            $"/api/main/app/services/{service.Id}",
            NewServiceRequest("Express session updated", 33000)
        );

        update.StatusCode.Should().Be(HttpStatusCode.OK);
        var shell = await AuthenticatedOwnerHttpClient.GetFromJsonAsync<JsonObject>("/api/main/app/shell");
        var serviceDto = shell!["services"]!.AsArray().Single(item => item!["id"]!.GetValue<string>() == service.Id)!;
        serviceDto["latestVersionNumber"]!.GetValue<int>().Should().Be(2);
    }

    [Fact]
    public async Task UpdateService_WhenAppointmentAlreadyBooked_ShouldKeepAppointmentServiceVersion()
    {
        await SeedShellAsync();
        var appointment = await ReadFirstAppointmentAsync();
        var originalVersion = await ReadServiceVersionAsync(appointment.ServiceVersionId);

        var update = await AuthenticatedOwnerHttpClient.PutAsJsonAsync(
            $"/api/main/app/services/{appointment.ServiceId}",
            NewServiceRequest("Changed service", 99000, "FullPaymentBeforeBooking")
        );

        update.StatusCode.Should().Be(HttpStatusCode.OK);
        appointment = await ReadAppointmentAsync(appointment.Id);
        appointment.ServiceVersionId.Should().Be(originalVersion.Id);
        var appointmentVersion = await ReadServiceVersionAsync(appointment.ServiceVersionId);
        appointmentVersion.Name.Should().Be(originalVersion.Name);
        appointmentVersion.PriceCents.Should().Be(originalVersion.PriceCents);
        appointmentVersion.PaymentPolicy.Should().Be(originalVersion.PaymentPolicy);
    }

    [Fact]
    public async Task UpdateAppointmentStatus_ShouldPersistStatusWithoutPaymentState()
    {
        await SeedShellAsync();
        var appointment = await ReadFirstAppointmentAsync();
        var originalPaymentStatus = appointment.PaymentStatus;

        var response = await AuthenticatedOwnerHttpClient.PostAsJsonAsync(
            $"/api/main/app/appointments/{appointment.Id}/status",
            new { status = "Completed" }
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        appointment = await ReadAppointmentAsync(appointment.Id);
        appointment.Status.Should().Be(AppointmentStatus.Completed);
        appointment.PaymentStatus.Should().Be(originalPaymentStatus);
    }

    [Fact]
    public async Task CreateCalendarBlock_ShouldHideOverlappingAvailabilitySlots()
    {
        await SeedShellAsync();
        var service = await ReadServiceByNameAsync("Express session");

        var create = await AuthenticatedOwnerHttpClient.PostAsJsonAsync(
            "/api/main/app/calendar/blocks",
            new
            {
                title = "Supply run",
                startAt = new DateTimeOffset(2026, 4, 27, 10, 0, 0, TimeSpan.FromHours(2)),
                endAt = new DateTimeOffset(2026, 4, 27, 11, 0, 0, TimeSpan.FromHours(2))
            }
        );

        create.StatusCode.Should().Be(HttpStatusCode.OK);
        var slots = await AuthenticatedOwnerHttpClient.GetFromJsonAsync<JsonArray>(
            $"/api/main/app/availability/slots?serviceId={service.Id}&date=2026-04-27"
        );
        slots.Should().NotBeNull();
        slots!.Select(slot => slot!["startAt"]!.GetValue<DateTimeOffset>())
            .Should()
            .NotContain(start => start >= new DateTimeOffset(2026, 4, 27, 9, 30, 0, TimeSpan.FromHours(2)) &&
                                 start < new DateTimeOffset(2026, 4, 27, 11, 0, 0, TimeSpan.FromHours(2)));
    }

    [Fact]
    public async Task AvailabilitySlots_ShouldExcludeExternalBusyBlocks()
    {
        await SeedShellAsync();
        var service = await ReadServiceByNameAsync("Express session");
        using (var scope = Provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MainDbContext>();
            var tenantId = (await db.BookableServices.IgnoreQueryFilters().SingleAsync(s => s.Id == service.Id)).TenantId;
            db.ExternalBusyBlocks.Add(new ExternalBusyBlock
            {
                TenantId = tenantId,
                Provider = "Google",
                Label = "Busy",
                StartAt = new DateTimeOffset(2026, 4, 27, 13, 0, 0, TimeSpan.FromHours(2)),
                EndAt = new DateTimeOffset(2026, 4, 27, 14, 0, 0, TimeSpan.FromHours(2))
            });
            await db.SaveChangesAsync();
        }

        var slots = await AuthenticatedOwnerHttpClient.GetFromJsonAsync<JsonArray>(
            $"/api/main/app/availability/slots?serviceId={service.Id}&date=2026-04-27"
        );

        slots.Should().NotBeNull();
        slots!.Select(slot => slot!["startAt"]!.GetValue<DateTimeOffset>())
            .Should()
            .NotContain(start => start >= new DateTimeOffset(2026, 4, 27, 12, 30, 0, TimeSpan.FromHours(2)) &&
                                 start < new DateTimeOffset(2026, 4, 27, 14, 0, 0, TimeSpan.FromHours(2)));
    }

    [Fact]
    public async Task UpdateClient_ShouldPersistOperationalNotes()
    {
        await SeedShellAsync();
        var client = await ReadFirstClientAsync();

        var response = await AuthenticatedOwnerHttpClient.PutAsJsonAsync(
            $"/api/main/app/clients/{client.Id}",
            new
            {
                name = client.Name,
                phone = client.Phone,
                email = client.Email,
                status = "VIP",
                alert = "Prefers morning bookings",
                internalNote = "Always confirm parking instructions."
            }
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        client = await ReadClientAsync(client.Id);
        client.Status.Should().Be("VIP");
        client.Alert.Should().Be("Prefers morning bookings");
        client.InternalNote.Should().Be("Always confirm parking instructions.");
    }

    [Fact]
    public async Task Shell_ShouldReturnClientAppointmentHistoryNewestFirst()
    {
        var response = await AuthenticatedOwnerHttpClient.GetAsync("/api/main/app/shell");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("appointmentHistory");
        using var scope = Provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MainDbContext>();
        var client = await db.Clients.IgnoreQueryFilters().FirstAsync(client => client.Name == "Liam Botha");
        var appointment = await db.Appointments.IgnoreQueryFilters().SingleAsync(appointment => appointment.ClientId == client.Id);
        var version = await db.BookableServiceVersions.IgnoreQueryFilters().SingleAsync(version => version.Id == appointment.ServiceVersionId);
        body.Should().Contain(version.Name);
    }

    private async Task SeedShellAsync()
    {
        var response = await AuthenticatedOwnerHttpClient.GetAsync("/api/main/app/shell");
        response.EnsureSuccessStatusCode();
    }

    private async Task<BookableService> ReadServiceByNameAsync(string name)
    {
        using var scope = Provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MainDbContext>();
        return await db.BookableServices.IgnoreQueryFilters().SingleAsync(service => service.Name == name);
    }

    private async Task<List<BookableServiceVersion>> ReadServiceVersionsAsync(string serviceId)
    {
        using var scope = Provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MainDbContext>();
        return await db.BookableServiceVersions.IgnoreQueryFilters().Where(version => version.ServiceId == serviceId).OrderBy(version => version.VersionNumber).ToListAsync();
    }

    private async Task<BookableServiceVersion> ReadServiceVersionAsync(string id)
    {
        using var scope = Provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MainDbContext>();
        return await db.BookableServiceVersions.IgnoreQueryFilters().SingleAsync(version => version.Id == id);
    }

    private async Task<Appointment> ReadFirstAppointmentAsync()
    {
        using var scope = Provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MainDbContext>();
        return (await db.Appointments.IgnoreQueryFilters().ToListAsync()).OrderBy(appointment => appointment.StartAt).First();
    }

    private async Task<Appointment> ReadAppointmentAsync(string id)
    {
        using var scope = Provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MainDbContext>();
        return await db.Appointments.IgnoreQueryFilters().SingleAsync(appointment => appointment.Id == id);
    }

    private async Task<Client> ReadFirstClientAsync()
    {
        using var scope = Provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MainDbContext>();
        return await db.Clients.IgnoreQueryFilters().OrderBy(client => client.Name).FirstAsync();
    }

    private async Task<Client> ReadClientAsync(string id)
    {
        using var scope = Provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MainDbContext>();
        return await db.Clients.IgnoreQueryFilters().SingleAsync(client => client.Id == id);
    }

    private static object NewServiceRequest(string name, int priceCents, string? paymentPolicy = null)
    {
        return new
        {
            name,
            categoryName = "Consultations",
            description = "Hands-on appointment",
            mode = "physical",
            durationMinutes = 60,
            priceCents,
            depositCents = 15000,
            paymentPolicy,
            bufferBeforeMinutes = 5,
            bufferAfterMinutes = 10,
            location = "Sea Point studio"
        };
    }
}
