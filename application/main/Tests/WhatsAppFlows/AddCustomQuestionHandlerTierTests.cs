using FluentAssertions;
using Main.Features.WhatsAppFlows.Commands;
using Main.Features.WhatsAppFlows.Domain;
using Main.Features.WhatsAppFlows.Infrastructure;
using Main.Features.WhatsAppFlows.Shared;
using NSubstitute;
using SharedKernel.Domain;
using SharedKernel.ExecutionContext;
using Xunit;

namespace Main.Tests.WhatsAppFlows;

public sealed class AddCustomQuestionHandlerTierTests
{
    private static readonly TenantId TenantId = new(303);

    private static (AddCustomQuestionHandler handler, ITenantFlowConfigRepository repo, ITierService tier) BuildSut()
    {
        var repo = Substitute.For<ITenantFlowConfigRepository>();
        var tier = Substitute.For<ITierService>();
        var ctx = Substitute.For<IExecutionContext>();
        ctx.TenantId.Returns(TenantId);
        return (new AddCustomQuestionHandler(repo, tier, ctx), repo, tier);
    }

    [Fact]
    public async Task Handle_StarterTier_AlwaysRejects()
    {
        var (sut, repo, tier) = BuildSut();
        tier.GetTierAsync(TenantId, Arg.Any<CancellationToken>()).Returns(TenantTier.Starter);
        repo.GetByTenantIdAsync(TenantId, Arg.Any<CancellationToken>())
            .Returns(TenantFlowConfig.Create(TenantId, BusinessVertical.Other));

        var result = await sut.Handle(
            new AddCustomQuestionCommand("Q?", CustomQuestionType.Text, true, null),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_EnterpriseTier_Unlimited_AddsBeyondPreviousCap()
    {
        var (sut, repo, tier) = BuildSut();
        tier.GetTierAsync(TenantId, Arg.Any<CancellationToken>()).Returns(TenantTier.Enterprise);

        // Pre-populate with 50 questions — previously legacy MaxCustomQuestions would gate at int.MaxValue
        // (effectively unlimited too), but the new `-1` sentinel path must take the unbounded branch.
        var config = TenantFlowConfig.Create(TenantId, BusinessVertical.Other);
        for (var i = 0; i < 50; i++)
        {
            config.AddCustomQuestion($"Q{i}", CustomQuestionType.Text, false, null);
        }
        repo.GetByTenantIdAsync(TenantId, Arg.Any<CancellationToken>()).Returns(config);

        var result = await sut.Handle(
            new AddCustomQuestionCommand("Q51", CustomQuestionType.Text, false, null),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue();
    }
}
