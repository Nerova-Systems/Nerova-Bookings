using Main.Features.Scheduling.Commands;
using Main.Features.Scheduling.Domain;
using Main.Features.WhatsAppBooking.Domain;
using Main.Features.WhatsAppBooking.Infrastructure;
using Main.Features.WhatsAppBooking.Shared;
using Main.Features.WhatsAppOnboarding.Domain;
using Microsoft.Extensions.Options;

namespace Main.Features.WhatsAppBooking;

/// <summary>
///     Deterministic conversation engine that drives the WhatsApp booking experience. For each inbound message
///     it loads (or starts) the customer's <see cref="WhatsAppConversation" />, advances the state machine, and
///     sends the next message. Booking details are captured inside a native WhatsApp Flow; the engine greets the
///     customer, launches that Flow, and turns the submitted Flow data into a booking via
///     <see cref="CreatePublicBookingCommand" />.
///     <para>
///         Runs inside the inbound webhook's processing (it is not itself a command). Booking creation is
///         dispatched through MediatR so it gets its own unit of work and its domain events fire (e.g. the
///         client upsert). Conversation/transcript changes are persisted by the caller's SaveChanges.
///     </para>
/// </summary>
public sealed class WhatsAppConversationEngine(
    IWhatsAppConversationRepository conversationRepository,
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

        // The customer submitted the booking Flow — turn the submitted details into a booking. Processed
        // regardless of tracked state: webhook + message dedup already guard against re-processing, and a
        // submitted Flow is an unambiguous booking intent even if the session state was lost or reset.
        if (inbound.Kind == WhatsAppInboundKind.FlowCompletion)
        {
            await CompleteBookingFromFlowAsync(account, conversation, inbound, now, cancellationToken);
            conversationRepository.Update(conversation);
            return;
        }

        // Returning customer after a completed/expired session, or a session that timed out: start fresh.
        if (conversation.State is WhatsAppConversationState.Confirmed or WhatsAppConversationState.Expired || conversation.HasExpired(now))
        {
            conversation.Restart(now);
        }

        // Idle (new/restarted) or the customer messaged again mid-Flow: (re)offer the booking entry.
        await SendBookingEntryAsync(account, conversation, inbound.FromPhoneNumber, now, cancellationToken);

        conversationRepository.Update(conversation);
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
                "WhatsApp Flow completion for conversation {ConversationId} could not be parsed into a bookable request.",
                conversation.Id.Value
            );
            await outboundSender.SendTextAsync(account, inbound.FromPhoneNumber, "Sorry, we couldn't read your booking details. Please send us a message to try again.", cancellationToken);
            conversation.Restart(now);
            return;
        }

        var profile = await schedulingProfileRepository.GetByTenantIdUnfilteredAsync(account.TenantId, cancellationToken);
        if (profile is null)
        {
            logger.LogWarning("No scheduling profile found for tenant {TenantId}; cannot create WhatsApp booking.", account.TenantId);
            await outboundSender.SendTextAsync(account, inbound.FromPhoneNumber, "Sorry, online booking isn't available right now. Please contact us directly.", cancellationToken);
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
                conversation.Id.Value,
                result.GetErrorSummary()
            );
            await outboundSender.SendTextAsync(account, inbound.FromPhoneNumber, "Sorry, we couldn't confirm that time — it may no longer be available. Please send us a message to try another slot.", cancellationToken);
            conversation.Restart(now);
            return;
        }

        conversation.CompleteWithBooking(result.Value.Id, now);

        var confirmation = BuildConfirmationMessage(result.Value.StartTime, flowResponse.TimeZone!);
        await outboundSender.SendTextAsync(account, inbound.FromPhoneNumber, confirmation, cancellationToken);
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
            // Fall back to the UTC instant when the submitted time zone id is unknown on this host.
        }

        return $"You're booked! See you on {localStart:dddd, d MMM} at {localStart:HH:mm}. We'll send a reminder before your appointment.";
    }

    private async Task SendBookingEntryAsync(
        WhatsAppBusinessAccount account,
        WhatsAppConversation conversation,
        string toPhoneNumber,
        DateTimeOffset now,
        CancellationToken cancellationToken
    )
    {
        var businessName = string.IsNullOrWhiteSpace(account.BusinessName) ? "our team" : account.BusinessName;

        if (!string.IsNullOrWhiteSpace(_options.FlowId))
        {
            var greeting = $"Hi! Thanks for messaging {businessName}. Tap below to book your appointment.";
            var flowToken = conversation.Id.Value;
            var sent = await outboundSender.SendFlowAsync(account, toPhoneNumber, greeting, _options.FlowId, flowToken, "Book appointment", null, null, cancellationToken);
            if (sent)
            {
                conversation.BeginFlow(flowToken, now);
                return;
            }

            logger.LogWarning("Failed to send the booking Flow to {Recipient} for tenant {TenantId}.", toPhoneNumber, account.TenantId);
            conversation.TouchInbound(now);
            return;
        }

        // No published Flow configured yet — send a plain greeting so the inbound/outbound loop still works.
        await outboundSender.SendTextAsync(
            account,
            toPhoneNumber,
            $"Hi! Thanks for messaging {businessName}. Our booking assistant is being set up — we'll be with you shortly.",
            cancellationToken
        );
        conversation.TouchInbound(now);
    }
}
