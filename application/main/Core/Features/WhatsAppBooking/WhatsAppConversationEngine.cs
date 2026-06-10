using Main.Features.Clients.Domain;
using Main.Features.Scheduling.Commands;
using Main.Features.Scheduling.Domain;
using Main.Features.WhatsAppBooking.Commands;
using Main.Features.WhatsAppBooking.Domain;
using Main.Features.WhatsAppBooking.Infrastructure;
using Main.Features.WhatsAppBooking.Shared;
using Main.Features.WhatsAppOnboarding.Domain;
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

        // Returning customer after a completed/expired session, or a session that timed out: start fresh.
        if (conversation.State is WhatsAppConversationState.Confirmed or WhatsAppConversationState.Expired || conversation.HasExpired(now))
        {
            conversation.Restart(now);
        }

        // Idle: immediately launch the right flow based on whether the customer is identified.
        if (conversation.State == WhatsAppConversationState.Idle)
        {
            var isIdentified = conversation.IsIdentified || await IsKnownClientAsync(account.TenantId, inbound.FromPhoneNumber, cancellationToken);
            if (isIdentified && !conversation.IsIdentified)
            {
                conversation.MarkIdentified(now);
            }

            if (isIdentified)
            {
                await SendBookingFlowAsync(account, conversation, inbound.FromPhoneNumber, now, cancellationToken);
            }
            else
            {
                await SendLoginFlowAsync(account, conversation, inbound.FromPhoneNumber, now, cancellationToken);
            }
        }
        else
        {
            // Mid-flow re-message: the customer sent a text while a flow is open — just ignore.
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
        var loginFlowId = account.LoginFlowId ?? _options.LoginFlowId;
        if (!string.IsNullOrWhiteSpace(loginFlowId))
        {
            var flowToken = $"login-{conversation.Id.Value}";
            var sent = await outboundSender.SendFlowAsync(
                account, toPhoneNumber,
                "Please confirm your details to sign in or create an account.",
                loginFlowId, flowToken, "Sign in / Register",
                "DETAILS", new { phone = toPhoneNumber, name = "", email = "", error_message = "" }, cancellationToken
            );
            if (sent)
            {
                conversation.BeginLoginFlow(flowToken, now);
                return;
            }

            logger.LogWarning("Failed to send login Flow to {Recipient} for tenant {TenantId}.", toPhoneNumber, account.TenantId);
        }

        // No Flow configured yet. Never ask for an email over plain text — email is collected only inside
        // the Flow — so send a neutral holding message instead.
        await outboundSender.SendTextAsync(account, toPhoneNumber,
            "We're getting your sign-in ready — we'll be with you shortly.",
            cancellationToken
        );
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
                cancellationToken
            );
            conversation.Restart(now);
            return;
        }

        // Upsert the client using the existing infrastructure.
        var upsertCommand = new UpsertClientFromWhatsAppLoginCommand(account.TenantId, loginResponse.Name!, loginResponse.Email!, inbound.FromPhoneNumber);
        await mediator.Send(upsertCommand, cancellationToken);

        conversation.MarkIdentified(now);

        // Identity confirmed — immediately open the booking flow without an extra step.
        await SendBookingFlowAsync(account, conversation, inbound.FromPhoneNumber, now, cancellationToken);
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
        var bookingFlowId = account.BookingFlowId ?? _options.FlowId;
        if (!string.IsNullOrWhiteSpace(bookingFlowId))
        {
            var flowToken = conversation.Id.Value;
            var sent = await outboundSender.SendFlowAsync(
                account, toPhoneNumber,
                "Tap below to choose your service, date and time.",
                bookingFlowId, flowToken, "Book appointment",
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
            cancellationToken
        );
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
                cancellationToken
            );
            conversation.Restart(now);
            return;
        }

        var profile = await schedulingProfileRepository.GetByTenantIdUnfilteredAsync(account.TenantId, cancellationToken);
        if (profile is null)
        {
            logger.LogWarning("No scheduling profile found for tenant {TenantId}; cannot create WhatsApp booking.", account.TenantId);
            await outboundSender.SendTextAsync(account, inbound.FromPhoneNumber,
                "Sorry, online booking isn't available right now. Please contact us directly.",
                cancellationToken
            );
            conversation.Restart(now);
            return;
        }

        var command = new CreatePublicBookingCommand(
            profile.Handle,
            flowResponse.EventSlug!,
            flowResponse.StartTime!.Value,
            flowResponse.DurationMinutes!.Value,
            flowResponse.TimeZone!,
            await GetClientNameAsync(account.TenantId, inbound.FromPhoneNumber, cancellationToken),
            await GetClientEmailAsync(account.TenantId, inbound.FromPhoneNumber, cancellationToken),
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
                cancellationToken
            );
            conversation.Restart(now);
            return;
        }

        conversation.CompleteWithBooking(result.Value.Id, now);

        var confirmation = BuildConfirmationMessage(result.Value.StartTime, flowResponse.TimeZone!);
        await outboundSender.SendTextAsync(account, inbound.FromPhoneNumber, confirmation, cancellationToken);
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────────

    private async Task<bool> IsKnownClientAsync(TenantId tenantId, string phoneNumber, CancellationToken cancellationToken)
    {
        var client = await clientRepository.GetByTenantAndContactUnfilteredAsync(tenantId, phoneNumber, null, cancellationToken);
        return client is not null;
    }

    private async Task<string> GetClientNameAsync(TenantId tenantId, string phoneNumber, CancellationToken cancellationToken)
    {
        var client = await clientRepository.GetByTenantAndContactUnfilteredAsync(tenantId, phoneNumber, null, cancellationToken);
        if (client is null) return "Unknown";
        return $"{client.FirstName} {client.LastName}".Trim();
    }

    private async Task<string> GetClientEmailAsync(TenantId tenantId, string phoneNumber, CancellationToken cancellationToken)
    {
        var client = await clientRepository.GetByTenantAndContactUnfilteredAsync(tenantId, phoneNumber, null, cancellationToken);
        return client?.Email ?? string.Empty;
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
