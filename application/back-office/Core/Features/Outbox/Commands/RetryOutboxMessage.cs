using BackOffice.Database;
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Cqrs;

namespace BackOffice.Features.Outbox.Commands;

[PublicAPI]
public sealed record RetryOutboxMessageCommand(Guid Id) : ICommand, IRequest<Result>;

public sealed class RetryOutboxMessageHandler(BackOfficeDbContext dbContext, TimeProvider timeProvider)
    : IRequestHandler<RetryOutboxMessageCommand, Result>
{
    public async Task<Result> Handle(RetryOutboxMessageCommand command, CancellationToken cancellationToken)
    {
        var message = await dbContext.OutboxMessages.AsTracking().SingleOrDefaultAsync(m => m.Id == command.Id, cancellationToken);
        if (message is null) return Result.NotFound($"Outbox message with id '{command.Id}' not found.");
        if (message.ProcessedAt is not null) return Result.BadRequest("Processed outbox messages cannot be retried.");

        message.Retry(timeProvider.GetUtcNow());
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
