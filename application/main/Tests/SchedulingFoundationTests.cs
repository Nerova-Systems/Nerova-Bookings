using System.Net.Http.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using Main.Database;
using Main.Features.Appointments;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel.Domain;
using Xunit;

namespace Main.Tests;

public sealed class SchedulingFoundationTests : EndpointBaseTest<MainDbContext>
{
    [Fact]
    public async Task Shell_ShouldCreateDefaultLocationAndStaffForSoloTenant()
    {
        var response = await AuthenticatedOwnerHttpClient.GetAsync("/api/main/app/shell");

        response.EnsureSuccessStatusCode();
        using var scope = Provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MainDbContext>();
        var location = await db.BusinessLocations.IgnoreQueryFilters().SingleAsync();
        location.Name.Should().Be("Nerova Studio");
        location.IsDefault.Should().BeTrue();
        var staff = await db.StaffMembers.IgnoreQueryFilters().SingleAsync();
        staff.LocationId.Should().Be(location.Id);
        staff.UserId.Should().NotBeNullOrWhiteSpace();
        (await db.BookableServices.IgnoreQueryFilters().ToListAsync()).Should().OnlyContain(service => service.LocationId == location.Id);
    }

    [Fact]
    public async Task AvailabilitySlots_ShouldExcludeRequiredResourceReservations()
    {
        await AuthenticatedOwnerHttpClient.GetAsync("/api/main/app/shell");
        using (var scope = Provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MainDbContext>();
            var service = await db.BookableServices.IgnoreQueryFilters().SingleAsync(service => service.Name == "Express session");
            var tenantId = service.TenantId;
            var locationId = service.LocationId;
            var resource = new SchedulingResource
            {
                TenantId = tenantId,
                LocationId = locationId,
                Name = "Room 1",
                Type = "Room",
                IsActive = true
            };
            db.SchedulingResources.Add(resource);
            db.BookableServiceResources.Add(new BookableServiceResource { TenantId = tenantId, ServiceId = service.Id, ResourceId = resource.Id });
            db.ResourceReservations.Add(new ResourceReservation
            {
                TenantId = tenantId,
                ResourceId = resource.Id,
                StartAt = new DateTimeOffset(2026, 4, 27, 10, 0, 0, TimeSpan.FromHours(2)),
                EndAt = new DateTimeOffset(2026, 4, 27, 11, 0, 0, TimeSpan.FromHours(2)),
                Source = "Manual"
            });
            await db.SaveChangesAsync();
        }

        using var verifyScope = Provider.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<MainDbContext>();
        var express = await verifyDb.BookableServices.IgnoreQueryFilters().SingleAsync(service => service.Name == "Express session");
        var slots = await AuthenticatedOwnerHttpClient.GetFromJsonAsync<JsonArray>(
            $"/api/main/app/availability/slots?serviceId={express.Id}&date=2026-04-27"
        );

        slots.Should().NotBeNull();
        slots!.Select(slot => slot!["startAt"]!.GetValue<DateTimeOffset>())
            .Should()
            .NotContain(start => start >= new DateTimeOffset(2026, 4, 27, 9, 30, 0, TimeSpan.FromHours(2)) &&
                                 start < new DateTimeOffset(2026, 4, 27, 11, 0, 0, TimeSpan.FromHours(2)));
    }

    [Fact]
    public async Task Shell_ShouldExposeConnectorOwnershipForStaffAndTenantConnections()
    {
        await AuthenticatedOwnerHttpClient.GetAsync("/api/main/app/shell");
        using (var scope = Provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MainDbContext>();
            var tenantId = await db.BusinessProfiles.IgnoreQueryFilters().Select(profile => profile.TenantId).SingleAsync();
            var staff = await db.StaffMembers.IgnoreQueryFilters().SingleAsync();
            db.IntegrationConnections.Add(new IntegrationConnection
            {
                TenantId = tenantId,
                Provider = "Google",
                Capability = "Calendar",
                Status = "Connected",
                OwnerType = ConnectorOwnerType.StaffMember,
                OwnerId = staff.Id,
                ExternalConnectionId = "google-calendar-staff"
            });
            db.IntegrationConnections.Add(new IntegrationConnection
            {
                TenantId = tenantId,
                Provider = "Paystack",
                Capability = "Payment",
                Status = "Connected",
                OwnerType = ConnectorOwnerType.Tenant,
                OwnerId = tenantId.ToString(),
                ExternalConnectionId = "paystack-tenant"
            });
            await db.SaveChangesAsync();
        }

        var shell = await AuthenticatedOwnerHttpClient.GetFromJsonAsync<JsonObject>("/api/main/app/shell");
        var integrations = shell!["integrations"]!.AsArray();
        integrations.Should().Contain(integration => integration!["provider"]!.GetValue<string>() == "Google" &&
                                                     integration["ownerType"]!.GetValue<string>() == "StaffMember" &&
                                                     integration["externalConnectionId"]!.GetValue<string>() == "google-calendar-staff");
        integrations.Should().Contain(integration => integration!["provider"]!.GetValue<string>() == "Paystack" &&
                                                     integration["ownerType"]!.GetValue<string>() == "Tenant");
    }
}
