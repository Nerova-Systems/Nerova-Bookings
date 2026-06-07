namespace Main.Features.WhatsAppBooking.Shared;

/// <summary>
///     A normalized inbound WhatsApp message handed to the conversation engine. Decouples the engine from the
///     raw Meta webhook shape: plain text, an interactive button/list reply (with the tapped option's id), or a
///     Flow completion carrying the submitted response JSON.
/// </summary>
public sealed record WhatsAppInboundMessage(
    string FromPhoneNumber,
    WhatsAppInboundKind Kind,
    string? Text,
    string? InteractiveReplyId,
    string? FlowResponseJson
);

public enum WhatsAppInboundKind
{
    Text,
    ButtonReply,
    ListReply,
    FlowCompletion,
    Other
}
