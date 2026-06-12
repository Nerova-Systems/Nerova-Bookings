using FluentValidation;
using JetBrains.Annotations;
using Main.Features.Clients.Domain;
using Main.Features.Receptionist.Domain;
using Main.Features.WhatsAppBooking.Domain;
using SharedKernel.Cqrs;
using SharedKernel.Domain;
using SharedKernel.Telemetry;

namespace Main.Features.Receptionist.Commands;

/// <summary>
///     Records that the AI receptionist handed a conversation to a human (spec R6). Creates the
///     <see cref="Escalation" /> inbox item. The caller owns the session state transition (the turn
///     handler moves its tracked session to Escalated) — this command never loads the session, so the
///     same row is not tracked twice within one unit of work. Anonymous webhook context: tenant identity
///     is supplied by the turn handler from server-side conversation state, never by the model.
/// </summary>
[PublicAPI]
public sealed record EscalateConversationCommand(TenantId TenantId, WhatsAppConversationId WhatsAppConversationId, ClientId? ClientId, string Reason, string Summary)
    : ICommand, IRequest<Result>;

public sealed class EscalateConversationValidator : AbstractValidator<EscalateConversationCommand>
{
    public EscalateConversationValidator()
    {
        RuleFor(command => command.Reason).NotEmpty().MaximumLength(500).WithMessage("Reason must be between 1 and 500 characters.");
        RuleFor(command => command.Summary).MaximumLength(2000).WithMessage("Summary must be at most 2000 characters.");
    }
}

public sealed class EscalateConversationHandler(IEscalationRepository escalationRepository, ITelemetryEventsCollector events)
    : IRequestHandler<EscalateConversationCommand, Result>
{
    public async Task<Result> Handle(EscalateConversationCommand command, CancellationToken cancellationToken)
    {
        var existingOpenEscalation = await escalationRepository.GetOpenByConversationUnfilteredAsync(command.WhatsAppConversationId, cancellationToken);
        if (existingOpenEscalation is not null)
        {
            return Result.Success();
        }

        var escalation = Escalation.Create(command.TenantId, command.WhatsAppConversationId, command.ClientId, command.Reason, command.Summary);
        await escalationRepository.AddAsync(escalation, cancellationToken);

        events.CollectEvent(new ReceptionistEscalated(escalation.Id, command.Reason));

        return Result.Success();
    }
}
