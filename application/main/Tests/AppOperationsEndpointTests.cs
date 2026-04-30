using System.Net;
using System.Net.Http.Json;
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

        var update = await AuthenticatedOwnerHttpClient.PutAsJsonAsync($"/api/main/app/services/{service.Id}", NewServiceRequest("Deep tissue plus", 75000));
        update.StatusCode.Should().Be(HttpStatusCode.OK);
        service = await ReadServiceByNameAsync("Deep tissue plus");
        service.PriceCents.Should().Be(75000);

        var archive = await AuthenticatedOwnerHttpClient.PostAsync($"/api/main/app/services/{service.Id}/archive", null);
        archive.StatusCode.Should().Be(HttpStatusCode.OK);
        service = await ReadServiceByNameAsync("Deep tissue plus");
        service.IsActive.Should().BeFalse();

        var restore = await AuthenticatedOwnerHttpClient.PostAsync($"/api/main/app/services/{service.Id}/restore", null);
        restore.StatusCode.Should().Be(HttpStatusCode.OK);
        service = await ReadServiceByNameAsync("Deep tissue plus");
        service.IsActive.Should().BeTrue();
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

    private static object NewServiceRequest(string name, int priceCents)
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
            bufferBeforeMinutes = 5,
            bufferAfterMinutes = 10,
            location = "Sea Point studio"
        };
    }
}
