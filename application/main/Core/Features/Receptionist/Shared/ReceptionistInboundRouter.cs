using Main.Features.Receptionist.Commands;
using Main.Features.Receptionist.Domain;
using Main.Features.WhatsAppBooking.Commands;
using Main.Features.WhatsAppBooking.Domain;
using Main.Features.WhatsAppBooking.Shared;
using Main.Features.WhatsAppOnboarding.Domain;

namespace Main.Features.Receptionist.Shared;

/// <summary>
///     Routes inbound WhatsApp messages between the AI receptionist and the deterministic Flows engine
///     (spec R1). Free-text (and button/list) messages on receptionist-enabled tenants run an agent turn;
///     Flow completions keep their existing deterministic path — except the login Flow completion, which
///     the router finishes (client upsert) before letting the agent greet the now-identified customer.
///     When the receptionist is disabled the router declines and behavior is byte-for-byte the Flows
///     engine (the kill switch, spec §6.5.7).
/// </summary>
public sealed class ReceptionistInboundRouter(
    IReceptionistSettingsRepository receptionistSettingsRepository,
    IWhatsAppConversationRepository conversationRepository,
    IMediator mediator,
    ILogger<ReceptionistInboundRouter> logger
)
{
    /// <summary>Returns true when the message was handled by the receptionist; false hands it to the Flows engine.</summary>
    public async Task<bool> TryHandleInboundAsync(WhatsAppBusinessAccount account, WhatsAppInboundMessage inbound, CancellationToken cancellationToken)
    {
        var settings = await receptionistSettingsRepository.GetByTenantUnfilteredAsync(account.TenantId, cancellationToken);
        if (settings?.IsEnabled != true)
        {
            return false;
        }

        if (inbound.Kind == WhatsAppInboundKind.FlowCompletion)
        {
            return await TryHandleLoginFlowCompletionAsync(account, inbound, cancellationToken);
        }

        var messageText = inbound.Text;
        if (string.IsNullOrWhiteSpace(messageText))
        {
            // Unsupported message types (media, stickers) are acknowledged with a turn so the agent can respond.
            messageText = "[The customer sent a message type we cannot read, such as media. Ask them to type their request.]";
        }

        var result = await mediator.Send(new ProcessReceptionistTurnCommand(account.TenantId, inbound.FromPhoneNumber, messageText), cancellationToken);
        if (!result.IsSuccess)
        {
            logger.LogWarning("Receptionist turn was rejected: {Error}", result.GetErrorSummary());
        }

        return true;
    }

    /// <summary>
    ///     Completes the login Flow in receptionist mode: parses the submission, upserts the client (the
    ///     same identity rails the Flows engine uses), then runs a system-initiated agent turn so the
    ///     customer is greeted by name instead of being pushed into the booking Flow.
    /// </summary>
    private async Task<bool> TryHandleLoginFlowCompletionAsync(WhatsAppBusinessAccount account, WhatsAppInboundMessage inbound, CancellationToken cancellationToken)
    {
        var conversation = await conversationRepository.GetByTenantAndPhoneUnfilteredAsync(account.TenantId, inbound.FromPhoneNumber, cancellationToken);
        if (conversation?.State != WhatsAppConversationState.AwaitingLoginFlow)
        {
            // Booking (and any other) Flow completions stay with the deterministic engine — hybrid by design (R11).
            return false;
        }

        var loginResponse = WhatsAppLoginFlowResponse.TryParse(inbound.FlowResponseJson);
        if (loginResponse is null)
        {
            logger.LogWarning("Receptionist login Flow completion for conversation {ConversationId} could not be parsed.", conversation.Id.Value);
            return false;
        }

        await mediator.Send(new UpsertClientFromWhatsAppLoginCommand(account.TenantId, loginResponse.Name!, loginResponse.Email!, inbound.FromPhoneNumber), cancellationToken);

        var result = await mediator.Send(
            new ProcessReceptionistTurnCommand(account.TenantId, inbound.FromPhoneNumber, "[The customer just completed sign-in successfully. Greet them by name and continue helping with their original request.]"),
            cancellationToken
        );
        if (!result.IsSuccess)
        {
            logger.LogWarning("Receptionist post-login turn was rejected: {Error}", result.GetErrorSummary());
        }

        return true;
    }
}
