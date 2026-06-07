using Main.Features.Clients.Domain;
using Main.Features.Scheduling.Commands;
using Main.Features.Scheduling.Domain;
using Main.Features.WhatsAppBooking.Commands;
using Main.Features.WhatsAppBooking.Domain;
using Main.Features.WhatsAppBooking.Infrastructure;
using Main.Features.WhatsAppBooking.Shared;
using Main.Features.WhatsAppOnboarding.Domain;
using Main.Integrations.Meta;
using Microsoft.Extensions.Options;
using SharedKernel.Domain;

namespace Main.Features.WhatsAppBooking;

/// <summary>
///     Deterministic conversation engine that drives the WhatsApp booking experience. Forks on first contact:
///     known customers (matched to a Client by phone) go straight to booking; unknown customers go through an
///     in-Flow login/registration step first. All booking details are captured inside native WhatsApp Flows.
/// </summary>
public sealed class WhatsAppConversationEngine(
    IWhatsAppConversationRepository conversationRepository,
    IClientRepository clientRepository,
    ISchedulingProfileRepository schedulingProfileRepository,
    IWhatsAppOutboundSender outboundSender,
    IMediator mediator,
    IOptions<WhatsAppBookingOptions> options,
    TimeProvider timeProvider,
    ILogger<WhatsAppConversationEngine> logger
)
{
    private const string ButtonIdBook = "book";
    private const string ButtonIdLogin = "login";

    private readonly WhatsAppBookingOptions _options = options.Value;

    public async Task HandleInboundAsync(WhatsAppBusinessAccount account, WhatsAppInboundMessage inbound, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();

        var conversation = await conversationRepository.GetByTenantAndPhoneUnfilteredAsync(account.TenantId, inbound.FromPhoneNumber, cancellationToken);
        if (conversation is null)
        {
            conversation = WhatsAppConversation.Start(account.TenantId, inbound.FromPhoneNumber, now);
            await conversationRepository.AddAsync(conversation, cancellationToken);
        }

        // Flow completion: route to login or booking handler based on the in-flight state.
        if (inbound.Kind == WhatsAppInboundKind.FlowCompletion)
        {
            if (conversation.State == WhatsAppConversationState.AwaitingLoginFlow)
            {
                await CompleteLoginFromFlowAsync(account, conversation, inbound, now, cancellationToken);
            }
            else
            {
                await CompleteBookingFromFlowAsync(account, conversation, inbound, now, cancellationToken);
            }

            conversationRepository.Update(conversation);
            return;
        }

        // Button reply routing.
        if (inbound.Kind == WhatsAppInboundKind.ButtonReply)
        {
            if (inbound.InteractiveReplyId == ButtonIdBook && conversation.State == WhatsAppConversationState.Idle)
            {
                await SendBookingFlowAsync(account, conversation, inbound.FromPhoneNumber, now, cancellationToken);
                conversationRepository.Update(conversation);
                return;
            }

            if (inbound.InteractiveReplyId == ButtonIdLogin && conversation.State == WhatsAppConversationState.Idle)
            {
                await SendLoginFlowAsync(account, conversation, inbound.FromPhoneNumber, now, cancellationToken);
                conversationRepository.Update(conversation);
                return;
            }
        }

        // Returning customer after a completed/expired session, or a session that timed out: start fresh.
        if (conversation.State is WhatsAppConversationState.Confirmed or WhatsAppConversationState.Expired || conversation.HasExpired(now))
        {
            conversation.Restart(now);
        }

        // Idle: welcome the customer and offer the right next step based on whether they are identified.
        if (conversation.State == WhatsAppConversationState.Idle)
        {
            var isIdentified = conversation.IsIdentified || await IsKnownClientAsync(account.TenantId, inbound.FromPhoneNumber, cancellationToken);
            if (isIdentified && !conversation.IsIdentified)
            {
                conversation.MarkIdentified(now);
            }

            await SendWelcomeAsync(account, conversation, inbound.FromPhoneNumber, isIdentified, now, cancellationToken);
        }
        else
        {
            // Mid-flow re-message: remind them the form is waiting.
            conversation.TouchInbound(now);
        }

        conversationRepository.Update(conversation);
    }

    // ─── Login flow ──────────────────────────────────────────────────────────────

    private async Task SendLoginFlowAsync(
        WhatsAppBusinessAccount account,
        WhatsAppConversation conversation,
        string toPhoneNumber,
        DateTimeOffset now,
        CancellationToken cancellationToken
    )
    {
        if (!string.IsNullOrWhiteSpace(_options.LoginFlowId))
        {
            var flowToken = $"login-{conversation.Id.Value}";
            var sent = await outboundSender.SendFlowAsync(
                account, toPhoneNumber,
                "Please enter your details to sign in or create an account.",
                _options.LoginFlowId, flowToken, "Sign in / Register",
                null, null, cancellationToken
            );
            if (sent)
            {
                conversation.BeginLoginFlow(flowToken, now);
                return;
            }

            logger.LogWarning("Failed to send login Flow to {Recipient} for tenant {TenantId}.", toPhoneNumber, account.TenantId);
        }

        // Fallback: plain text asking for email (pre-Flow path).
        await outboundSender.SendTextAsync(account, toPhoneNumber,
            "To get started, please reply with your email address so we can look up your account.",
            cancellationToken);
        conversation.TouchInbound(now);
    }

    private async Task CompleteLoginFromFlowAsync(
        WhatsAppBusinessAccount account,
        WhatsAppConversation conversation,
        WhatsAppInboundMessage inbound,
        DateTimeOffset now,
        CancellationToken cancellationToken
    )
    {
        var loginResponse = WhatsAppLoginFlowResponse.TryParse(inbound.FlowResponseJson);
        if (loginResponse is null)
        {
            logger.LogWarning("Login Flow completion for conversation {ConversationId} could not be parsed.", conversation.Id.Value);
            await outboundSender.SendTextAsync(account, inbound.FromPhoneNumber,
                "Sorry, we couldn't read your details. Please try again.",
                cancellationToken);
            conversation.Restart(now);
            return;
        }

        // Upsert the client using the existing infrastructure.
        var upsertCommand = new UpsertClientFromWhatsAppLoginCommand(account.TenantId, loginResponse.Name!, loginResponse.Email!, inbound.FromPhoneNumber);
        await mediator.Send(upsertCommand, cancellationToken);

        conversation.MarkIdentified(now);

        var businessName = string.IsNullOrWhiteSpace(account.BusinessName) ? "us" : account.BusinessName;
        await outboundSender.SendButtonsAsync(
            account, inbound.FromPhoneNumber,
            $"Welcome, {loginResponse.Name}! You're all set. Tap below to book your appointment with {businessName}.",
            [new WhatsAppReplyButton(ButtonIdBook, "Book appointment")],
            cancellationToken
        );
    }

    // ─── Booking flow ─────────────────────────────────────────────────────────────

    private async Task SendBookingFlowAsync(
        WhatsAppBusinessAccount account,
        WhatsAppConversation conversation,
        string toPhoneNumber,
        DateTimeOffset now,
        CancellationToken cancellationToken
    )
    {
        if (!string.IsNullOrWhiteSpace(_options.FlowId))
        {
            var flowToken = conversation.Id.Value;
            var sent = await outboundSender.SendFlowAsync(
                account, toPhoneNumber,
                "Tap below to choose your service, date and time.",
                _options.FlowId, flowToken, "Book appointment",
                null, null, cancellationToken
            );
            if (sent)
            {
                conversation.BeginFlow(flowToken, now);
                return;
            }

            logger.LogWarning("Failed to send booking Flow to {Recipient} for tenant {TenantId}.", toPhoneNumber, account.TenantId);
            conversation.TouchInbound(now);
            return;
        }

        // No Flow configured yet.
        await outboundSender.SendTextAsync(account, toPhoneNumber,
            "Our booking assistant is being set up — we'll be with you shortly.",
            cancellationToken);
        conversation.TouchInbound(now);
    }

    private async Task CompleteBookingFromFlowAsync(
        WhatsAppBusinessAccount account,
        WhatsAppConversation conversation,
        WhatsAppInboundMessage inbound,
        DateTimeOffset now,
        CancellationToken cancellationToken
    )
    {
        var flowResponse = WhatsAppBookingFlowResponse.TryParse(inbound.FlowResponseJson);
        if (flowResponse is null)
        {
            logger.LogWarning(
                "WhatsApp booking Flow completion for conversation {ConversationId} could not be parsed.",
                conversation.Id.Value
            );
            await outboundSender.SendTextAsync(account, inbound.FromPhoneNumber,
                "Sorry, we couldn't read your booking details. Please send us a message to try again.",
                cancellationToken);
            conversation.Restart(now);
            return;
        }

        var profile = await schedulingProfileRepository.GetByTenantIdUnfilteredAsync(account.TenantId, cancellationToken);
        if (profile is null)
        {
            logger.LogWarning("No scheduling profile found for tenant {TenantId}; cannot create WhatsApp booking.", account.TenantId);
            await outboundSender.SendTextAsync(account, inbound.FromPhoneNumber,
                "Sorry, online booking isn't available right now. Please contact us directly.",
                cancellationToken);
            conversation.Restart(now);
            return;
        }

        var command = new CreatePublicBookingCommand(
            profile.Handle,
            flowResponse.EventSlug!,
            flowResponse.StartTime!.Value,
            flowResponse.DurationMinutes!.Value,
            flowResponse.TimeZone!,
            flowResponse.BookerName!,
            flowResponse.BookerEmail!,
            BookerPhone: inbound.FromPhoneNumber
        );

        var result = await mediator.Send(command, cancellationToken);
        if (!result.IsSuccess || result.Value is null)
        {
            logger.LogWarning(
                "WhatsApp booking creation failed for conversation {ConversationId}: {Error}",
                conversation.Id.Value, result.GetErrorSummary()
            );
            await outboundSender.SendTextAsync(account, inbound.FromPhoneNumber,
                "Sorry, we couldn't confirm that time — it may no longer be available. Please send us a message to try another slot.",
                cancellationToken);
            conversation.Restart(now);
            return;
        }

        conversation.CompleteWithBooking(result.Value.Id, now);

        var confirmation = BuildConfirmationMessage(result.Value.StartTime, flowResponse.TimeZone!);
        await outboundSender.SendTextAsync(account, inbound.FromPhoneNumber, confirmation, cancellationToken);
    }

    // ─── Welcome fork ─────────────────────────────────────────────────────────────

    private async Task SendWelcomeAsync(
        WhatsAppBusinessAccount account,
        WhatsAppConversation conversation,
        string toPhoneNumber,
        bool isIdentified,
        DateTimeOffset now,
        CancellationToken cancellationToken
    )
    {
        var businessName = string.IsNullOrWhiteSpace(account.BusinessName) ? "us" : account.BusinessName;

        if (isIdentified)
        {
            // Known customer — offer booking directly.
            await outboundSender.SendButtonsAsync(
                account, toPhoneNumber,
                $"Welcome back! Ready to book with {businessName}?",
                [new WhatsAppReplyButton(ButtonIdBook, "Book appointment")],
                cancellationToken
            );
        }
        else
        {
            // Unknown customer — welcome + offer login/register.
            await outboundSender.SendButtonsAsync(
                account, toPhoneNumber,
                $"Hi! Welcome to {businessName}. To get started, please sign in or create an account.",
                [new WhatsAppReplyButton(ButtonIdLogin, "Sign in / Register")],
                cancellationToken
            );
        }

        conversation.TouchInbound(now);
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────────

    private async Task<bool> IsKnownClientAsync(TenantId tenantId, string phoneNumber, CancellationToken cancellationToken)
    {
        var client = await clientRepository.GetByTenantAndContactUnfilteredAsync(tenantId, phoneNumber, null, cancellationToken);
        return client is not null;
    }

    private static string BuildConfirmationMessage(DateTimeOffset startUtc, string timeZoneId)
    {
        var localStart = startUtc;
        try
        {
            localStart = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(startUtc, timeZoneId);
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            // Fall back to UTC when the time zone id is unknown on this host.
        }

        return $"You're booked! See you on {localStart:dddd, d MMM} at {localStart:HH:mm}. We'll send a reminder before your appointment.";
    }
}
