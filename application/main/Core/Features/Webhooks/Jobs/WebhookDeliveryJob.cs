using Main.Features.Webhooks.Domain;
using Main.Features.Webhooks.Infrastructure;
using SharedKernel.Persistence;
using TickerQ.Utilities.Base;
using TickerQ.Utilities.Interfaces;

namespace Main.Features.Webhooks.Jobs;

/// <summary>
///     Polls <see cref="WebhookDelivery" /> rows whose <c>NextAttemptAt</c> has passed and
///     dispatches each one. Runs every 60 seconds. The processor encapsulates HTTP and backoff
///     logic; this job is responsible for batching + committing the unit of work.
/// </summary>
public sealed class WebhookDeliveryJob(
    IWebhookDeliveryRepository deliveryRepository,
    WebhookDeliveryProcessor processor,
    TimeProvider timeProvider,
    IUnitOfWork unitOfWork
) : ITickerFunction
{
    /// <summary>
    ///     Upper bound on deliveries processed in a single tick. Sized so we comfortably finish the
    ///     batch within the 60-second cron interval, even at the 15-second request timeout.
    /// </summary>
    public const int BatchSize = 25;

    public async Task ExecuteAsync(TickerFunctionContext context, CancellationToken ct)
    {
        _ = context;
        var now = timeProvider.GetUtcNow();
        var due = await deliveryRepository.GetDueAsync(now, BatchSize, ct);
        if (due.Length == 0) return;

        foreach (var delivery in due)
        {
            if (ct.IsCancellationRequested) break;
            await processor.ProcessAsync(delivery, ct);
            deliveryRepository.Update(delivery);
        }

        await unitOfWork.CommitAsync(ct);
    }
}
