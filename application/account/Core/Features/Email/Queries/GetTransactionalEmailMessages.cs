using Account.Database;
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Cqrs;
using SharedKernel.Integrations.Email;

namespace Account.Features.Email.Queries;

[PublicAPI]
public sealed record GetTransactionalEmailMessagesQuery(TransactionalEmailStatus? Status = null, int PageOffset = 0, int PageSize = 50)
    : IRequest<Result<TransactionalEmailMessagesResponse>>;

public sealed class GetTransactionalEmailMessagesHandler(AccountDbContext dbContext)
    : IRequestHandler<GetTransactionalEmailMessagesQuery, Result<TransactionalEmailMessagesResponse>>
{
    public async Task<Result<TransactionalEmailMessagesResponse>> Handle(GetTransactionalEmailMessagesQuery query, CancellationToken cancellationToken)
    {
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var pageOffset = Math.Max(query.PageOffset, 0);
        var emailQuery = dbContext.Set<TransactionalEmailMessage>().AsQueryable();

        if (query.Status is not null)
        {
            emailQuery = emailQuery.Where(e => e.Status == query.Status);
        }

        var totalCount = await emailQuery.CountAsync(cancellationToken);
        var messages = await emailQuery
            .OrderByDescending(e => e.CreatedAt)
            .Skip(pageOffset)
            .Take(pageSize)
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
            .ToArrayAsync(cancellationToken);

        return new TransactionalEmailMessagesResponse(totalCount, messages);
    }
}
