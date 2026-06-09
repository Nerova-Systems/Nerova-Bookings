using JetBrains.Annotations;
using Main.Features.WhatsAppMessaging.Domain;
using SharedKernel.Cqrs;

namespace Main.Features.WhatsAppMessaging.Queries;

[PublicAPI]
public sealed record GetWhatsAppWebhookEventsQuery : IRequest<Result<GetWhatsAppWebhookEventsResponse>>;

[PublicAPI]
public sealed record GetWhatsAppWebhookEventsResponse(WhatsAppWebhookEventItem[] Events);

[PublicAPI]
public sealed record WhatsAppWebhookEventItem(
    string Id,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ProcessedAt,
    string? Error
);

public sealed class GetWhatsAppWebhookEventsHandler(IWhatsAppEventRepository whatsAppEventRepository)
    : IRequestHandler<GetWhatsAppWebhookEventsQuery, Result<GetWhatsAppWebhookEventsResponse>>
{
    public async Task<Result<GetWhatsAppWebhookEventsResponse>> Handle(GetWhatsAppWebhookEventsQuery query, CancellationToken cancellationToken)
    {
        var events = await whatsAppEventRepository.GetRecentAsync(20, cancellationToken);

        var items = events.Select(e => new WhatsAppWebhookEventItem(
                e.Id.Value,
                e.Status.ToString(),
                e.CreatedAt,
                e.ProcessedAt,
                e.Error
            )
        ).ToArray();

        return new GetWhatsAppWebhookEventsResponse(items);
    }
}
