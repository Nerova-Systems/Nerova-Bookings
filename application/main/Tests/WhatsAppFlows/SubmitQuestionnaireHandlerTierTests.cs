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

/// <summary>
///     Unit tests for the Phase 6 tier gating in <see cref="SubmitQuestionnaireHandler" />.
///     No DB, no HTTP — repositories + tier service are mocked.
/// </summary>
public sealed class SubmitQuestionnaireHandlerTierTests
{
    private static readonly TenantId TenantId = new(101);

    private static (SubmitQuestionnaireHandler handler, ITenantFlowConfigRepository repo, ITierService tier) BuildSut()
    {
        var repo = Substitute.For<ITenantFlowConfigRepository>();
        var tier = Substitute.For<ITierService>();
        var ctx = Substitute.For<IExecutionContext>();
        ctx.TenantId.Returns(TenantId);
        return (new SubmitQuestionnaireHandler(repo, tier, ctx), repo, tier);
    }

    private static SubmitQuestionnaireCommand DefaultCommand(
        StaffAssignment staff = StaffAssignment.AutoAssign,
        PaymentTiming payment = PaymentTiming.AfterSession,
        bool multipleServices = false,
        string? confirmationTemplate = null)
    {
        return new SubmitQuestionnaireCommand(
            BusinessVertical.Other,
            staff,
            payment,
            DepositAmountCents: payment == PaymentTiming.Deposit ? 100 : null,
            BookingWindowDays: 30,
            DefaultSessionMinutes: 30,
            HasMultipleServices: multipleServices,
            AllowSameDayBookings: true,
            ConfirmationMessageTemplate: confirmationTemplate
                                         ?? SubmitQuestionnaireHandler.DefaultConfirmationMessageTemplate,
            CancellationContact: "Call us at 1234"
        );
    }

    [Fact]
    public async Task Handle_StarterTier_SpecificStaff_ReturnsForbidden()
    {
        var (sut, _, tier) = BuildSut();
        tier.GetTierAsync(TenantId, Arg.Any<CancellationToken>()).Returns(TenantTier.Starter);

        var result = await sut.Handle(DefaultCommand(staff: StaffAssignment.SpecificStaff), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(System.Net.HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Handle_StarterTier_BeforeBookingPayment_ReturnsForbidden()
    {
        var (sut, _, tier) = BuildSut();
        tier.GetTierAsync(TenantId, Arg.Any<CancellationToken>()).Returns(TenantTier.Starter);

        var result = await sut.Handle(DefaultCommand(payment: PaymentTiming.BeforeBooking), CancellationToken.None);

        result.StatusCode.Should().Be(System.Net.HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Handle_StarterTier_MultipleServices_ReturnsForbidden()
    {
        var (sut, _, tier) = BuildSut();
        tier.GetTierAsync(TenantId, Arg.Any<CancellationToken>()).Returns(TenantTier.Starter);

        var result = await sut.Handle(DefaultCommand(multipleServices: true), CancellationToken.None);

        result.StatusCode.Should().Be(System.Net.HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Handle_StarterTier_CustomConfirmationMessage_ReturnsForbidden()
    {
        var (sut, _, tier) = BuildSut();
        tier.GetTierAsync(TenantId, Arg.Any<CancellationToken>()).Returns(TenantTier.Starter);

        var result = await sut.Handle(
            DefaultCommand(confirmationTemplate: "Custom message {name}!"),
            CancellationToken.None
        );

        result.StatusCode.Should().Be(System.Net.HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Handle_EnterpriseTier_AllOptionsAllowed_Succeeds()
    {
        var (sut, repo, tier) = BuildSut();
        tier.GetTierAsync(TenantId, Arg.Any<CancellationToken>()).Returns(TenantTier.Enterprise);
        repo.GetByTenantIdAsync(TenantId, Arg.Any<CancellationToken>()).Returns((TenantFlowConfig?)null);

        var command = DefaultCommand(
            staff: StaffAssignment.SpecificStaff,
            payment: PaymentTiming.BeforeBooking,
            multipleServices: true,
            confirmationTemplate: "Custom: {name}!"
        );

        var result = await sut.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await repo.Received(1).AddAsync(Arg.Any<TenantFlowConfig>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ProfessionalTier_DefaultConfirmationTemplate_Succeeds()
    {
        var (sut, repo, tier) = BuildSut();
        tier.GetTierAsync(TenantId, Arg.Any<CancellationToken>()).Returns(TenantTier.Professional);
        repo.GetByTenantIdAsync(TenantId, Arg.Any<CancellationToken>()).Returns((TenantFlowConfig?)null);

        var result = await sut.Handle(DefaultCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }
}
