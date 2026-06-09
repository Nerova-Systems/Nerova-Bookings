using JetBrains.Annotations;
using Main.Features.WhatsAppMessaging.Domain;
using SharedKernel.Cqrs;

namespace Main.Features.WhatsAppMessaging.Queries;

[PublicAPI]
public sealed record GetWhatsAppMessagesQuery : IRequest<Result<GetWhatsAppMessagesResponse>>;

[PublicAPI]
public sealed record GetWhatsAppMessagesResponse(WhatsAppMessageItem[] Messages);

[PublicAPI]
public sealed record WhatsAppMessageItem(
    string Id,
    string Direction,
    string From,
    string To,
    string Text,
    string Status,
    DateTimeOffset Timestamp
);

public sealed class GetWhatsAppMessagesHandler(IWhatsAppMessageRepository whatsAppMessageRepository)
    : IRequestHandler<GetWhatsAppMessagesQuery, Result<GetWhatsAppMessagesResponse>>
{
    public async Task<Result<GetWhatsAppMessagesResponse>> Handle(GetWhatsAppMessagesQuery query, CancellationToken cancellationToken)
    {
        var messages = await whatsAppMessageRepository.GetByTenantAsync(cancellationToken);

        var items = messages.Select(m => new WhatsAppMessageItem(
                m.Id.Value,
                m.Direction.ToString(),
                m.FromPhoneNumber,
                m.ToPhoneNumber,
                m.Text,
                m.Status.ToString(),
                m.Timestamp
            )
        ).ToArray();

        return new GetWhatsAppMessagesResponse(items);
    }
}
