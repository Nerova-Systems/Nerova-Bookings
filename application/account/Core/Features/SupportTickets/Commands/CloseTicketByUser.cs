using Account.Features.SupportTickets.Domain;
using FluentValidation;
using JetBrains.Annotations;
using SharedKernel.Cqrs;
using SharedKernel.ExecutionContext;
using SharedKernel.Telemetry;

namespace Account.Features.SupportTickets.Commands;

[PublicAPI]
public sealed record CloseTicketByUserCommand : ICommand, IRequest<Result>
{
    [JsonIgnore] // Removes from API contract
    public SupportTicketId Id { get; init; } = null!;

    public SupportTicketCsatScore? CsatScore { get; init; }

    public string? CsatComment { get; init; }
}

public sealed class CloseTicketByUserValidator : AbstractValidator<CloseTicketByUserCommand>
{
    public CloseTicketByUserValidator()
    {
        RuleFor(x => x.CsatComment!).MaximumLength(SupportTicket.CsatCommentMaxLength)
            .WithMessage($"CSAT comment must be at most {SupportTicket.CsatCommentMaxLength} characters.")
            .When(x => x.CsatComment is not null);
    }
}

public sealed class CloseTicketByUserHandler(
    ISupportTicketRepository ticketRepository,
    IExecutionContext executionContext,
    TimeProvider timeProvider,
    ITelemetryEventsCollector events
) : IRequestHandler<CloseTicketByUserCommand, Result>
{
    public async Task<Result> Handle(CloseTicketByUserCommand command, CancellationToken cancellationToken)
    {
        var ticket = await ticketRepository.GetByIdAsync(command.Id, cancellationToken);
        if (ticket is null) return Result.NotFound($"Support ticket with id '{command.Id}' not found.");

        if (ticket.ReporterId != executionContext.UserInfo.Id!) return Result.NotFound($"Support ticket with id '{command.Id}' not found.");

        var now = timeProvider.GetUtcNow();
        if (command.CsatScore is not null)
        {
            ticket.SubmitCsat(command.CsatScore.Value, command.CsatComment, now);
            events.CollectEvent(new SupportTicketCsatSubmitted(ticket.Id, command.CsatScore.Value));
        }
        else
        {
            if (!ticket.CloseByUser(now))
            {
                return Result.BadRequest("Ticket is already closed.");
            }
        }

        ticketRepository.Update(ticket);
        events.CollectEvent(new SupportTicketClosed(ticket.Id, SupportMessageAuthorKind.User));
        return Result.Success();
    }
}
