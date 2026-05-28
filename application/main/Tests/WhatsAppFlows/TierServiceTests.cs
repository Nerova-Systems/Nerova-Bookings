using FluentAssertions;
using Main.Features.WhatsAppFlows.Infrastructure;
using Microsoft.Extensions.Caching.Memory;
using NSubstitute;
using SharedKernel.Domain;
using Xunit;

namespace Main.Tests.WhatsAppFlows;

public sealed class TierServiceTests
{
    private static readonly TenantId TenantId = new(202);

    private static (DefaultTierService sut, IWhatsAppSubscriptionLookup lookup) BuildSut()
    {
        var lookup = Substitute.For<IWhatsAppSubscriptionLookup>();
        var cache = new MemoryCache(new MemoryCacheOptions());
        return (new DefaultTierService(lookup, cache), lookup);
    }

    [Theory]
    [InlineData(null, TenantTier.Starter)]
    [InlineData("Basis", TenantTier.Professional)]
    [InlineData("Standard", TenantTier.Business)]
    [InlineData("Premium", TenantTier.Enterprise)]
    public async Task GetTierAsync_MapsPlanToTier(string? plan, TenantTier expected)
    {
        var (sut, lookup) = BuildSut();
        lookup.GetSubscriptionPlanAsync(TenantId, Arg.Any<CancellationToken>()).Returns(plan);

        var tier = await sut.GetTierAsync(TenantId, CancellationToken.None);

        tier.Should().Be(expected);
    }

    [Fact]
    public async Task GetTierAsync_CachesResult_PerTenant()
    {
        var (sut, lookup) = BuildSut();
        lookup.GetSubscriptionPlanAsync(TenantId, Arg.Any<CancellationToken>()).Returns("Premium");

        await sut.GetTierAsync(TenantId, CancellationToken.None);
        await sut.GetTierAsync(TenantId, CancellationToken.None);

        await lookup.Received(1).GetSubscriptionPlanAsync(TenantId, Arg.Any<CancellationToken>());
    }
}
