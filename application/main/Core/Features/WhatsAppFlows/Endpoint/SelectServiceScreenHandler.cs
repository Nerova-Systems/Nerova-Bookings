using System.Text.Json.Nodes;
using Main.Database;
using Main.Features.EventTypes.Domain;
using Main.Features.WhatsAppFlows.Domain;
using Microsoft.EntityFrameworkCore;

namespace Main.Features.WhatsAppFlows.Endpoint;

/// <summary>
///     Service selection screen. INIT lists the tenant's bookable event types (id + title); the
///     submit step stashes the choice and routes onward.
/// </summary>
public sealed class SelectServiceScreenHandler(MainDbContext dbContext) : IFlowScreenHandler
{
    public string ScreenId => FlowScreens.SelectService;

    public async Task<FlowScreenResponse> Handle(FlowScreenRequest request, TenantFlowConfig config, CancellationToken cancellationToken)
    {
        if (string.Equals(request.Action, "INIT", StringComparison.OrdinalIgnoreCase))
        {
            var services = await dbContext.Set<EventType>()
                .Where(e => e.TenantId == request.TenantId)
                .OrderBy(e => e.Title)
                .Select(e => new { id = e.Id.Value.ToString(), title = e.Title })
                .ToListAsync(cancellationToken);

            var items = new JsonArray();
            foreach (var service in services)
            {
                items.Add(new JsonObject { ["id"] = service.id, ["title"] = service.title });
            }

            return new FlowScreenResponse(FlowScreens.SelectService, new JsonObject { ["services"] = items });
        }

        var data = new JsonObject();
        if (request.Data.TryGetProperty("selected_service_id", out var selected))
        {
            data["selected_service_id"] = selected.GetString();
        }

        var next = config.StaffAssignment == StaffAssignment.SpecificStaff ? FlowScreens.SelectStaff : FlowScreens.SelectDate;
        return new FlowScreenResponse(next, data);
    }
}
