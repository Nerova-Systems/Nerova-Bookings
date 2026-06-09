using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Main.Database;
using Main.Features;
using Main.Integrations.Meta;
using SharedKernel.Tests;
using SharedKernel.Tests.Persistence;
using SharedKernel.Validation;
using Xunit;

namespace Main.Tests.WhatsAppOnboarding;

public sealed class CompleteEmbeddedSignupTests : EndpointBaseTest<MainDbContext>
{
    private const string WabaId = "123456789012345";
    private const string PhoneNumberId = "987654321098765";

    [Fact]
    public async Task CompleteEmbeddedSignup_WhenOwner_ShouldOnboardAndCollectTelemetry()
    {
        // Arrange
        var command = new { code = "valid-auth-code", wabaId = WabaId, phoneNumberId = PhoneNumberId };
        TelemetryEventsCollectorSpy.Reset();

        // Act
        var response = await AuthenticatedOwnerHttpClient.PostAsJsonAsync("/api/main/whatsapp/embedded-signup/complete", command);

        // Assert
        response.EnsureSuccessStatusCode();

        var businessName = Connection.ExecuteScalar<string>(
            "SELECT business_name FROM whats_app_business_accounts WHERE tenant_id = @tenantId",
            [new { tenantId = DatabaseSeeder.TenantId.Value }]
        );
        businessName.Should().Be(MockMetaGraphClient.MockBusinessName);

        var status = Connection.ExecuteScalar<string>(
            "SELECT status FROM whats_app_business_accounts WHERE tenant_id = @tenantId",
            [new { tenantId = DatabaseSeeder.TenantId.Value }]
        );
        status.Should().Be("Connected");

        // The access token must be encrypted at rest, never persisted as the plaintext provider token.
        var accessToken = Connection.ExecuteScalar<string>(
            "SELECT access_token FROM whats_app_business_accounts WHERE tenant_id = @tenantId",
            [new { tenantId = DatabaseSeeder.TenantId.Value }]
        );
        accessToken.Should().NotBeNullOrEmpty();
        accessToken.Should().NotBe(MockMetaGraphClient.MockAccessToken);

        TelemetryEventsCollectorSpy.CollectedEvents.Should().ContainSingle(e => e is WhatsAppBusinessAccountOnboarded);
    }

    [Fact]
    public async Task CompleteEmbeddedSignup_WhenAlreadyOnboarded_ShouldBeIdempotent()
    {
        // Arrange
        var command = new { code = "valid-auth-code", wabaId = WabaId, phoneNumberId = PhoneNumberId };
        (await AuthenticatedOwnerHttpClient.PostAsJsonAsync("/api/main/whatsapp/embedded-signup/complete", command)).EnsureSuccessStatusCode();
        TelemetryEventsCollectorSpy.Reset();

        // Act
        var response = await AuthenticatedOwnerHttpClient.PostAsJsonAsync("/api/main/whatsapp/embedded-signup/complete", command);

        // Assert
        response.EnsureSuccessStatusCode();

        var rowCount = Connection.ExecuteScalar<long>(
            "SELECT COUNT(*) FROM whats_app_business_accounts WHERE tenant_id = @tenantId",
            [new { tenantId = DatabaseSeeder.TenantId.Value }]
        );
        rowCount.Should().Be(1);

        TelemetryEventsCollectorSpy.CollectedEvents.Should().BeEmpty();
    }

    [Fact]
    public async Task CompleteEmbeddedSignup_WhenMember_ShouldReturnForbidden()
    {
        // Arrange
        var command = new { code = "valid-auth-code", wabaId = WabaId, phoneNumberId = PhoneNumberId };
        TelemetryEventsCollectorSpy.Reset();

        // Act
        var response = await AuthenticatedMemberHttpClient.PostAsJsonAsync("/api/main/whatsapp/embedded-signup/complete", command);

        // Assert
        await response.ShouldHaveErrorStatusCode(HttpStatusCode.Forbidden, "Only owners can connect a WhatsApp Business Account.");

        var rowCount = Connection.ExecuteScalar<long>(
            "SELECT COUNT(*) FROM whats_app_business_accounts WHERE tenant_id = @tenantId",
            [new { tenantId = DatabaseSeeder.TenantId.Value }]
        );
        rowCount.Should().Be(0);
    }

    [Fact]
    public async Task CompleteEmbeddedSignup_WhenFieldsAreEmpty_ShouldReturnValidationErrors()
    {
        // Arrange
        var command = new { code = "", wabaId = "", phoneNumberId = "" };

        // Act
        var response = await AuthenticatedOwnerHttpClient.PostAsJsonAsync("/api/main/whatsapp/embedded-signup/complete", command);

        // Assert
        await response.ShouldHaveErrorStatusCode(HttpStatusCode.BadRequest, [
                new ErrorDetail("Code", "The authorization code is required."),
                new ErrorDetail("WabaId", "The WhatsApp Business Account ID is required."),
                new ErrorDetail("PhoneNumberId", "The phone number ID is required.")
            ]
        );
    }
}
