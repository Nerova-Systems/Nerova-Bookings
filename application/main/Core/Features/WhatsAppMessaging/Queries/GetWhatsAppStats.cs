using JetBrains.Annotations;
using Main.Features.WhatsAppMessaging.Domain;
using SharedKernel.Cqrs;

namespace Main.Features.WhatsAppMessaging.Queries;

[PublicAPI]
public sealed record GetWhatsAppStatsQuery : IRequest<Result<GetWhatsAppStatsResponse>>;

[PublicAPI]
public sealed record GetWhatsAppStatsResponse(
    int TotalMessages,
    int InboundCount,
    int OutboundCount,
    int DeliveredCount,
    int ReadCount,
    int FailedCount,
    int UniqueContacts,
    DateTimeOffset? LastActivityAt
);

public sealed class GetWhatsAppStatsHandler(IWhatsAppMessageRepository whatsAppMessageRepository)
    : IRequestHandler<GetWhatsAppStatsQuery, Result<GetWhatsAppStatsResponse>>
{
    public async Task<Result<GetWhatsAppStatsResponse>> Handle(GetWhatsAppStatsQuery query, CancellationToken cancellationToken)
    {
        _ = query;

        var messages = await whatsAppMessageRepository.GetByTenantAsync(cancellationToken);

        var inboundCount = messages.Count(m => m.Direction == MessageDirection.Inbound);
        var outboundCount = messages.Count(m => m.Direction == MessageDirection.Outbound);
        var deliveredCount = messages.Count(m => m.Status is WhatsAppMessageStatus.Delivered or WhatsAppMessageStatus.Read);
        var readCount = messages.Count(m => m.Status == WhatsAppMessageStatus.Read);
        var failedCount = messages.Count(m => m.Status == WhatsAppMessageStatus.Failed);

        var uniqueContacts = messages
            .Select(m => m.Direction == MessageDirection.Inbound ? m.FromPhoneNumber : m.ToPhoneNumber)
            .Distinct()
            .Count();

        var lastActivityAt = messages.Length == 0 ? (DateTimeOffset?)null : messages.Max(m => m.Timestamp);

        return new GetWhatsAppStatsResponse(
            messages.Length,
            inboundCount,
            outboundCount,
            deliveredCount,
            readCount,
            failedCount,
            uniqueContacts,
            lastActivityAt
        );
    }
}
