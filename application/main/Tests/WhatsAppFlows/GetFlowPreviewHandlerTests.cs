using FluentAssertions;
using Main.Features.WhatsAppFlows.Domain;
using Main.Features.WhatsAppFlows.Infrastructure;
using Main.Features.WhatsAppFlows.Queries;
using Main.Features.WhatsAppFlows.Shared;
using NSubstitute;
using SharedKernel.Cqrs;
using SharedKernel.Domain;
using SharedKernel.ExecutionContext;
using Xunit;

namespace Main.Tests.WhatsAppFlows;

/// <summary>
///     Unit tests for <see cref="GetFlowPreviewHandler" />. No DB, no HTTP — all collaborators
///     mocked.
/// </summary>
public sealed class GetFlowPreviewHandlerTests
{
    private static readonly TenantId TenantId = new(77);

    [Fact]
    public async Task Handle_WhenTenantHasNoFlowConfig_ReturnsNotFound()
    {
        var ctx = new Context();
        ctx.Repository.GetByTenantIdAsync(TenantId, Arg.Any<CancellationToken>()).Returns((TenantFlowConfig?)null);

        var result = await ctx.Sut.Handle(new GetFlowPreviewQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(System.Net.HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Handle_WhenProfileMissing_ReturnsNotFound()
    {
        var ctx = new Context();
        ctx.Repository.GetByTenantIdAsync(TenantId, Arg.Any<CancellationToken>()).Returns(BuildConfig());
        ctx.ProfileSync.GetByTenantId(TenantId, Arg.Any<CancellationToken>()).Returns((WhatsAppFlowProfile?)null);

        var result = await ctx.Sut.Handle(new GetFlowPreviewQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(System.Net.HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Handle_WhenFlowIdMissing_ReturnsNotFound()
    {
        var ctx = new Context();
        ctx.Repository.GetByTenantIdAsync(TenantId, Arg.Any<CancellationToken>()).Returns(BuildConfig());
        ctx.ProfileSync.GetByTenantId(TenantId, Arg.Any<CancellationToken>())
            .Returns(BuildProfile(flowId: null, flowStatus: "Draft"));

        var result = await ctx.Sut.Handle(new GetFlowPreviewQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(System.Net.HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Handle_WhenFlowNotPublished_ReturnsNotFound()
    {
        var ctx = new Context();
        ctx.Repository.GetByTenantIdAsync(TenantId, Arg.Any<CancellationToken>()).Returns(BuildConfig());
        ctx.ProfileSync.GetByTenantId(TenantId, Arg.Any<CancellationToken>())
            .Returns(BuildProfile(flowId: "flow_1", flowStatus: "Draft"));

        var result = await ctx.Sut.Handle(new GetFlowPreviewQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(System.Net.HttpStatusCode.NotFound);
        await ctx.MetaClient.DidNotReceive().GetFlowPreviewUrlAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenPublishedAndMetaSucceeds_ReturnsPreviewUrl()
    {
        var ctx = new Context();
        var expiresAt = new DateTimeOffset(2030, 1, 1, 0, 0, 0, TimeSpan.Zero);
        ctx.Repository.GetByTenantIdAsync(TenantId, Arg.Any<CancellationToken>()).Returns(BuildConfig());
        ctx.ProfileSync.GetByTenantId(TenantId, Arg.Any<CancellationToken>())
            .Returns(BuildProfile(flowId: "flow_1", flowStatus: "Published"));
        ctx.MetaClient.GetFlowPreviewUrlAsync("flow_1", "TOKEN", Arg.Any<CancellationToken>())
            .Returns(new FlowPreviewResponse("https://business.facebook.com/wa/manage/flows/preview/abc", expiresAt));

        var result = await ctx.Sut.Handle(new GetFlowPreviewQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.PreviewUrl.Should().Be("https://business.facebook.com/wa/manage/flows/preview/abc");
        result.Value.ExpiresAt.Should().Be(expiresAt);
    }

    [Fact]
    public async Task Handle_WhenMetaFails_SurfacesFailure()
    {
        var ctx = new Context();
        ctx.Repository.GetByTenantIdAsync(TenantId, Arg.Any<CancellationToken>()).Returns(BuildConfig());
        ctx.ProfileSync.GetByTenantId(TenantId, Arg.Any<CancellationToken>())
            .Returns(BuildProfile(flowId: "flow_1", flowStatus: "Published"));
        ctx.MetaClient.GetFlowPreviewUrlAsync("flow_1", "TOKEN", Arg.Any<CancellationToken>())
            .Returns(Result<FlowPreviewResponse>.BadRequest("Meta GetFlowPreview failed: HTTP 500"));

        var result = await ctx.Sut.Handle(new GetFlowPreviewQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(System.Net.HttpStatusCode.BadRequest);
    }

    private static TenantFlowConfig BuildConfig()
    {
        return TenantFlowConfig.Create(TenantId, BusinessVertical.Other);
    }

    private static WhatsAppFlowProfile BuildProfile(string? flowId, string flowStatus)
    {
        return new WhatsAppFlowProfile(
            TenantId,
            "waba_abc",
            "phone_123",
            "+27 81 123 4567",
            flowId,
            flowStatus,
            "Complete",
            "TOKEN",
            null,
            null,
            null
        );
    }

    private sealed class Context
    {
        public ITenantFlowConfigRepository Repository { get; } = Substitute.For<ITenantFlowConfigRepository>();
        public IWhatsAppFlowProfileSync ProfileSync { get; } = Substitute.For<IWhatsAppFlowProfileSync>();
        public IMetaFlowsApiClient MetaClient { get; } = Substitute.For<IMetaFlowsApiClient>();

        public GetFlowPreviewHandler Sut
        {
            get
            {
                var executionContext = Substitute.For<IExecutionContext>();
                executionContext.TenantId.Returns(TenantId);
                return new GetFlowPreviewHandler(Repository, ProfileSync, MetaClient, executionContext);
            }
        }
    }
}
