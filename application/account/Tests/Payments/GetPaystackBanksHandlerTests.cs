using Account.Features.Payments.Queries;
using Account.Integrations.Paystack;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Xunit;

namespace Account.Tests.Payments;

public sealed class GetPaystackBanksHandlerTests
{
    private static readonly IReadOnlyList<PaystackBankDto> SampleBanks =
    [
        new("044", "Access Bank"),
        new("058", "Guaranty Trust Bank")
    ];

    [Fact]
    public async Task Handle_WhenApiReturnsData_ShouldReturnBanks()
    {
        // Arrange
        var (handler, _) = BuildHandler(SampleBanks);

        // Act
        var result = await handler.Handle(new GetPaystackBanksQuery(), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Banks.Should().HaveCount(2);
        result.Value.Banks.Should().ContainSingle(b => b.Code == "044" && b.Name == "Access Bank");
        result.Value.Banks.Should().ContainSingle(b => b.Code == "058" && b.Name == "Guaranty Trust Bank");
    }

    [Fact]
    public async Task Handle_WhenCalledTwice_ShouldOnlyCallApiOnce()
    {
        // Arrange
        var (handler, paystackClient) = BuildHandler(SampleBanks);
        var query = new GetPaystackBanksQuery();

        // Act
        await handler.Handle(query, CancellationToken.None);
        await handler.Handle(query, CancellationToken.None);

        // Assert
        await paystackClient.Received(1).GetBanksAsync("ZA", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenApiReturnsEmptyList_ShouldReturnEmptyBanks()
    {
        // Arrange
        var (handler, _) = BuildHandler([]);

        // Act
        var result = await handler.Handle(new GetPaystackBanksQuery(), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Banks.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WhenDifferentCountries_ShouldCacheSeparately()
    {
        // Arrange
        var paystackClient = Substitute.For<IPaystackClient>();
        paystackClient.GetBanksAsync("ZA", Arg.Any<CancellationToken>()).Returns(SampleBanks);
        paystackClient.GetBanksAsync("NG", Arg.Any<CancellationToken>()).Returns(new List<PaystackBankDto> { new("033", "UBA") });

        var (handler, _) = BuildHandler(paystackClient);

        // Act
        var zaResult = await handler.Handle(new GetPaystackBanksQuery(), CancellationToken.None);
        var ngResult = await handler.Handle(new GetPaystackBanksQuery("NG"), CancellationToken.None);

        // Assert
        zaResult.Value!.Banks.Should().HaveCount(2);
        ngResult.Value!.Banks.Should().HaveCount(1);
        ngResult.Value.Banks[0].Code.Should().Be("033");
    }

    private static (GetPaystackBanksHandler handler, IPaystackClient paystackClient) BuildHandler(
        IReadOnlyList<PaystackBankDto> bankResponse)
    {
        var paystackClient = Substitute.For<IPaystackClient>();
        paystackClient.GetBanksAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(bankResponse);
        return BuildHandler(paystackClient);
    }

    private static (GetPaystackBanksHandler handler, IPaystackClient paystackClient) BuildHandler(
        IPaystackClient paystackClient)
    {
        var services = new ServiceCollection();
        services.AddMemoryCache();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(
            new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["Paystack:AllowMockProvider"] = "false",
                        ["Paystack:SubscriptionEnabled"] = "true"
                    }
                )
                .Build()
        );
        services.AddKeyedScoped<IPaystackClient>("paystack", (_, _) => paystackClient);
        services.AddSingleton(Substitute.For<IHttpContextAccessor>());
        services.AddScoped<PaystackClientFactory>();

        var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<PaystackClientFactory>();
        var memoryCache = provider.GetRequiredService<IMemoryCache>();

        return (new GetPaystackBanksHandler(factory, memoryCache), paystackClient);
    }
}
