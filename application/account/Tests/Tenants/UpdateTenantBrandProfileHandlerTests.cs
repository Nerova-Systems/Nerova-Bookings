using Account.Features.Subscriptions.Domain;
using Account.Features.Tenants.Commands;
using Account.Features.Tenants.Domain;
using Account.Features.Users.Domain;
using FluentAssertions;
using NSubstitute;
using SharedKernel.Authentication;
using SharedKernel.Cqrs;
using SharedKernel.Domain;
using SharedKernel.ExecutionContext;
using SharedKernel.Telemetry;
using Xunit;

namespace Account.Tests.Tenants;

/// <summary>
///     Tier enforcement and persistence for <see cref="UpdateTenantBrandProfileHandler" />.
///     Direct handler instantiation with NSubstitute mocks; no <c>WebApplicationFactory</c>.
/// </summary>
public sealed class UpdateTenantBrandProfileHandlerTests
{
    private static (
        UpdateTenantBrandProfileHandler sut,
        ITenantRepository tenants,
        ISubscriptionRepository subs
        ) BuildSut()
    {
        var tenants = Substitute.For<ITenantRepository>();
        var subs = Substitute.For<ISubscriptionRepository>();
        var executionContext = Substitute.For<IExecutionContext>();
        executionContext.UserInfo.Returns(new UserInfo { IsAuthenticated = true, Role = nameof(UserRole.Owner) });
        var events = Substitute.For<ITelemetryEventsCollector>();

        var tenant = Tenant.Create("owner@acme.test", existingCount: 0);
        tenants.GetCurrentTenantAsync(Arg.Any<CancellationToken>()).Returns(tenant);

        var sut = new UpdateTenantBrandProfileHandler(tenants, subs, executionContext, events);
        return (sut, tenants, subs);
    }

    private static UpdateTenantBrandProfileCommand BasicCommand()
    {
        return new UpdateTenantBrandProfileCommand
        {
            BusinessDisplayName = "Acme",
            BrandVertical = MetaBusinessVertical.Retail
        };
    }

    [Fact]
    public async Task NonOwner_ReturnsForbidden()
    {
        var tenants = Substitute.For<ITenantRepository>();
        var subs = Substitute.For<ISubscriptionRepository>();
        var executionContext = Substitute.For<IExecutionContext>();
        executionContext.UserInfo.Returns(new UserInfo { IsAuthenticated = true, Role = nameof(UserRole.Member) });
        var sut = new UpdateTenantBrandProfileHandler(
            tenants, subs, executionContext, Substitute.For<ITelemetryEventsCollector>()
        );

        var result = await sut.Handle(BasicCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(System.Net.HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task FreeTier_LogoUpload_ReturnsForbidden()
    {
        var (sut, _, subs) = BuildSut();
        // Free tier = no subscription row.
        subs.GetByTenantIdUnfilteredAsync(Arg.Any<TenantId>(), Arg.Any<CancellationToken>()).Returns((Subscription?)null);

        var result = await sut.Handle(
            BasicCommand() with { BrandLogoUrl = "/logos/123/abc.png" },
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(System.Net.HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task FreeTier_AboutText_ReturnsForbidden()
    {
        var (sut, _, subs) = BuildSut();
        subs.GetByTenantIdUnfilteredAsync(Arg.Any<TenantId>(), Arg.Any<CancellationToken>()).Returns((Subscription?)null);

        var result = await sut.Handle(
            BasicCommand() with { BrandAboutText = "Open 9-5" },
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(System.Net.HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task BasisTier_TwoWebsites_ReturnsForbidden()
    {
        // Basis allows only 1 website; Standard+ unlocks the second slot. Subscription.Create
        // defaults to Basis so no extra setup needed here.
        var (sut, _, subs) = BuildSut();
        subs.GetByTenantIdUnfilteredAsync(Arg.Any<TenantId>(), Arg.Any<CancellationToken>())
            .Returns(Subscription.Create(new TenantId(1)));

        var result = await sut.Handle(
            BasicCommand() with { BrandWebsites = ["https://a.test", "https://b.test"] },
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(System.Net.HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task BasisTier_ValidProfile_Persists()
    {
        var (sut, tenants, subs) = BuildSut();
        subs.GetByTenantIdUnfilteredAsync(Arg.Any<TenantId>(), Arg.Any<CancellationToken>())
            .Returns(Subscription.Create(new TenantId(1)));

        var result = await sut.Handle(BasicCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        tenants.Received(1).Update(Arg.Any<Tenant>());
    }
}
