using JetBrains.Annotations;
using Main.Features.Receptionist.Domain;
using SharedKernel.Cqrs;
using SharedKernel.ExecutionContext;

namespace Main.Features.Receptionist.Queries;

[PublicAPI]
public sealed record EscalationResponse(
    EscalationId Id,
    string WhatsAppConversationId,
    string? ClientId,
    string Reason,
    string Summary,
    EscalationStatus Status,
    DateTimeOffset CreatedAt,
    string? ResolutionNote
);

[PublicAPI]
public sealed record GetEscalationsResponse(EscalationResponse[] Escalations, int OpenCount);

[PublicAPI]
public sealed record GetEscalationsQuery(bool OpenOnly = false) : IRequest<Result<GetEscalationsResponse>>;

public sealed class GetEscalationsHandler(IEscalationRepository escalationRepository, IExecutionContext executionContext)
    : IRequestHandler<GetEscalationsQuery, Result<GetEscalationsResponse>>
{
    public async Task<Result<GetEscalationsResponse>> Handle(GetEscalationsQuery query, CancellationToken cancellationToken)
    {
        if (executionContext.TenantId is null)
        {
            return Result<GetEscalationsResponse>.Unauthorized("Authentication is required.");
        }

        var escalations = await escalationRepository.GetByTenantAsync(query.OpenOnly, cancellationToken);

        var responses = escalations.Select(escalation => new EscalationResponse(
                escalation.Id,
                escalation.WhatsAppConversationId.Value,
                escalation.ClientId?.Value,
                escalation.Reason,
                escalation.Summary,
                escalation.Status,
                escalation.CreatedAt,
                escalation.ResolutionNote
            )
        ).ToArray();

        return Result<GetEscalationsResponse>.Success(new GetEscalationsResponse(responses, responses.Count(r => r.Status == EscalationStatus.Open)));
    }
}
