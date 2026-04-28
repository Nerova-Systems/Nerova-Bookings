using Account.Database;
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Cqrs;
using SharedKernel.Integrations.Email;

namespace Account.Features.Email.Commands;

[PublicAPI]
public sealed record RetryTransactionalEmailMessageCommand(Guid Id) : ICommand, IRequest<Result>;

public sealed class RetryTransactionalEmailMessageHandler(AccountDbContext dbContext, TimeProvider timeProvider)
    : IRequestHandler<RetryTransactionalEmailMessageCommand, Result>
{
    public async Task<Result> Handle(RetryTransactionalEmailMessageCommand command, CancellationToken cancellationToken)
    {
        var message = await dbContext.Set<TransactionalEmailMessage>().SingleOrDefaultAsync(e => e.Id == command.Id, cancellationToken);
        if (message is null)
        {
            return Result.NotFound($"Transactional email message '{command.Id}' was not found.");
        }

        if (message.Status == TransactionalEmailStatus.Sent)
        {
            return Result.BadRequest("Sent transactional email messages cannot be retried.");
        }

        message.Retry(timeProvider.GetUtcNow());
        return Result.Success();
    }
}
