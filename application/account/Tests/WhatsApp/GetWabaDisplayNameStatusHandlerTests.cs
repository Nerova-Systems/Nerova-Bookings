using Account.Features.WhatsApp.Domain;
using Account.Features.WhatsApp.Queries;
using FluentAssertions;
using NSubstitute;
using SharedKernel.Domain;
using Xunit;

namespace Account.Tests.WhatsApp;

/// <summary>
///     Unit coverage for <see cref="GetWabaDisplayNameStatusHandler" />. The handler is thin; the
///     two scenarios that matter are "tenant has no config" (null → 404) and "tenant has a config"
///     (returns the projection verbatim so the UI can render the pending banner).
/// </summary>
public sealed class GetWabaDisplayNameStatusHandlerTests
{
    private static readonly TenantId TenantId = new(9900);

    [Fact]
    public async Task Handle_NoConfig_ReturnsNull()
    {
        var repo = Substitute.For<IWabaConfigurationRepository>();
        repo.GetByTenantIdAsync(TenantId, Arg.Any<CancellationToken>()).Returns((WabaConfiguration?)null);
        var handler = new GetWabaDisplayNameStatusHandler(repo);

        var result = await handler.Handle(new GetWabaDisplayNameStatusQuery(TenantId), CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task Handle_PendingReview_ReturnsPopulatedProjection()
    {
        var repo = Substitute.For<IWabaConfigurationRepository>();
        var config = WabaConfiguration.Create(TenantId, "waba_1", "phone_1", "+27 11 000 0000");
        config.SetWabaAccessToken("token_1");
        var requestedAt = new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero);
        config.RequestDisplayNameChange("Acme Studio", requestedAt);
        repo.GetByTenantIdAsync(TenantId, Arg.Any<CancellationToken>()).Returns(config);
        var handler = new GetWabaDisplayNameStatusHandler(repo);

        var result = await handler.Handle(new GetWabaDisplayNameStatusQuery(TenantId), CancellationToken.None);

        result.Should().NotBeNull();
        result!.Status.Should().Be(WabaDisplayNameStatus.PendingReview);
        result.RequestedDisplayName.Should().Be("Acme Studio");
        result.RequestedAt.Should().Be(requestedAt);
        result.VerifiedName.Should().BeNull();
    }
}
