using System.Text.Json;
using Main.Features.WhatsAppBooking.Infrastructure;
using Microsoft.AspNetCore.Http.Features;
using SharedKernel.Endpoints;

namespace Main.Api.Endpoints;

public sealed class WhatsAppFlowEndpoints : IEndpoints
{
    private const string RoutePrefix = "/api/main/whatsapp/flows";

    public void MapEndpoints(IEndpointRouteBuilder routes)
    {
        // ── Public endpoints (called by Meta during Flow interactions) ────────────

        routes.MapGet($"{RoutePrefix}/public-key", (WhatsAppFlowCrypto crypto) =>
            Results.Text(crypto.PublicKeyPem, "text/plain")
        ).AllowAnonymous();

        routes.MapPost($"{RoutePrefix}/login/data",
            async (HttpRequest request, WhatsAppLoginFlowDataEndpoint handler, CancellationToken ct) =>
            {
                if (!TryParseEncryptedBody(await ReadBodyAsync(request, ct), out var aesKey, out var flowData, out var iv))
                {
                    return Results.BadRequest("Malformed Flow data request.");
                }

                var encrypted = await handler.HandleEncryptedAsync(aesKey, flowData, iv, ct);
                return Results.Text(encrypted, "text/plain");
            }
        ).AllowAnonymous().DisableAntiforgery();

        routes.MapPost($"{RoutePrefix}/booking/data",
            async (HttpRequest request, WhatsAppBookingFlowDataEndpoint handler, WhatsAppFlowCrypto crypto, CancellationToken ct) =>
            {
                if (!TryParseEncryptedBody(await ReadBodyAsync(request, ct), out var aesKey, out var flowData, out var iv))
                {
                    return Results.BadRequest("Malformed Flow data request.");
                }

                var encrypted = await handler.HandleEncryptedAsync(crypto, aesKey, flowData, iv, ct);
                return Results.Text(encrypted, "text/plain");
            }
        ).AllowAnonymous().DisableAntiforgery();

        // ── Authenticated endpoints (setup / diagnostics) ─────────────────────────

        var authGroup = routes.MapGroup(RoutePrefix).RequireAuthorization().WithTags("WhatsAppFlows");

        authGroup.MapGet("/login/definition", () =>
            Results.Text(WhatsAppLoginFlowDefinition.Build(), "application/json")
        );

        authGroup.MapGet("/booking/definition", () =>
            Results.Text(WhatsAppBookingFlowDefinition.Build(), "application/json")
        );
    }

    private static async Task<string> ReadBodyAsync(HttpRequest request, CancellationToken ct)
    {
        // Abuse posture: Flows data-exchange payloads are small encrypted envelopes; cap the anonymous
        // surface well below the Kestrel default so oversized bodies are rejected before reading.
        // (The feature is absent on the in-memory test server — hence the null-conditional.)
        var bodySizeFeature = request.HttpContext.Features.Get<IHttpMaxRequestBodySizeFeature>();
        if (bodySizeFeature is { IsReadOnly: false }) bodySizeFeature.MaxRequestBodySize = 1024 * 1024;

        using var reader = new StreamReader(request.Body);
        return await reader.ReadToEndAsync(ct);
    }

    private static bool TryParseEncryptedBody(string body, out string aesKey, out string flowData, out string iv)
    {
        aesKey = flowData = iv = string.Empty;
        if (string.IsNullOrWhiteSpace(body)) return false;
        try
        {
            using var doc = JsonDocument.Parse(body);
            aesKey = doc.RootElement.GetProperty("encrypted_aes_key").GetString() ?? string.Empty;
            flowData = doc.RootElement.GetProperty("encrypted_flow_data").GetString() ?? string.Empty;
            iv = doc.RootElement.GetProperty("initial_vector").GetString() ?? string.Empty;
            return !string.IsNullOrEmpty(aesKey) && !string.IsNullOrEmpty(flowData) && !string.IsNullOrEmpty(iv);
        }
        catch
        {
            return false;
        }
    }
}
