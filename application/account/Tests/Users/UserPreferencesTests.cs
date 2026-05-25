using System.Net;
using System.Net.Http.Json;
using Account.Database;
using Account.Features.Users.Commands;
using Account.Features.Users.Domain;
using Account.Features.Users.Queries;
using FluentAssertions;
using SharedKernel.Tests;
using SharedKernel.Validation;
using Xunit;

namespace Account.Tests.Users;

public sealed class UserPreferencesTests(AccountWebApplicationFactory factory)
    : EndpointBaseTest<AccountDbContext>(factory), IClassFixture<AccountWebApplicationFactory>
{
    private const string PreferencesRoute = "/api/account/users/me/preferences";

    [Fact]
    public async Task GetPreferences_WhenNoRowExists_ShouldReturnDefaults()
    {
        // Act
        var response = await AuthenticatedOwnerHttpClient.GetAsync(PreferencesRoute);

        // Assert
        response.ShouldBeSuccessfulGetRequest();
        var body = await response.Content.ReadFromJsonAsync<UserPreferencesResponse>();
        body.Should().NotBeNull();
        body!.TimeFormat.Should().Be(UserPreferences.DefaultTimeFormat);
        body.WeekStart.Should().Be(UserPreferences.DefaultWeekStart);
        body.Language.Should().Be(UserPreferences.DefaultLanguage);
        body.TimeZone.Should().Be(UserPreferences.DefaultTimeZone);
    }

    [Fact]
    public async Task PatchPreferences_WhenValidFullPayload_ShouldPersistAndReturnUpdatedValues()
    {
        // Arrange
        var command = new UpdateCurrentUserPreferencesCommand(
            TimeFormat.TwelveHour,
            DayOfWeek.Sunday,
            "da-DK",
            "Europe/Copenhagen"
        );

        // Act
        var response = await AuthenticatedOwnerHttpClient.PatchAsJsonAsync(PreferencesRoute, command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<UserPreferencesResponse>();
        body.Should().NotBeNull();
        body!.TimeFormat.Should().Be(TimeFormat.TwelveHour);
        body.WeekStart.Should().Be(DayOfWeek.Sunday);
        body.Language.Should().Be("da-DK");
        body.TimeZone.Should().Be("Europe/Copenhagen");

        // Round-trip through GET to confirm persistence.
        var getResponse = await AuthenticatedOwnerHttpClient.GetAsync(PreferencesRoute);
        var roundTripped = await getResponse.Content.ReadFromJsonAsync<UserPreferencesResponse>();
        roundTripped.Should().BeEquivalentTo(body);
    }

    [Fact]
    public async Task PatchPreferences_WhenPartialPayload_ShouldOnlyUpdateProvidedFields()
    {
        // Arrange — seed an explicit baseline.
        var seed = new UpdateCurrentUserPreferencesCommand(TimeFormat.TwelveHour, DayOfWeek.Sunday, "da-DK", "Europe/Copenhagen");
        (await AuthenticatedOwnerHttpClient.PatchAsJsonAsync(PreferencesRoute, seed)).EnsureSuccessStatusCode();

        // Act — change only the time zone.
        var patch = new UpdateCurrentUserPreferencesCommand(null, null, null, "America/Los_Angeles");
        var response = await AuthenticatedOwnerHttpClient.PatchAsJsonAsync(PreferencesRoute, patch);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<UserPreferencesResponse>();
        body!.TimeFormat.Should().Be(TimeFormat.TwelveHour); // unchanged
        body.WeekStart.Should().Be(DayOfWeek.Sunday);        // unchanged
        body.Language.Should().Be("da-DK");                   // unchanged
        body.TimeZone.Should().Be("America/Los_Angeles");    // updated
    }

    [Fact]
    public async Task PatchPreferences_WhenInvalidTimeZone_ShouldReturnValidationError()
    {
        // Arrange
        var command = new UpdateCurrentUserPreferencesCommand(null, null, null, "Not/A_RealZone");

        // Act
        var response = await AuthenticatedOwnerHttpClient.PatchAsJsonAsync(PreferencesRoute, command);

        // Assert
        var expectedErrors = new[]
        {
            new ErrorDetail("timeZone", "Time zone must be a valid IANA identifier (e.g. 'UTC', 'Europe/Copenhagen').")
        };
        await response.ShouldHaveErrorStatusCode(HttpStatusCode.BadRequest, expectedErrors);
    }

    [Fact]
    public async Task PatchPreferences_WhenUnsupportedLanguage_ShouldReturnValidationError()
    {
        // Arrange
        var command = new UpdateCurrentUserPreferencesCommand(null, null, "fr-FR", null);

        // Act
        var response = await AuthenticatedOwnerHttpClient.PatchAsJsonAsync(PreferencesRoute, command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PatchPreferences_WhenNotAuthenticated_ShouldReturnUnauthorized()
    {
        // Arrange
        var command = new UpdateCurrentUserPreferencesCommand(TimeFormat.TwelveHour, null, null, null);

        // Act
        var response = await AnonymousHttpClient.PatchAsJsonAsync(PreferencesRoute, command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetCurrentUser_ShouldEmbedPreferences()
    {
        // Arrange — set a non-default preference so we can prove embedding works.
        var seed = new UpdateCurrentUserPreferencesCommand(TimeFormat.TwelveHour, DayOfWeek.Sunday, "da-DK", "Europe/Copenhagen");
        (await AuthenticatedOwnerHttpClient.PatchAsJsonAsync(PreferencesRoute, seed)).EnsureSuccessStatusCode();

        // Act
        var response = await AuthenticatedOwnerHttpClient.GetAsync("/api/account/users/me");

        // Assert
        response.ShouldBeSuccessfulGetRequest();
        var body = await response.Content.ReadFromJsonAsync<CurrentUserResponse>();
        body.Should().NotBeNull();
        body!.Preferences.TimeFormat.Should().Be(TimeFormat.TwelveHour);
        body.Preferences.WeekStart.Should().Be(DayOfWeek.Sunday);
        body.Preferences.Language.Should().Be("da-DK");
        body.Preferences.TimeZone.Should().Be("Europe/Copenhagen");
    }

    [Fact]
    public void CreateDefault_ShouldReturnSensibleDefaults()
    {
        // Act
        var defaults = UserPreferences.CreateDefault(DatabaseSeeder.Tenant1Owner.Id);

        // Assert
        defaults.UserId.Should().Be(DatabaseSeeder.Tenant1Owner.Id);
        defaults.TimeFormat.Should().Be(TimeFormat.TwentyFourHour);
        defaults.WeekStart.Should().Be(DayOfWeek.Monday);
        defaults.Language.Should().Be("en-US");
        defaults.TimeZone.Should().Be("UTC");
    }

    [Fact]
    public void Update_WhenNullArguments_ShouldPreserveExistingValues()
    {
        // Arrange
        var prefs = UserPreferences.CreateDefault(DatabaseSeeder.Tenant1Owner.Id);
        prefs.Update(TimeFormat.TwelveHour, DayOfWeek.Sunday, "da-DK", "Europe/Copenhagen");

        // Act — apply an all-null partial update.
        prefs.Update(null, null, null, null);

        // Assert
        prefs.TimeFormat.Should().Be(TimeFormat.TwelveHour);
        prefs.WeekStart.Should().Be(DayOfWeek.Sunday);
        prefs.Language.Should().Be("da-DK");
        prefs.TimeZone.Should().Be("Europe/Copenhagen");
    }
}
