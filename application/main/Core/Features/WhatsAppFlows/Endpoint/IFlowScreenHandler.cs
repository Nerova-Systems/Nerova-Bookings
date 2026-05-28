using System.Text.Json;
using System.Text.Json.Nodes;
using JetBrains.Annotations;
using Main.Features.WhatsAppFlows.Domain;
using SharedKernel.Domain;

namespace Main.Features.WhatsAppFlows.Endpoint;

/// <summary>
///     Per-screen contract resolved by <see cref="IFlowScreenHandler.ScreenId" />. The dispatcher
///     hands the decrypted Meta request straight to the matching handler and forwards its response
///     verbatim back to Meta, encrypted.
/// </summary>
[PublicAPI]
public sealed record FlowScreenRequest(string Action, JsonElement Data, string FlowToken, TenantId TenantId);

/// <summary>
///     A handler's reply. <c>NextScreen</c> is null for the terminal <c>SUCCESS</c> screen.
/// </summary>
[PublicAPI]
public sealed record FlowScreenResponse(string? NextScreen, JsonObject Data);

public interface IFlowScreenHandler
{
    string ScreenId { get; }

    Task<FlowScreenResponse> Handle(FlowScreenRequest request, TenantFlowConfig config, CancellationToken cancellationToken);
}
