using FluentValidation;
using JetBrains.Annotations;
using Main.Features.WhatsAppFlows.Domain;
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
    IExecutionContext executionContext
) : IRequestHandler<SubmitQuestionnaireCommand, Result<TenantFlowConfigResponse>>
{
    public async Task<Result<TenantFlowConfigResponse>> Handle(SubmitQuestionnaireCommand command, CancellationToken cancellationToken)
    {
        var tenantId = executionContext.TenantId;
        if (tenantId is null) return Result<TenantFlowConfigResponse>.Unauthorized("Authentication is required.");

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
