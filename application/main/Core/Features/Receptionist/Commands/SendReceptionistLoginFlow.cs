using JetBrains.Annotations;
using Main.Features.WhatsAppBooking.Domain;
using Main.Features.WhatsAppBooking.Infrastructure;
using Main.Features.WhatsAppOnboarding.Domain;
using Microsoft.Extensions.Options;
using SharedKernel.Cqrs;
using SharedKernel.Domain;

namespace Main.Features.Receptionist.Commands;

/// <summary>
///     Sends the WhatsApp sign-in Flow to an unidentified customer (the receptionist's identity gate,
///     spec R3). Reuses the deterministic login Flow the Flows engine uses, so identity verification has
///     exactly one implementation regardless of which engine asked for it.
/// </summary>
[PublicAPI]
public sealed record SendReceptionistLoginFlowCommand(TenantId TenantId, WhatsAppConversationId WhatsAppConversationId)
    : ICommand, IRequest<Result>;

public sealed class SendReceptionistLoginFlowHandler(
    IWhatsAppConversationRepository conversationRepository,
    IWhatsAppBusinessAccountRepository businessAccountRepository,
    IWhatsAppOutboundSender outboundSender,
    IOptions<WhatsAppBookingOptions> options,
    TimeProvider timeProvider
) : IRequestHandler<SendReceptionistLoginFlowCommand, Result>
{
    public async Task<Result> Handle(SendReceptionistLoginFlowCommand command, CancellationToken cancellationToken)
    {
        var conversation = await conversationRepository.GetByIdAsync(command.WhatsAppConversationId, cancellationToken);
        if (conversation is null || conversation.TenantId != command.TenantId)
        {
            return Result.NotFound($"Conversation '{command.WhatsAppConversationId}' was not found.");
        }

        var account = await businessAccountRepository.GetByTenantIdUnfilteredAsync(command.TenantId, cancellationToken);
        if (account is null)
        {
            return Result.BadRequest("WhatsApp is not connected for this business.");
        }

        var loginFlowId = account.LoginFlowId ?? options.Value.LoginFlowId;
        if (string.IsNullOrWhiteSpace(loginFlowId))
        {
            return Result.BadRequest("The sign-in step is not configured yet.");
        }

        var now = timeProvider.GetUtcNow();
        var flowToken = $"login-{conversation.Id.Value}";
        var sent = await outboundSender.SendFlowAsync(
            account, conversation.CustomerPhoneNumber,
            "Please confirm your details to sign in or create an account.",
            loginFlowId, flowToken, "Sign in / Register",
            "DETAILS", new { phone = conversation.CustomerPhoneNumber, name = "", email = "", error_message = "" }, cancellationToken
        );

        if (!sent)
        {
            return Result.BadRequest("The sign-in message could not be sent. Please try again.");
        }

        conversation.BeginLoginFlow(flowToken, now);
        conversationRepository.Update(conversation);

        return Result.Success();
    }
}
