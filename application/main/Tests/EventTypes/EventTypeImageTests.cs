using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using JetBrains.Annotations;
using Main.Database;
using NSubstitute;
using SharedKernel.Tests;
using Xunit;

namespace Main.Tests.EventTypes;

public sealed class EventTypeImageTests : EndpointBaseTest<MainDbContext>
{
    [Fact]
    public async Task UploadImage_WhenPngWithinLimit_ShouldStoreBlobAndSetImageUrl()
    {
        // Arrange
        var eventType = await CreateEventTypeAsync();

        // Act
        var response = await AuthenticatedOwnerHttpClient.PostAsync($"/api/event-types/{eventType.Id}/image", PngForm(1024));

        // Assert
        response.EnsureSuccessStatusCode();
        await BlobStorageClient.Received(1).UploadAsync("service-images", Arg.Any<string>(), "image/png", Arg.Any<Stream>(), Arg.Any<CancellationToken>());

        var fetched = await GetEventTypeAsync(eventType.Id);
        fetched.ImageUrl.Should().StartWith($"/service-images/").And.EndWith(".png");
        fetched.ImageUrl.Should().Contain(eventType.Id);
    }

    [Fact]
    public async Task UploadImage_WhenUnsupportedContentType_ShouldReturnBadRequest()
    {
        // Arrange
        var eventType = await CreateEventTypeAsync();
        var form = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(new byte[256]);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/gif");
        form.Add(fileContent, "file", "service.gif");

        // Act
        var response = await AuthenticatedOwnerHttpClient.PostAsync($"/api/event-types/{eventType.Id}/image", form);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await BlobStorageClient.DidNotReceiveWithAnyArgs().UploadAsync(null!, null!, null!, null!, CancellationToken.None);
    }

    [Fact]
    public async Task UploadImage_WhenFileTooLarge_ShouldReturnBadRequest()
    {
        // Arrange
        var eventType = await CreateEventTypeAsync();

        // Act
        var response = await AuthenticatedOwnerHttpClient.PostAsync($"/api/event-types/{eventType.Id}/image", PngForm(3 * 1024 * 1024));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await BlobStorageClient.DidNotReceiveWithAnyArgs().UploadAsync(null!, null!, null!, null!, CancellationToken.None);
    }

    [Fact]
    public async Task UploadImage_WhenMember_ShouldReturnForbidden()
    {
        // Arrange
        var eventType = await CreateEventTypeAsync();

        // Act
        var response = await AuthenticatedMemberHttpClient.PostAsync($"/api/event-types/{eventType.Id}/image", PngForm(1024));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task RemoveImage_WhenImageExists_ShouldClearImageUrl()
    {
        // Arrange
        var eventType = await CreateEventTypeAsync();
        (await AuthenticatedOwnerHttpClient.PostAsync($"/api/event-types/{eventType.Id}/image", PngForm(1024))).EnsureSuccessStatusCode();

        // Act
        var response = await AuthenticatedOwnerHttpClient.DeleteAsync($"/api/event-types/{eventType.Id}/image");

        // Assert
        response.EnsureSuccessStatusCode();
        var fetched = await GetEventTypeAsync(eventType.Id);
        fetched.ImageUrl.Should().BeNull();
    }

    private static MultipartFormDataContent PngForm(int sizeBytes)
    {
        var form = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(new byte[sizeBytes]);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
        form.Add(fileContent, "file", "service.png");
        return form;
    }

    private async Task<EventTypeResponse> CreateEventTypeAsync()
    {
        var scheduleResponse = await AuthenticatedOwnerHttpClient.PostAsJsonAsync("/api/schedules", new
            {
                name = "Working hours",
                timeZone = "Africa/Johannesburg",
                isDefault = true,
                availabilityWindows = new[] { new { days = new[] { 1, 2, 3, 4, 5 }, startMinute = 540, endMinute = 1020 } }
            }
        );
        scheduleResponse.EnsureSuccessStatusCode();
        var schedule = (await scheduleResponse.DeserializeResponse<ScheduleResponse>())!;

        var response = await AuthenticatedOwnerHttpClient.PostAsJsonAsync("/api/event-types", new
            {
                title = "Glow facial",
                slug = "glow-facial",
                description = "A 30 minute facial",
                durationMinutes = 30,
                hidden = false,
                scheduleId = schedule.Id,
                beforeEventBufferMinutes = 0,
                afterEventBufferMinutes = 0,
                slotIntervalMinutes = 30,
                minimumBookingNoticeMinutes = 60,
                locationType = "inPerson",
                locationValue = "Studio"
            }
        );
        response.EnsureSuccessStatusCode();
        return (await response.DeserializeResponse<EventTypeResponse>())!;
    }

    private async Task<EventTypeResponse> GetEventTypeAsync(string id)
    {
        var response = await AuthenticatedOwnerHttpClient.GetAsync($"/api/event-types/{id}");
        response.EnsureSuccessStatusCode();
        return (await response.DeserializeResponse<EventTypeResponse>())!;
    }

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed record ScheduleResponse(string Id);

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed record EventTypeResponse(string Id, string Title, string? ImageUrl);
}
