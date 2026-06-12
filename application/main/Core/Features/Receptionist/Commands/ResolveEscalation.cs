using FluentValidation;
using JetBrains.Annotations;
using Main.Features.Receptionist.Domain;
using SharedKernel.Cqrs;
using SharedKernel.ExecutionContext;
using SharedKernel.Telemetry;

namespace Main.Features.Receptionist.Commands;

/// <summary>
///     Owner-side resolution of an escalation: marks it resolved (or dismissed) and returns the
///     conversation's receptionist session to Active so the agent responds again.
/// </summary>
[PublicAPI]
public sealed record ResolveEscalationCommand(bool Dismiss = false, string? ResolutionNote = null) : ICommand, IRequest<Result>
{
    [JsonIgnore] // Removes from API contract
    public EscalationId Id { get; init; } = null!;
}

public sealed class ResolveEscalationValidator : AbstractValidator<ResolveEscalationCommand>
{
    public ResolveEscalationValidator()
    {
        RuleFor(command => command.ResolutionNote).MaximumLength(2000).WithMessage("Resolution note must be at most 2000 characters.");
    }
}

public sealed class ResolveEscalationHandler(
    IEscalationRepository escalationRepository,
    IReceptionistSessionRepository receptionistSessionRepository,
    IExecutionContext executionContext,
    ITelemetryEventsCollector events
) : IRequestHandler<ResolveEscalationCommand, Result>
{
    public async Task<Result> Handle(ResolveEscalationCommand command, CancellationToken cancellationToken)
    {
        var userId = executionContext.UserInfo.Id;
        if (userId is null)
        {
            return Result.Unauthorized("Authentication is required.");
        }

        var escalation = await escalationRepository.GetByIdAsync(command.Id, cancellationToken);
        if (escalation is null)
        {
            return Result.NotFound($"Escalation '{command.Id}' was not found.");
        }

        if (escalation.Status != EscalationStatus.Open)
        {
            return Result.BadRequest("Escalation has already been resolved.");
        }

        if (command.Dismiss)
        {
            escalation.Dismiss(userId);
        }
        else
        {
            escalation.Resolve(userId, command.ResolutionNote);
        }

        escalationRepository.Update(escalation);

        var session = await receptionistSessionRepository.GetByConversationUnfilteredAsync(escalation.WhatsAppConversationId, cancellationToken);
        if (session?.State == ReceptionistSessionState.Escalated)
        {
            session.Resume();
            receptionistSessionRepository.Update(session);
        }

        events.CollectEvent(new EscalationResolved(escalation.Id, command.Dismiss));

        return Result.Success();
    }
}
