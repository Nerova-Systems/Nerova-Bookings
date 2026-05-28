using System.Globalization;
using System.Text.Json.Nodes;
using Main.Features.Scheduling.Queries;
using Main.Features.WhatsAppFlows.Domain;
using SharedKernel.Cqrs;

namespace Main.Features.WhatsAppFlows.Endpoint;

/// <summary>
///     Slot selection screen. Calls <see cref="GetPublicSlotsQuery" /> with the date the user
///     selected on the previous screen and returns the list of available start times. On
///     data_exchange the chosen slot is captured and we advance to custom questions, the payment
///     notice, or straight to confirmation depending on tenant config.
/// </summary>
public sealed class SelectTimeScreenHandler(IMediator mediator) : IFlowScreenHandler
{
    public string ScreenId => FlowScreens.SelectTime;

    public async Task<FlowScreenResponse> Handle(FlowScreenRequest request, TenantFlowConfig config, CancellationToken cancellationToken)
    {
        if (string.Equals(request.Action, "INIT", StringComparison.OrdinalIgnoreCase)
            || string.Equals(request.Action, "data_exchange", StringComparison.OrdinalIgnoreCase) && IsLoadingSlots(request))
        {
            var slots = await LoadSlotsAsync(request, cancellationToken);
            var data = new JsonObject { ["slots"] = slots };
            return new FlowScreenResponse(FlowScreens.SelectTime, data);
        }

        var nextData = new JsonObject();
        if (request.Data.TryGetProperty("selected_time", out var slot))
        {
            nextData["selected_time"] = slot.GetString();
        }

        var next = ResolveNextScreen(config);
        return new FlowScreenResponse(next, nextData);
    }

    private static bool IsLoadingSlots(FlowScreenRequest request)
    {
        return !request.Data.TryGetProperty("selected_time", out _);
    }

    private async Task<JsonArray> LoadSlotsAsync(FlowScreenRequest request, CancellationToken cancellationToken)
    {
        // The earlier screens stash these into the flow data. If any are missing we return an
        // empty array — the upstream client will surface the issue rather than crashing here.
        var handle = TryGetString(request.Data, "handle");
        var eventSlug = TryGetString(request.Data, "event_slug");
        var dateText = TryGetString(request.Data, "selected_date");
        var timeZone = TryGetString(request.Data, "time_zone") ?? "UTC";

        var items = new JsonArray();
        if (string.IsNullOrWhiteSpace(handle) || string.IsNullOrWhiteSpace(eventSlug) || string.IsNullOrWhiteSpace(dateText))
        {
            return items;
        }

        if (!DateOnly.TryParse(dateText, CultureInfo.InvariantCulture, out var date))
        {
            return items;
        }

        var start = new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var end = start.AddDays(1);
        var query = new GetPublicSlotsQuery(handle, eventSlug, start, end, timeZone);
        var result = await mediator.Send(query, cancellationToken);
        if (!result.IsSuccess || result.Value is null) return items;

        foreach (var (_, slotsForDay) in result.Value.Slots)
        {
            foreach (var slot in slotsForDay)
            {
                items.Add(new JsonObject
                    {
                        ["start"] = slot.Time.ToString("O", CultureInfo.InvariantCulture),
                        ["end"] = slot.EndTime.ToString("O", CultureInfo.InvariantCulture)
                    }
                );
            }
        }

        return items;
    }

    private static string? TryGetString(System.Text.Json.JsonElement element, string property)
    {
        return element.ValueKind == System.Text.Json.JsonValueKind.Object
               && element.TryGetProperty(property, out var value)
               && value.ValueKind == System.Text.Json.JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static string ResolveNextScreen(TenantFlowConfig config)
    {
        if (config.CustomPreBookingQuestions.Count > 0) return FlowScreens.CustomQuestions;
        if (config.PaymentTiming == PaymentTiming.BeforeBooking || config.PaymentTiming == PaymentTiming.Deposit)
        {
            return FlowScreens.PaymentNotice;
        }

        return FlowScreens.ConfirmBooking;
    }
}
