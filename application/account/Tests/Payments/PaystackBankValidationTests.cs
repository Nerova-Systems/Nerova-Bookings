using System.Net;
using System.Net.Http.Json;
using Account.Database;
using Account.Features.WhatsApp.Commands;
using Account.Features.WhatsApp.Domain;
using Account.Tests.WhatsApp;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using SharedKernel.Cqrs;
using SharedKernel.Tests;
using Xunit;

namespace Account.Tests.Payments;

public sealed class PaystackBankValidationTests(WhatsAppWebApplicationFactory factory)
    : EndpointBaseTest<AccountDbContext>(factory), IClassFixture<WhatsAppWebApplicationFactory>
{
    private readonly WhatsAppWebApplicationFactory _factory = factory;

    [Fact]
    public async Task ConnectPaystack_WhenBankCodeIsValid_ShouldSucceed()
    {
        // Arrange
        _factory.MockSubaccountService
            .CreateSubaccount(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<decimal>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result<string>.Success("ACCT_validbank123")));

        using (var scope = Provider.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AccountDbContext>();
            var config = WabaConfiguration.Create(
                DatabaseSeeder.Tenant1.Id,
                "waba_bank_valid_001",
                "phone_bank_valid_001",
                "+27 81 000 0010"
            );
            dbContext.Set<WabaConfiguration>().Add(config);
            await dbContext.SaveChangesAsync();
        }

        // Act — "044" is Access Bank, included in MockPaystackClient
        var response = await AuthenticatedOwnerHttpClient.PostAsJsonAsync(
            "/api/whatsapp/connect-paystack",
            new
            {
                businessName = "Test Business",
                bankCode = "044",
                accountNumber = "0123456789",
                percentageFee = 2.5m
            }
        );

        // Assert
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<ConnectPaystackSubaccountResponse>();
        result!.SubaccountCode.Should().Be("ACCT_validbank123");
    }

    [Fact]
    public async Task ConnectPaystack_WhenBankCodeIsInvalid_ShouldReturnBadRequest()
    {
        // Arrange — seed config so the request gets past the NotFound check
        using (var scope = Provider.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AccountDbContext>();
            var config = WabaConfiguration.Create(
                DatabaseSeeder.Tenant1.Id,
                "waba_bank_invalid_001",
                "phone_bank_invalid_001",
                "+27 81 000 0011"
            );
            dbContext.Set<WabaConfiguration>().Add(config);
            await dbContext.SaveChangesAsync();
        }

        // Act — "999" is not in the MockPaystackClient bank list
        var response = await AuthenticatedOwnerHttpClient.PostAsJsonAsync(
            "/api/whatsapp/connect-paystack",
            new
            {
                businessName = "Test Business",
                bankCode = "999",
                accountNumber = "0123456789",
                percentageFee = 2.5m
            }
        );

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
