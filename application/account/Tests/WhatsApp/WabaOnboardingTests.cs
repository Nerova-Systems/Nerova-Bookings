using System.Net;
using System.Net.Http.Json;
using Account.Database;
using Account.Features.WhatsApp.Commands;
using Account.Features.WhatsApp.Domain;
using Account.Features.WhatsApp.Queries;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using SharedKernel.Cqrs;
using SharedKernel.Tests;
using Xunit;

namespace Account.Tests.WhatsApp;

/// <summary>
///     Integration tests for the four WhatsApp onboarding endpoints:
///     <list type="bullet">
///         <item><c>POST /api/whatsapp/link-waba</c></item>
///         <item><c>POST /api/whatsapp/generate-key-pair</c></item>
///         <item><c>POST /api/whatsapp/connect-paystack</c></item>
///         <item><c>GET  /api/whatsapp/onboarding-status</c></item>
///     </list>
/// </summary>
public sealed class WabaOnboardingTests(WhatsAppWebApplicationFactory factory)
    : EndpointBaseTest<AccountDbContext>(factory), IClassFixture<WhatsAppWebApplicationFactory>
{
    // ──────────────────────────────────────────────────────────────────────────
    // POST /api/whatsapp/link-waba
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task LinkWabaAccount_WhenAnonymous_ShouldReturnUnauthorized()
    {
        var response = await AnonymousHttpClient.PostAsJsonAsync(
            "/api/whatsapp/link-waba",
            new { wabaId = "waba_123", phoneNumberId = "phone_123", displayPhoneNumber = "+27 81 123 4567" }
        );

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task LinkWabaAccount_WhenValid_ShouldCreateConfigurationWithWabaLinkedStatus()
    {
        // Act
        var response = await AuthenticatedOwnerHttpClient.PostAsJsonAsync(
            "/api/whatsapp/link-waba",
            new { wabaId = "waba_abc123", phoneNumberId = "phone_abc123", displayPhoneNumber = "+27 81 000 0001" }
        );

        // Assert
        response.ShouldHaveEmptyHeaderAndLocationOnSuccess();

        using var scope = Provider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IWabaConfigurationRepository>();
        var config = await repository.GetByTenantIdAsync(DatabaseSeeder.Tenant1.Id, CancellationToken.None);

        config.Should().NotBeNull();
        config!.WabaId.Should().Be("waba_abc123");
        config.PhoneNumberId.Should().Be("phone_abc123");
        config.DisplayPhoneNumber.Should().Be("+27 81 000 0001");
        config.OnboardingGateStatus.Should().Be(WabaOnboardingStatus.WabaLinked);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // POST /api/whatsapp/generate-key-pair
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GenerateWabaKeyPair_WhenNoConfigurationExists_ShouldReturnNotFound()
    {
        var response = await AuthenticatedOwnerHttpClient.PostAsync("/api/whatsapp/generate-key-pair", null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GenerateWabaKeyPair_WhenWabaLinked_ShouldReturnPublicKeyAndFingerprint()
    {
        // Arrange — seed a WabaLinked configuration for Tenant1
        using (var scope = Provider.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AccountDbContext>();
            var config = WabaConfiguration.Create(
                DatabaseSeeder.Tenant1.Id,
                "waba_keygen_001",
                "phone_keygen_001",
                "+27 81 000 0002"
            );
            dbContext.Set<WabaConfiguration>().Add(config);
            await dbContext.SaveChangesAsync();
        }

        // Act
        var response = await AuthenticatedOwnerHttpClient.PostAsync("/api/whatsapp/generate-key-pair", null);

        // Assert
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<GenerateWabaKeyPairResponse>();
        result.Should().NotBeNull();
        result!.PublicKeyPem.Should().StartWith("-----BEGIN PUBLIC KEY-----");
        result.Fingerprint.Should().HaveLength(64);
        result.Fingerprint.Should().MatchRegex("^[0-9a-f]{64}$");

        // Verify key pair was persisted
        using var verifyScope = Provider.CreateScope();
        var repository = verifyScope.ServiceProvider.GetRequiredService<IWabaConfigurationRepository>();
        var stored = await repository.GetByTenantIdAsync(DatabaseSeeder.Tenant1.Id, CancellationToken.None);
        stored!.EncryptedPrivateKey.Should().NotBeNullOrEmpty();
        stored.PrivateKeyIv.Should().NotBeNullOrEmpty();
        stored.PublicKeyFingerprint.Should().Be(result.Fingerprint);
        stored.OnboardingGateStatus.Should().Be(WabaOnboardingStatus.KeyPairGenerated);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // POST /api/whatsapp/connect-paystack
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ConnectPaystackSubaccount_WhenSubaccountCreated_ShouldStoreSubaccountCode()
    {
        // Arrange — configure the mock and seed a WabaLinked config
        factory.MockSubaccountService
            .CreateSubaccount(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<decimal>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result<string>.Success("ACCT_mock123")));

        using (var scope = Provider.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AccountDbContext>();
            var config = WabaConfiguration.Create(
                DatabaseSeeder.Tenant1.Id,
                "waba_paystack_001",
                "phone_paystack_001",
                "+27 81 000 0003"
            );
            dbContext.Set<WabaConfiguration>().Add(config);
            await dbContext.SaveChangesAsync();
        }

        // Act
        var response = await AuthenticatedOwnerHttpClient.PostAsJsonAsync(
            "/api/whatsapp/connect-paystack",
            new
            {
                businessName = "Nerova Demo",
                bankCode = "044",
                accountNumber = "0123456789",
                percentageFee = 2.5m
            }
        );

        // Assert
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<ConnectPaystackSubaccountResponse>();
        result.Should().NotBeNull();
        result!.SubaccountCode.Should().Be("ACCT_mock123");

        // Verify subaccount code was persisted
        using var verifyScope = Provider.CreateScope();
        var repository = verifyScope.ServiceProvider.GetRequiredService<IWabaConfigurationRepository>();
        var stored = await repository.GetByTenantIdAsync(DatabaseSeeder.Tenant1.Id, CancellationToken.None);
        stored!.SubaccountCode.Should().Be("ACCT_mock123");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // GET /api/whatsapp/onboarding-status
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetOnboardingStatus_WhenNoConfigurationExists_ShouldReturnNotFound()
    {
        var response = await AuthenticatedOwnerHttpClient.GetAsync("/api/whatsapp/onboarding-status");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetOnboardingStatus_WhenAllGatesComplete_ShouldReturnCompleteWithCanPublishFlow()
    {
        // Arrange — seed a fully completed WabaConfiguration
        using (var scope = Provider.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AccountDbContext>();
            var config = WabaConfiguration.Create(
                DatabaseSeeder.Tenant1.Id,
                "waba_complete_001",
                "phone_complete_001",
                "+27 81 000 0004"
            );
            config.SetKeyPair("enc_key_blob", "iv_blob", "a1b2c3d4e5f67890a1b2c3d4e5f67890a1b2c3d4e5f67890a1b2c3d4e5f67890");
            config.SetSubaccountCode("ACCT_complete001"); // all gates done → Complete
            dbContext.Set<WabaConfiguration>().Add(config);
            await dbContext.SaveChangesAsync();
        }

        // Act
        var response = await AuthenticatedOwnerHttpClient.GetAsync("/api/whatsapp/onboarding-status");

        // Assert
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<WabaOnboardingStatusResponse>();
        result.Should().NotBeNull();
        result!.Status.Should().Be(WabaOnboardingStatus.Complete);
        result.CanPublishFlow.Should().BeTrue();
        result.WabaLinked.Should().BeTrue();
        result.KeyPairGenerated.Should().BeTrue();
        result.PaystackConnected.Should().BeTrue();
        result.DisplayPhoneNumber.Should().Be("+27 81 000 0004");
        result.PublicKeyFingerprint.Should().Be("a1b2c3d4e5f67890a1b2c3d4e5f67890a1b2c3d4e5f67890a1b2c3d4e5f67890");
    }
}
