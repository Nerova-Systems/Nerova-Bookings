using System.Net;
using Account.Features.Users.Domain;
using Account.Features.WhatsApp.Commands;
using Account.Features.WhatsApp.Domain;
using Account.Features.WhatsApp.Infrastructure;
using FluentAssertions;
using NSubstitute;
using SharedKernel.Authentication;
using SharedKernel.Cqrs;
using SharedKernel.Domain;
using SharedKernel.ExecutionContext;
using SharedKernel.Telemetry;
using Xunit;
using Account.Features;

namespace Account.Tests.WhatsApp;

/// <summary>
///     Unit coverage for <see cref="RequestWabaDisplayNameChangeHandler" />. Asserts the
///     owner-only gate, the "not while pending" invariant, the cloud-API call-through, and the
///     telemetry emission on the happy path.
/// </summary>
public sealed class RequestWabaDisplayNameChangeHandlerTests
{
    private static readonly TenantId TenantId = new(8800);

    private sealed record Sut(
        RequestWabaDisplayNameChangeHandler Handler,
        IWabaConfigurationRepository Repo,
        IWhatsAppCloudApiClient Client,
        ITelemetryEventsCollector Events
    );

    private static Sut BuildSut(string role = nameof(UserRole.Owner))
    {
        var repo = Substitute.For<IWabaConfigurationRepository>();
        var client = Substitute.For<IWhatsAppCloudApiClient>();
        var executionContext = Substitute.For<IExecutionContext>();
        executionContext.UserInfo.Returns(new UserInfo { IsAuthenticated = true, Role = role });
        executionContext.TenantId.Returns(TenantId);
        var events = Substitute.For<ITelemetryEventsCollector>();
        var handler = new RequestWabaDisplayNameChangeHandler(repo, client, executionContext, TimeProvider.System, events);
        return new Sut(handler, repo, client, events);
    }

    private static WabaConfiguration LinkedConfig()
    {
        var config = WabaConfiguration.Create(TenantId, "waba_1", "phone_1", "+27 11 000 0000");
        config.SetWabaAccessToken("token_1");
        return config;
    }

    [Fact]
    public async Task Handle_NonOwner_ReturnsForbidden()
    {
        var sut = BuildSut(role: nameof(UserRole.Member));

        var result = await sut.Handler.Handle(new RequestWabaDisplayNameChangeCommand("Acme Studio"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Handle_NoConfig_ReturnsNotFound()
    {
        var sut = BuildSut();
        sut.Repo.GetByTenantIdAsync(TenantId, Arg.Any<CancellationToken>()).Returns((WabaConfiguration?)null);

        var result = await sut.Handler.Handle(new RequestWabaDisplayNameChangeCommand("Acme Studio"), CancellationToken.None);

        result.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Handle_ConfigWithoutToken_ReturnsBadRequest()
    {
        var sut = BuildSut();
        var config = WabaConfiguration.Create(TenantId, "waba_1", "phone_1", "+27 11 000 0000");
        // no WabaAccessToken set
        sut.Repo.GetByTenantIdAsync(TenantId, Arg.Any<CancellationToken>()).Returns(config);

        var result = await sut.Handler.Handle(new RequestWabaDisplayNameChangeCommand("Acme Studio"), CancellationToken.None);

        result.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Handle_AlreadyPending_ReturnsBadRequest()
    {
        var sut = BuildSut();
        var config = LinkedConfig();
        config.RequestDisplayNameChange("Acme Studio", DateTimeOffset.UtcNow);
        sut.Repo.GetByTenantIdAsync(TenantId, Arg.Any<CancellationToken>()).Returns(config);

        var result = await sut.Handler.Handle(new RequestWabaDisplayNameChangeCommand("Acme Studios"), CancellationToken.None);

        result.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await sut.Client.DidNotReceive().RequestDisplayNameChangeAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()
        );
    }

    [Fact]
    public async Task Handle_HappyPath_CallsCloudApiUpdatesAggregateAndEmitsTelemetry()
    {
        var sut = BuildSut();
        var config = LinkedConfig();
        sut.Repo.GetByTenantIdAsync(TenantId, Arg.Any<CancellationToken>()).Returns(config);
        sut.Client.RequestDisplayNameChangeAsync("phone_1", "token_1", "Acme Studio", Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        var result = await sut.Handler.Handle(new RequestWabaDisplayNameChangeCommand("Acme Studio"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        config.DisplayNameStatus.Should().Be(WabaDisplayNameStatus.PendingReview);
        config.RequestedDisplayName.Should().Be("Acme Studio");
        sut.Repo.Received(1).Update(config);
        sut.Events.Received(1).CollectEvent(Arg.Any<WabaDisplayNameChangeRequested>());
    }

    [Fact]
    public async Task Handle_CloudApiFailure_PropagatesAndDoesNotMutateAggregate()
    {
        var sut = BuildSut();
        var config = LinkedConfig();
        sut.Repo.GetByTenantIdAsync(TenantId, Arg.Any<CancellationToken>()).Returns(config);
        sut.Client.RequestDisplayNameChangeAsync("phone_1", "token_1", "Acme Studio", Arg.Any<CancellationToken>())
            .Returns(Result.BadRequest("Meta refused."));

        var result = await sut.Handler.Handle(new RequestWabaDisplayNameChangeCommand("Acme Studio"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        config.DisplayNameStatus.Should().Be(WabaDisplayNameStatus.None);
        sut.Repo.DidNotReceive().Update(Arg.Any<WabaConfiguration>());
    }
}
