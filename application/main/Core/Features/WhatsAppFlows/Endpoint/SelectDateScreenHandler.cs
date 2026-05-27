using System.Text.Json.Nodes;
using Main.Features.WhatsAppFlows.Domain;

namespace Main.Features.WhatsAppFlows.Endpoint;

/// <summary>
///     Date selection screen. The date picker UI is rendered entirely client-side; this handler
///     just stashes the selected date and advances to slot selection.
/// </summary>
public sealed class SelectDateScreenHandler : IFlowScreenHandler
{
    public string ScreenId => FlowScreens.SelectDate;

    public Task<FlowScreenResponse> Handle(FlowScreenRequest request, TenantFlowConfig config, CancellationToken cancellationToken)
    {
        if (string.Equals(request.Action, "INIT", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(new FlowScreenResponse(FlowScreens.SelectDate, new JsonObject()));
        }

        var data = new JsonObject();
        if (request.Data.TryGetProperty("selected_date", out var date))
        {
            data["selected_date"] = date.GetString();
        }

        return Task.FromResult(new FlowScreenResponse(FlowScreens.SelectTime, data));
    }
}
