using FluentAssertions;
using Main.Database;
using Main.Features.Apps.Connectors.GoogleCalendar;
using Main.Features.Apps.Connectors.GoogleMeet;
using Main.Features.Apps.Connectors.MsTeams;
using Main.Features.Apps.Connectors.Office365Calendar;
using Main.Features.Apps.Connectors.Zoom;
using Main.Features.Apps.Domain;
using Main.Features.Apps.Shared;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel.Tests;
using Xunit;

namespace Main.Tests.Apps;

/// <summary>
///     Verifies that GET /api/apps surfaces each connector's REAL OAuth scopes through the new
///     <see cref="AppResponse.Permissions" /> field. The expected scope strings are pinned
///     against the connector option constants (the single source of truth the installers and
///     authorize calls share) so the test fails if a connector's real scope set ever drifts.
/// </summary>
public sealed class AppPermissionsEndpointTests : EndpointBaseTest<MainDbContext>
{
    public AppPermissionsEndpointTests()
    {
        using var scope = Provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MainDbContext>();
        db.Set<App>().Add(App.Create(GoogleCalendarSlug.Slug, "Google Calendar", AppCategory.Calendar, "", ""));
        db.Set<App>().Add(App.Create(Office365CalendarSlug.Slug, "Office 365 Calendar", AppCategory.Calendar, "", ""));
        db.Set<App>().Add(App.Create(ZoomSlug.Slug, "Zoom", AppCategory.Conferencing, "", ""));
        db.Set<App>().Add(App.Create(GoogleMeetSlug.Slug, "Google Meet", AppCategory.Conferencing, "", ""));
        db.Set<App>().Add(App.Create(MsTeamsSlug.Slug, "Microsoft Teams", AppCategory.Conferencing, "", ""));
        db.SaveChanges();
    }

    [Fact]
    public async Task ListApps_ShouldExposeRealScopesPerApp()
    {
        var response = await AuthenticatedMemberHttpClient.GetAsync("/api/apps");

        response.ShouldBeSuccessfulGetRequest();
        var payload = await response.DeserializeResponse<AppsResponse>();
        payload.Should().NotBeNull();

        ScopesFor(payload, GoogleCalendarSlug.Slug).Should().Equal(
            GoogleCalendarOptions.CalendarReadonlyScope,
            GoogleCalendarOptions.CalendarEventsScope
        );

        ScopesFor(payload, Office365CalendarSlug.Slug).Should().Equal(
            Office365CalendarOptions.OfflineAccessScope,
            Office365CalendarOptions.CalendarsReadWriteScope,
            Office365CalendarOptions.OnlineMeetingsScope
        );

        ScopesFor(payload, ZoomSlug.Slug).Should().Equal(ZoomOptions.MeetingWriteScope);

        // Google Meet has no OAuth of its own — it reuses the Google Calendar scopes.
        ScopesFor(payload, GoogleMeetSlug.Slug).Should().Equal(
            GoogleCalendarOptions.CalendarReadonlyScope,
            GoogleCalendarOptions.CalendarEventsScope
        );

        // MS Teams reuses the Office 365 credential and specifically requires OnlineMeetings.ReadWrite.
        ScopesFor(payload, MsTeamsSlug.Slug).Should().Contain(Office365CalendarOptions.OnlineMeetingsScope);
    }

    [Fact]
    public async Task ListApps_EveryPermission_ShouldHaveTitleAndDescription()
    {
        var response = await AuthenticatedMemberHttpClient.GetAsync("/api/apps");

        response.ShouldBeSuccessfulGetRequest();
        var payload = (await response.DeserializeResponse<AppsResponse>())!;

        var permissions = payload.Apps.SelectMany(app => app.Permissions).ToArray();
        permissions.Should().NotBeEmpty();
        permissions.Should().OnlyContain(permission =>
            !string.IsNullOrWhiteSpace(permission.Scope)
            && !string.IsNullOrWhiteSpace(permission.Title)
            && !string.IsNullOrWhiteSpace(permission.Description)
        );
    }

    private static string[] ScopesFor(AppsResponse payload, AppSlug slug)
    {
        return payload.Apps.Single(app => app.Slug == slug).Permissions.Select(permission => permission.Scope).ToArray();
    }
}
