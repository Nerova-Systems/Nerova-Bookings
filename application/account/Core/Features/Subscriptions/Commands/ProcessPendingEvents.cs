using Account.Features.Subscriptions.Domain;
using Account.Features.Subscriptions.Shared;
using JetBrains.Annotations;
using SharedKernel.Cqrs;

namespace Account.Features.Subscriptions.Commands;

[PublicAPI]
public sealed record ProcessPendingEventsCommand : ICommand, IRequest<Result>;

public sealed class ProcessPendingEventsHandler(ISubscriptionRepository subscriptionRepository, ProcessPendingPaystackEvents processPendingPaystackEvents)
    : IRequestHandler<ProcessPendingEventsCommand, Result>
{
    public async Task<Result> Handle(ProcessPendingEventsCommand command, CancellationToken cancellationToken)
    {
        var subscription = await subscriptionRepository.GetCurrentAsync(cancellationToken);

        if (subscription.PaystackCustomerId is null)
        {
            return Result.BadRequest("No Paystack customer found for this subscription.");
        }

        await processPendingPaystackEvents.ExecuteAsync(subscription.PaystackCustomerId, cancellationToken);

        return Result.Success();
    }
}
