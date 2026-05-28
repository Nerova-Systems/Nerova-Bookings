using FluentValidation;
using JetBrains.Annotations;
using Main.Features.WhatsAppFlows.Domain;
using Main.Features.WhatsAppFlows.Infrastructure;
using Main.Features.WhatsAppFlows.Shared;
using SharedKernel.Cqrs;
using SharedKernel.ExecutionContext;

namespace Main.Features.WhatsAppFlows.Commands;

/// <summary>
///     Persists the questionnaire output. Creates a new <see cref="TenantFlowConfig" /> for the
///     current tenant if none exists; otherwise updates the existing one. Idempotent on
///     repeated submission.
/// </summary>
[PublicAPI]
public sealed record SubmitQuestionnaireCommand(
    BusinessVertical BusinessVertical,
    StaffAssignment StaffAssignment,
    PaymentTiming PaymentTiming,
    long? DepositAmountCents,
    int BookingWindowDays,
    int DefaultSessionMinutes,
    bool HasMultipleServices,
    bool AllowSameDayBookings,
    string ConfirmationMessageTemplate,
    string CancellationContact
) : ICommand, IRequest<Result<TenantFlowConfigResponse>>;

public sealed class SubmitQuestionnaireValidator : AbstractValidator<SubmitQuestionnaireCommand>
{
    public SubmitQuestionnaireValidator()
    {
        RuleFor(c => c.BookingWindowDays).InclusiveBetween(1, 365);
        RuleFor(c => c.DefaultSessionMinutes).InclusiveBetween(5, 480);
        RuleFor(c => c.ConfirmationMessageTemplate).NotEmpty().MaximumLength(1000);
        RuleFor(c => c.CancellationContact).MaximumLength(500);
        RuleFor(c => c.DepositAmountCents)
            .NotNull().When(c => c.PaymentTiming == PaymentTiming.Deposit)
            .WithMessage("DepositAmountCents is required when PaymentTiming is Deposit.");
    }
}

public sealed class SubmitQuestionnaireHandler(
    ITenantFlowConfigRepository repository,
    ITierService tierService,
    IExecutionContext executionContext
) : IRequestHandler<SubmitQuestionnaireCommand, Result<TenantFlowConfigResponse>>
{
    /// <summary>
    ///     Default confirmation template applied by <c>TenantFlowConfig.ApplyVerticalDefaults</c>.
    ///     Tenants on a tier without <see cref="TierLimits.CustomConfirmationMessage" /> may only
    ///     submit this exact value.
    /// </summary>
    public const string DefaultConfirmationMessageTemplate =
        "Hi {name}, your booking for {service} on {time} with {staff} is confirmed.";

    public async Task<Result<TenantFlowConfigResponse>> Handle(SubmitQuestionnaireCommand command, CancellationToken cancellationToken)
    {
        var tenantId = executionContext.TenantId;
        if (tenantId is null) return Result<TenantFlowConfigResponse>.Unauthorized("Authentication is required.");

        // ─── Tier gating ──────────────────────────────────────────────────
        // We evaluate per-tier limits before persisting so the questionnaire submission is rejected
        // atomically with a clear 403. Bool-valued capabilities are enforced as
        // "your tier doesn't allow X, switch to Y" rather than silently downgrading the input.
        var tier = await tierService.GetTierAsync(tenantId, cancellationToken);

        if (command.StaffAssignment != StaffAssignment.AutoAssign && !TierLimits.StaffSelectionInFlow(tier))
        {
            return Result<TenantFlowConfigResponse>.Forbidden("Staff selection in the booking flow is not available on your current plan.");
        }

        if (command.PaymentTiming != PaymentTiming.AfterSession && TierLimits.PaymentTimingChoice(tier) == PaymentTimingChoice.AfterOnly)
        {
            return Result<TenantFlowConfigResponse>.Forbidden("Pre-booking and deposit payments are not available on your current plan.");
        }

        if (command.HasMultipleServices && !TierLimits.MultipleServicesInFlow(tier))
        {
            return Result<TenantFlowConfigResponse>.Forbidden("Multiple services in the booking flow are not available on your current plan.");
        }

        if (!string.Equals(command.ConfirmationMessageTemplate, DefaultConfirmationMessageTemplate, StringComparison.Ordinal)
            && !TierLimits.CustomConfirmationMessage(tier))
        {
            return Result<TenantFlowConfigResponse>.Forbidden("Custom confirmation messages are not available on your current plan.");
        }

        var existing = await repository.GetByTenantIdAsync(tenantId, cancellationToken);
        if (existing is null)
        {
            var config = TenantFlowConfig.Create(tenantId, command.BusinessVertical);
            config.UpdateBusinessProfile(
                command.BusinessVertical,
                command.StaffAssignment,
                command.PaymentTiming,
                command.DepositAmountCents,
                command.BookingWindowDays,
                command.DefaultSessionMinutes,
                command.HasMultipleServices,
                command.AllowSameDayBookings,
                command.ConfirmationMessageTemplate,
                command.CancellationContact
            );
            await repository.AddAsync(config, cancellationToken);
            return TenantFlowConfigResponse.From(config);
        }

        existing.UpdateBusinessProfile(
            command.BusinessVertical,
            command.StaffAssignment,
            command.PaymentTiming,
            command.DepositAmountCents,
            command.BookingWindowDays,
            command.DefaultSessionMinutes,
            command.HasMultipleServices,
            command.AllowSameDayBookings,
            command.ConfirmationMessageTemplate,
            command.CancellationContact
        );
        repository.Update(existing);
        return TenantFlowConfigResponse.From(existing);
    }
}
