using Account.Features.Subscriptions.Domain;
using Account.Features.Tenants.Commands;
using Account.Features.Tenants.Domain;
using Account.Features.Users.Domain;
using Account.Features.WhatsApp.Domain;
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
///     Phase 7a — tier enforcement and outbox enqueue for
///     <see cref="UpdateTenantBrandProfileHandler" />. Mirrors the layout of
///     <c>LinkWabaAccountHandlerTierTests</c> — direct handler instantiation with NSubstitute
///     mocks; no <c>WebApplicationFactory</c>.
/// </summary>
public sealed class UpdateTenantBrandProfileHandlerTests
{
    private static (
        UpdateTenantBrandProfileHandler sut,
        ITenantRepository tenants,
        ISubscriptionRepository subs,
        IWabaConfigurationRepository waba,
        IWabaProfileSyncOutboxRepository outbox
        ) BuildSut()
    {
        var tenants = Substitute.For<ITenantRepository>();
        var subs = Substitute.For<ISubscriptionRepository>();
        var waba = Substitute.For<IWabaConfigurationRepository>();
        var outbox = Substitute.For<IWabaProfileSyncOutboxRepository>();
        var executionContext = Substitute.For<IExecutionContext>();
        executionContext.UserInfo.Returns(new UserInfo { IsAuthenticated = true, Role = nameof(UserRole.Owner) });
        var time = TimeProvider.System;
        var events = Substitute.For<ITelemetryEventsCollector>();

        var tenant = Tenant.Create("owner@acme.test", existingCount: 0);
        tenants.GetCurrentTenantAsync(Arg.Any<CancellationToken>()).Returns(tenant);

        var sut = new UpdateTenantBrandProfileHandler(
            tenants, subs, waba, outbox, executionContext, time, events
        );
        return (sut, tenants, subs, waba, outbox);
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
        var (_, tenants, subs, waba, outbox) = BuildSut();
        var executionContext = Substitute.For<IExecutionContext>();
        executionContext.UserInfo.Returns(new UserInfo { IsAuthenticated = true, Role = nameof(UserRole.Member) });
        var sut = new UpdateTenantBrandProfileHandler(
            tenants, subs, waba, outbox, executionContext, TimeProvider.System,
            Substitute.For<ITelemetryEventsCollector>()
        );

        var result = await sut.Handle(BasicCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(System.Net.HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task FreeTier_LogoUpload_ReturnsForbidden()
    {
        var (sut, _, subs, _, _) = BuildSut();
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
        var (sut, _, subs, _, _) = BuildSut();
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
        var (sut, _, subs, _, _) = BuildSut();
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
    public async Task BasisTier_NoWabaLinked_PersistsButDoesNotEnqueue()
    {
        var (sut, _, subs, waba, outbox) = BuildSut();
        subs.GetByTenantIdUnfilteredAsync(Arg.Any<TenantId>(), Arg.Any<CancellationToken>())
            .Returns(Subscription.Create(new TenantId(1)));
        waba.GetByTenantIdAsync(Arg.Any<TenantId>(), Arg.Any<CancellationToken>()).Returns((WabaConfiguration?)null);

        var result = await sut.Handle(BasicCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await outbox.DidNotReceive().AddAsync(Arg.Any<WabaProfileSyncOutbox>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task BasisTier_WithLinkedWaba_EnqueuesOutboxRow()
    {
        var (sut, _, subs, waba, outbox) = BuildSut();
        subs.GetByTenantIdUnfilteredAsync(Arg.Any<TenantId>(), Arg.Any<CancellationToken>())
            .Returns(Subscription.Create(new TenantId(1)));
        var wabaConfig = WabaConfiguration.Create(new TenantId(1), "waba_1", "phone_1", "+1 555 0100");
        waba.GetByTenantIdAsync(Arg.Any<TenantId>(), Arg.Any<CancellationToken>()).Returns(wabaConfig);

        var result = await sut.Handle(BasicCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await outbox.Received(1).AddAsync(
            Arg.Is<WabaProfileSyncOutbox>(o => o.Status == WabaProfileSyncStatus.Pending && o.PhoneNumberId == "phone_1"),
            Arg.Any<CancellationToken>()
        );
    }
}
