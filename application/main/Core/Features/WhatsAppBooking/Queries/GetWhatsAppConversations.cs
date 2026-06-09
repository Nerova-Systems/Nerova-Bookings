using JetBrains.Annotations;
using Main.Features.WhatsAppBooking.Domain;
using Main.Features.WhatsAppMessaging.Domain;
using SharedKernel.Cqrs;

namespace Main.Features.WhatsAppBooking.Queries;

/// <summary>
///     Returns every WhatsApp booking conversation for the current tenant — the record/debug view of
///     chatbot &lt;-&gt; customer interactions. Each item carries the conversation's state, activity timestamps,
///     message counts, and the resulting booking id (when a booking was created), so an operator can
///     systematically inspect what happened in each conversation.
/// </summary>
[PublicAPI]
public sealed record GetWhatsAppConversationsQuery : IRequest<Result<GetWhatsAppConversationsResponse>>;

[PublicAPI]
public sealed record GetWhatsAppConversationsResponse(WhatsAppConversationItem[] Conversations);

[PublicAPI]
public sealed record WhatsAppConversationItem(
    string Id,
    string CustomerPhoneNumber,
    string State,
    string? BookingId,
    int InboundCount,
    int OutboundCount,
    DateTimeOffset LastActivityAt,
    DateTimeOffset ExpiresAt
);

public sealed class GetWhatsAppConversationsHandler(
    IWhatsAppConversationRepository conversationRepository,
    IWhatsAppMessageRepository messageRepository
) : IRequestHandler<GetWhatsAppConversationsQuery, Result<GetWhatsAppConversationsResponse>>
{
    public async Task<Result<GetWhatsAppConversationsResponse>> Handle(GetWhatsAppConversationsQuery query, CancellationToken cancellationToken)
    {
        var conversations = await conversationRepository.GetByTenantAsync(cancellationToken);
        if (conversations.Length == 0)
        {
            return new GetWhatsAppConversationsResponse([]);
        }

        var messages = await messageRepository.GetByTenantAsync(cancellationToken);

        // Group message counts by the customer phone number, which appears as From (inbound) or To (outbound).
        var inboundByPhone = messages
            .Where(message => message.Direction == MessageDirection.Inbound)
            .GroupBy(message => message.FromPhoneNumber)
            .ToDictionary(group => group.Key, group => group.Count());
        var outboundByPhone = messages
            .Where(message => message.Direction == MessageDirection.Outbound)
            .GroupBy(message => message.ToPhoneNumber)
            .ToDictionary(group => group.Key, group => group.Count());

        var items = conversations.Select(conversation => new WhatsAppConversationItem(
                conversation.Id.Value,
                conversation.CustomerPhoneNumber,
                conversation.State.ToString(),
                conversation.DraftBookingId?.Value,
                inboundByPhone.GetValueOrDefault(conversation.CustomerPhoneNumber),
                outboundByPhone.GetValueOrDefault(conversation.CustomerPhoneNumber),
                conversation.LastInboundAt,
                conversation.ExpiresAt
            )
        ).ToArray();

        return new GetWhatsAppConversationsResponse(items);
    }
}
