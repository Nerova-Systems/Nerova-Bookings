using Account.Features.Subscriptions.Domain;
using Account.Features.WhatsApp.Commands;
using Account.Features.WhatsApp.Domain;
using FluentAssertions;
using NSubstitute;
using SharedKernel.Domain;
using Xunit;

namespace Account.Tests.WhatsApp;

/// <summary>
///     Unit tests for the Phase 6 phone-number-limit tier gating in
///     <see cref="LinkWabaAccountHandler" />. The schema is 1-WABA-per-tenant so the only
///     operation that increases the phone-number count is switching <c>PhoneNumberId</c> on an
///     existing row.
/// </summary>
public sealed class LinkWabaAccountHandlerTierTests
{
    private static readonly TenantId TenantId = new(404);

    private static (LinkWabaAccountHandler sut, IWabaConfigurationRepository repo, ISubscriptionRepository subs) BuildSut()
    {
        var repo = Substitute.For<IWabaConfigurationRepository>();
        var subs = Substitute.For<ISubscriptionRepository>();
        return (new LinkWabaAccountHandler(repo, subs), repo, subs);
    }

    [Fact]
    public async Task Handle_NoExistingConfig_AlwaysAllowed()
    {
        var (sut, repo, _) = BuildSut();
        repo.GetByTenantIdAsync(TenantId, Arg.Any<CancellationToken>()).Returns((WabaConfiguration?)null);

        var result = await sut.Handle(
            new LinkWabaAccountCommand(TenantId, "waba_1", "phone_1", "+1 555 0100"),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue();
        await repo.Received(1).AddAsync(Arg.Any<WabaConfiguration>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_BasisPlan_SwitchingPhoneNumber_ReturnsForbidden()
    {
        var (sut, repo, subs) = BuildSut();
        var existing = WabaConfiguration.Create(TenantId, "waba_1", "phone_existing", "+1 555 0001");
        repo.GetByTenantIdAsync(TenantId, Arg.Any<CancellationToken>()).Returns(existing);

        var subscription = Subscription.Create(TenantId); // defaults to Basis
        subs.GetByTenantIdUnfilteredAsync(TenantId, Arg.Any<CancellationToken>()).Returns(subscription);

        var result = await sut.Handle(
            new LinkWabaAccountCommand(TenantId, "waba_1", "phone_new", "+1 555 0002"),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(System.Net.HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Handle_PremiumPlan_SwitchingPhoneNumber_Allowed()
    {
        var (sut, repo, subs) = BuildSut();
        var existing = WabaConfiguration.Create(TenantId, "waba_1", "phone_existing", "+1 555 0001");
        repo.GetByTenantIdAsync(TenantId, Arg.Any<CancellationToken>()).Returns(existing);

        var subscription = Subscription.Create(TenantId);
        subscription.SetPaystackBillingState(
            paystackAuthorizationCode: null,
            plan: SubscriptionPlan.Premium,
            currentPriceAmount: 100m,
            currentPriceCurrency: "USD",
            currentPeriodEnd: DateTimeOffset.UtcNow.AddMonths(1),
            paymentMethod: null
        );
        subs.GetByTenantIdUnfilteredAsync(TenantId, Arg.Any<CancellationToken>()).Returns(subscription);

        var result = await sut.Handle(
            new LinkWabaAccountCommand(TenantId, "waba_1", "phone_new", "+1 555 0002"),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_SamePhoneNumber_NoLimitCheck_Allowed()
    {
        var (sut, repo, subs) = BuildSut();
        var existing = WabaConfiguration.Create(TenantId, "waba_1", "phone_same", "+1 555 0001");
        repo.GetByTenantIdAsync(TenantId, Arg.Any<CancellationToken>()).Returns(existing);

        var result = await sut.Handle(
            new LinkWabaAccountCommand(TenantId, "waba_1", "phone_same", "+1 555 0001"),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue();
        // Subscription lookup not even invoked when the phone number hasn't changed.
        await subs.DidNotReceive().GetByTenantIdUnfilteredAsync(Arg.Any<TenantId>(), Arg.Any<CancellationToken>());
    }
}
