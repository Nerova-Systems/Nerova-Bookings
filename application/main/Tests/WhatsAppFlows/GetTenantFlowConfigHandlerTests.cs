using FluentAssertions;
using Main.Features.WhatsAppFlows.Domain;
using Main.Features.WhatsAppFlows.Infrastructure;
using Main.Features.WhatsAppFlows.Queries;
using NSubstitute;
using SharedKernel.Domain;
using SharedKernel.ExecutionContext;
using Xunit;

namespace Main.Tests.WhatsAppFlows;

/// <summary>
///     Unit tests for <see cref="GetTenantFlowConfigHandler" /> — specifically that the WABA phone
///     number propagates from the cross-SCS profile sync onto the read model in E.164 form.
/// </summary>
public sealed class GetTenantFlowConfigHandlerTests
{
    private static readonly TenantId TenantId = new(91);

    [Fact]
    public async Task Handle_WhenConfigExistsAndProfileHasDisplayPhone_NormalizesToE164()
    {
        var ctx = new Context();
        ctx.Repository.GetByTenantIdAsync(TenantId, Arg.Any<CancellationToken>())
            .Returns(TenantFlowConfig.Create(TenantId, BusinessVertical.Other));
        ctx.ProfileSync.GetByTenantId(TenantId, Arg.Any<CancellationToken>())
            .Returns(BuildProfile(displayPhone: "+27 81 123 4567"));

        var result = await ctx.Sut.Handle(new GetTenantFlowConfigQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.WabaPhoneNumber.Should().Be("+27811234567");
    }

    [Fact]
    public async Task Handle_WhenProfileMissing_WabaPhoneNumberIsNull()
    {
        var ctx = new Context();
        ctx.Repository.GetByTenantIdAsync(TenantId, Arg.Any<CancellationToken>())
            .Returns(TenantFlowConfig.Create(TenantId, BusinessVertical.Other));
        ctx.ProfileSync.GetByTenantId(TenantId, Arg.Any<CancellationToken>())
            .Returns((WhatsAppFlowProfile?)null);

        var result = await ctx.Sut.Handle(new GetTenantFlowConfigQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.WabaPhoneNumber.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WhenProfileDisplayPhoneAlreadyE164_RoundTrips()
    {
        var ctx = new Context();
        ctx.Repository.GetByTenantIdAsync(TenantId, Arg.Any<CancellationToken>())
            .Returns(TenantFlowConfig.Create(TenantId, BusinessVertical.Other));
        ctx.ProfileSync.GetByTenantId(TenantId, Arg.Any<CancellationToken>())
            .Returns(BuildProfile(displayPhone: "+27811234567"));

        var result = await ctx.Sut.Handle(new GetTenantFlowConfigQuery(), CancellationToken.None);

        result.Value!.WabaPhoneNumber.Should().Be("+27811234567");
    }

    [Fact]
    public async Task Handle_WhenDisplayPhoneEmpty_WabaPhoneNumberIsNull()
    {
        var ctx = new Context();
        ctx.Repository.GetByTenantIdAsync(TenantId, Arg.Any<CancellationToken>())
            .Returns(TenantFlowConfig.Create(TenantId, BusinessVertical.Other));
        ctx.ProfileSync.GetByTenantId(TenantId, Arg.Any<CancellationToken>())
            .Returns(BuildProfile(displayPhone: null));

        var result = await ctx.Sut.Handle(new GetTenantFlowConfigQuery(), CancellationToken.None);

        result.Value!.WabaPhoneNumber.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WhenConfigMissing_ReturnsNotFound()
    {
        var ctx = new Context();
        ctx.Repository.GetByTenantIdAsync(TenantId, Arg.Any<CancellationToken>()).Returns((TenantFlowConfig?)null);

        var result = await ctx.Sut.Handle(new GetTenantFlowConfigQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(System.Net.HttpStatusCode.NotFound);
    }

    private static WhatsAppFlowProfile BuildProfile(string? displayPhone)
    {
        return new WhatsAppFlowProfile(
            TenantId,
            "waba_abc",
            "phone_123",
            displayPhone,
            "flow_1",
            "Published",
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

        public GetTenantFlowConfigHandler Sut
        {
            get
            {
                var executionContext = Substitute.For<IExecutionContext>();
                executionContext.TenantId.Returns(TenantId);
                return new GetTenantFlowConfigHandler(Repository, ProfileSync, executionContext);
            }
        }
    }
}
