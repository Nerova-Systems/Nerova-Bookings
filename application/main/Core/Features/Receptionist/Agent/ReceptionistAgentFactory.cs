using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace Main.Features.Receptionist.Agent;

/// <summary>
///     Builds the receptionist <see cref="AIAgent" /> per turn from tenant configuration: server-composed
///     persona instructions and the state-filtered tool catalog. Agents are cheap, stateless objects —
///     the durable state is the serialized session on <see cref="Domain.ReceptionistSession" />.
/// </summary>
public sealed class ReceptionistAgentFactory(IChatClient chatClient, ReceptionistToolCatalog toolCatalog)
{
    public AIAgent Create(ReceptionistTurnContext context, string serviceSummary, string clientDetailsSummary = "", string recordableFieldsSummary = "")
    {
        var instructions = PersonaComposer.Compose(context, serviceSummary, clientDetailsSummary, recordableFieldsSummary);
        var tools = toolCatalog.Build(context);

        return new ChatClientAgent(chatClient, instructions, "Receptionist", "Nerova AI front desk receptionist", tools);
    }
}
