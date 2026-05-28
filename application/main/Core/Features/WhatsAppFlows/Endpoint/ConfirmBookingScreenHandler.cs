using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Main.Features.Scheduling.Commands;
using Main.Features.WhatsAppFlows.Domain;
using SharedKernel.Cqrs;

namespace Main.Features.WhatsAppFlows.Endpoint;

/// <summary>
///     Final confirmation screen. Dispatches <see cref="CreatePublicBookingCommand" /> with the
///     fields stashed by the earlier screens. On success advances to <see cref="FlowScreens.Success" />;
///     on validation failure returns an error data object that the dispatcher will surface to Meta.
/// </summary>
public sealed class ConfirmBookingScreenHandler(IMediator mediator, IPublisher publisher) : IFlowScreenHandler
{
    public string ScreenId => FlowScreens.ConfirmBooking;

    public async Task<FlowScreenResponse> Handle(FlowScreenRequest request, TenantFlowConfig config, CancellationToken cancellationToken)
    {
        if (!string.Equals(request.Action, "data_exchange", StringComparison.OrdinalIgnoreCase))
        {
            return new FlowScreenResponse(FlowScreens.ConfirmBooking, new JsonObject());
        }

        var handle = TryGetString(request.Data, "handle");
        var eventSlug = TryGetString(request.Data, "event_slug");
        var bookerName = TryGetString(request.Data, "booker_name") ?? "WhatsApp Booker";
        var bookerEmail = TryGetString(request.Data, "booker_email") ?? $"whatsapp+{request.FlowToken}@nerova.local";
        var timeZone = TryGetString(request.Data, "time_zone") ?? "UTC";
        var startText = TryGetString(request.Data, "selected_time");
        var duration = config.DefaultSessionMinutes <= 0 ? 60 : config.DefaultSessionMinutes;

        if (string.IsNullOrWhiteSpace(handle) || string.IsNullOrWhiteSpace(eventSlug) || string.IsNullOrWhiteSpace(startText))
        {
            return Error("Missing booking details.");
        }

        if (!DateTimeOffset.TryParse(startText, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var start))
        {
            return Error("Invalid selected time.");
        }

        var responses = ExtractResponses(request.Data);

        var command = new CreatePublicBookingCommand(
            handle,
            eventSlug,
            start,
            duration,
            timeZone,
            bookerName,
            bookerEmail,
            responses
        );

        var result = await mediator.Send(command, cancellationToken);
        if (!result.IsSuccess || result.Value is null)
        {
            return Error(result.GetErrorSummary());
        }

        // Fan out to the post-flow messaging dispatcher so the booker gets a confirmation
        // summary and (when payment-timing = BeforeBooking) a Paystack payment link via WhatsApp.
        // The booker WaId is captured from the encrypted flow payload (Meta does not enrich the
        // request with the originating phone number out of the box — clients pass it through as a
        // hidden "wa_id" field on the confirm screen). Falls back to email if missing so the
        // dispatcher can decide whether it has enough information to dispatch.
        var bookerWaId = TryGetString(request.Data, "wa_id")
                         ?? TryGetString(request.Data, "phone_number")
                         ?? bookerEmail;
        await publisher.Publish(
            new BookingCreatedViaFlowEvent(result.Value.Id, config.TenantId, bookerWaId, bookerName),
            cancellationToken
        );

        return new FlowScreenResponse(FlowScreens.Success, new JsonObject
            {
                ["booking_id"] = result.Value.Id.Value.ToString(),
                ["start_time"] = result.Value.StartTime.ToString("O", CultureInfo.InvariantCulture),
                ["status"] = result.Value.Status
            }
        );
    }

    private static FlowScreenResponse Error(string message)
    {
        return new FlowScreenResponse(FlowScreens.ConfirmBooking, new JsonObject
            {
                ["acknowledged"] = true,
                ["error_message"] = message
            }
        );
    }

    private static string? TryGetString(JsonElement element, string property)
    {
        return element.ValueKind == JsonValueKind.Object
               && element.TryGetProperty(property, out var value)
               && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static Dictionary<string, string>? ExtractResponses(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object) return null;
        if (!element.TryGetProperty("custom_answers", out var answers)) return null;
        if (answers.ValueKind != JsonValueKind.Object) return null;

        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var property in answers.EnumerateObject())
        {
            if (property.Value.ValueKind == JsonValueKind.String)
            {
                result[property.Name] = property.Value.GetString() ?? string.Empty;
            }
        }

        return result;
    }
}
