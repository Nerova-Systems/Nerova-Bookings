using System.Text.Json.Nodes;
using Main.Database;
using Main.Features.EventTypes.Domain;
using Main.Features.WhatsAppFlows.Domain;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Domain;

namespace Main.Features.WhatsAppFlows.Endpoint;

/// <summary>
///     Staff selection screen. INIT lists hosts attached to the selected event type (or all
///     tenant users if the dropdown is unscoped). data_exchange stashes <c>selected_staff_id</c>
///     and advances to date selection.
/// </summary>
public sealed class SelectStaffScreenHandler(MainDbContext dbContext) : IFlowScreenHandler
{
    public string ScreenId => FlowScreens.SelectStaff;

    public async Task<FlowScreenResponse> Handle(FlowScreenRequest request, TenantFlowConfig config, CancellationToken cancellationToken)
    {
        if (string.Equals(request.Action, "INIT", StringComparison.OrdinalIgnoreCase))
        {
            // Default staff list = unique owners of event types for this tenant. Cheap, no SCS hop.
            var ownerIds = await dbContext.Set<EventType>()
                .Where(e => e.TenantId == request.TenantId)
                .Select(e => e.OwnerUserId)
                .Distinct()
                .ToListAsync(cancellationToken);

            var items = new JsonArray();
            foreach (var id in ownerIds)
            {
                items.Add(new JsonObject { ["id"] = id.Value.ToString(), ["name"] = $"Host {id.Value}" });
            }

            return new FlowScreenResponse(FlowScreens.SelectStaff, new JsonObject { ["staff"] = items });
        }

        var data = new JsonObject();
        if (request.Data.TryGetProperty("selected_staff_id", out var staff))
        {
            data["selected_staff_id"] = staff.GetString();
        }

        return new FlowScreenResponse(FlowScreens.SelectDate, data);
    }
}
