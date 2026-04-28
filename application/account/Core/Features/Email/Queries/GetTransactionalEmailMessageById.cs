using Account.Database;
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Cqrs;
using SharedKernel.Integrations.Email;

namespace Account.Features.Email.Queries;

[PublicAPI]
public sealed record GetTransactionalEmailMessageByIdQuery(Guid Id) : IRequest<Result<TransactionalEmailMessageResponse>>;

public sealed class GetTransactionalEmailMessageByIdHandler(AccountDbContext dbContext)
    : IRequestHandler<GetTransactionalEmailMessageByIdQuery, Result<TransactionalEmailMessageResponse>>
{
    public async Task<Result<TransactionalEmailMessageResponse>> Handle(GetTransactionalEmailMessageByIdQuery query, CancellationToken cancellationToken)
    {
        var message = await dbContext.Set<TransactionalEmailMessage>()
            .Where(e => e.Id == query.Id)
            .Select(e => new TransactionalEmailMessageResponse(
                e.Id,
                e.Recipient,
                e.Subject,
                e.TemplateKey,
                e.Status,
                e.Attempts,
                e.CreatedAt,
                e.NextAttemptAt,
                e.SentAt,
                e.DeadLetteredAt,
                e.LastError,
                e.CorrelationId
            ))
            .SingleOrDefaultAsync(cancellationToken);

        return message is null
            ? Result<TransactionalEmailMessageResponse>.NotFound($"Transactional email message '{query.Id}' was not found.")
            : message;
    }
}
