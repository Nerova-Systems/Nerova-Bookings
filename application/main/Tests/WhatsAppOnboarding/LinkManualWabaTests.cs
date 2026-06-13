using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Main.Database;
using Main.Features;
using SharedKernel.Tests.Persistence;
using Xunit;

namespace Main.Tests.WhatsAppOnboarding;

/// <summary>
///     Tests for the manual WABA link — the developer/owner escape hatch for WABAs living in the same
///     Meta Business Portfolio as the app (where Embedded Signup is blocked by Meta). Uses the mock Meta
///     client; the supplied token must be protected at rest exactly like the embedded-signup path.
/// </summary>
public sealed class LinkManualWabaTests : EndpointBaseTest<MainDbContext>
{
    private const string ManualLinkUrl = "/api/main/whatsapp/manual-link";
    private const string WabaId = "388302184850579";
    private const string PhoneNumberId = "117720227547289";
    private const string AccessToken = "manually-issued-token";

    [Fact]
    public async Task LinkManualWaba_WhenOwner_ShouldOnboardProtectTokenAndProvisionFlows()
    {
        // Arrange
        var command = new { wabaId = WabaId, phoneNumberId = PhoneNumberId, accessToken = AccessToken };
        TelemetryEventsCollectorSpy.Reset();

        // Act
        var response = await AuthenticatedOwnerHttpClient.PostAsJsonAsync(ManualLinkUrl, command);

        // Assert
        response.EnsureSuccessStatusCode();
        Connection.ExecuteScalar<string>("SELECT status FROM whats_app_business_accounts WHERE tenant_id = @tenantId", [new { tenantId = DatabaseSeeder.TenantId.Value }]).Should().Be("Connected");

        var storedAccessToken = Connection.ExecuteScalar<string>("SELECT access_token FROM whats_app_business_accounts WHERE tenant_id = @tenantId", [new { tenantId = DatabaseSeeder.TenantId.Value }]);
        storedAccessToken.Should().NotBeNullOrEmpty();
        storedAccessToken.Should().NotBe(AccessToken, "the token must be encrypted at rest");

        TelemetryEventsCollectorSpy.CollectedEvents.Should().ContainSingle(e => e is WhatsAppBusinessAccountOnboarded);
    }

    [Fact]
    public async Task LinkManualWaba_WhenAlreadyOnboarded_ShouldBeIdempotent()
    {
        // Arrange
        var command = new { wabaId = WabaId, phoneNumberId = PhoneNumberId, accessToken = AccessToken };
        (await AuthenticatedOwnerHttpClient.PostAsJsonAsync(ManualLinkUrl, command)).EnsureSuccessStatusCode();
        TelemetryEventsCollectorSpy.Reset();

        // Act
        var response = await AuthenticatedOwnerHttpClient.PostAsJsonAsync(ManualLinkUrl, command);

        // Assert
        response.EnsureSuccessStatusCode();
        Connection.ExecuteScalar<long>("SELECT COUNT(*) FROM whats_app_business_accounts WHERE tenant_id = @tenantId", [new { tenantId = DatabaseSeeder.TenantId.Value }]).Should().Be(1);
        TelemetryEventsCollectorSpy.CollectedEvents.Should().BeEmpty();
    }

    [Fact]
    public async Task LinkManualWaba_WhenMember_ShouldReturnForbidden()
    {
        // Act
        var response = await AuthenticatedMemberHttpClient.PostAsJsonAsync(ManualLinkUrl, new { wabaId = WabaId, phoneNumberId = PhoneNumberId, accessToken = AccessToken });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        Connection.ExecuteScalar<long>("SELECT COUNT(*) FROM whats_app_business_accounts", []).Should().Be(0);
    }

    [Fact]
    public async Task LinkManualWaba_WhenAnonymous_ShouldReturnUnauthorized()
    {
        // Act
        var response = await AnonymousHttpClient.PostAsJsonAsync(ManualLinkUrl, new { wabaId = WabaId, phoneNumberId = PhoneNumberId, accessToken = AccessToken });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task LinkManualWaba_WhenAccessTokenMissing_ShouldReturnBadRequest()
    {
        // Act
        var response = await AuthenticatedOwnerHttpClient.PostAsJsonAsync(ManualLinkUrl, new { wabaId = WabaId, phoneNumberId = PhoneNumberId, accessToken = "" });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
