using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Main.Database;
using Main.Features.Schedules.Shared;
using SharedKernel.Tests;
using SharedKernel.Validation;
using Xunit;

namespace Main.Tests.Schedules;

public sealed class TravelScheduleEndpointsTests : EndpointBaseTest<MainDbContext>
{
    [Fact]
    public async Task CrudRoundTrip_ShouldPersistAndReturnEntries()
    {
        var ownerId = DatabaseSeeder.Tenant1Owner.Id;
        var basePath = $"/api/users/{ownerId}/travel-schedules";

        var createBody = new
        {
            startDate = "2026-06-01",
            endDate = "2026-06-07",
            timeZone = "Europe/Paris"
        };

        var createResponse = await AuthenticatedOwnerHttpClient.PostAsJsonAsync(basePath, createBody);
        createResponse.EnsureSuccessStatusCode();
        var created = (await createResponse.DeserializeResponse<TravelScheduleResponse>())!;
        created.TimeZone.Should().Be("Europe/Paris");

        var listResponse = await AuthenticatedOwnerHttpClient.GetAsync(basePath);
        listResponse.ShouldBeSuccessfulGetRequest();
        var list = (await listResponse.DeserializeResponse<TravelSchedulesResponse>())!;
        list.TravelSchedules.Should().ContainSingle().Which.Id.Should().Be(created.Id);

        var updateBody = new
        {
            startDate = "2026-06-02",
            endDate = "2026-06-08",
            timeZone = "Asia/Tokyo"
        };
        var updateResponse = await AuthenticatedOwnerHttpClient.PutAsJsonAsync($"{basePath}/{created.Id}", updateBody);
        updateResponse.EnsureSuccessStatusCode();
        var updated = (await updateResponse.DeserializeResponse<TravelScheduleResponse>())!;
        updated.TimeZone.Should().Be("Asia/Tokyo");

        var deleteResponse = await AuthenticatedOwnerHttpClient.DeleteAsync($"{basePath}/{created.Id}");
        deleteResponse.EnsureSuccessStatusCode();

        var afterDelete = await AuthenticatedOwnerHttpClient.GetAsync(basePath);
        var emptyList = (await afterDelete.DeserializeResponse<TravelSchedulesResponse>())!;
        emptyList.TravelSchedules.Should().BeEmpty();
    }

    [Fact]
    public async Task List_WhenOtherUserRequested_ShouldReturnForbidden()
    {
        var otherUserId = DatabaseSeeder.Tenant1Member.Id;
        var response = await AuthenticatedOwnerHttpClient.GetAsync($"/api/users/{otherUserId}/travel-schedules");
        await response.ShouldHaveErrorStatusCode(HttpStatusCode.Forbidden, "Travel schedules can only be managed by their owner.");
    }
}

public sealed class OutOfOfficeEndpointsTests : EndpointBaseTest<MainDbContext>
{
    [Fact]
    public async Task CrudRoundTrip_ShouldPersistAndReturnEntries()
    {
        var ownerId = DatabaseSeeder.Tenant1Owner.Id;
        var basePath = $"/api/users/{ownerId}/out-of-office";

        var createBody = new
        {
            startDate = "2026-06-01",
            endDate = "2026-06-03",
            reason = "Vacation"
        };

        var createResponse = await AuthenticatedOwnerHttpClient.PostAsJsonAsync(basePath, createBody);
        createResponse.EnsureSuccessStatusCode();
        var created = (await createResponse.DeserializeResponse<OutOfOfficeResponse>())!;
        created.Reason.Should().Be("Vacation");

        var listResponse = await AuthenticatedOwnerHttpClient.GetAsync(basePath);
        listResponse.ShouldBeSuccessfulGetRequest();
        var list = (await listResponse.DeserializeResponse<OutOfOfficesResponse>())!;
        list.OutOfOffices.Should().ContainSingle().Which.Id.Should().Be(created.Id);

        var updateBody = new
        {
            startDate = "2026-06-01",
            endDate = "2026-06-05",
            reason = "Conference"
        };
        var updateResponse = await AuthenticatedOwnerHttpClient.PutAsJsonAsync($"{basePath}/{created.Id}", updateBody);
        updateResponse.EnsureSuccessStatusCode();
        var updated = (await updateResponse.DeserializeResponse<OutOfOfficeResponse>())!;
        updated.Reason.Should().Be("Conference");

        var deleteResponse = await AuthenticatedOwnerHttpClient.DeleteAsync($"{basePath}/{created.Id}");
        deleteResponse.EnsureSuccessStatusCode();

        var afterDelete = await AuthenticatedOwnerHttpClient.GetAsync(basePath);
        var emptyList = (await afterDelete.DeserializeResponse<OutOfOfficesResponse>())!;
        emptyList.OutOfOffices.Should().BeEmpty();
    }

    [Fact]
    public async Task List_WhenOtherUserRequested_ShouldReturnForbidden()
    {
        var otherUserId = DatabaseSeeder.Tenant1Member.Id;
        var response = await AuthenticatedOwnerHttpClient.GetAsync($"/api/users/{otherUserId}/out-of-office");
        await response.ShouldHaveErrorStatusCode(HttpStatusCode.Forbidden, "Out-of-office entries can only be managed by their owner.");
    }
}
