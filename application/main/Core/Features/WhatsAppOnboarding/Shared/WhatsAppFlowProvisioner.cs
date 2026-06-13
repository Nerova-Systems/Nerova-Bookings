using Main.Features.WhatsAppBooking.Infrastructure;
using Main.Features.WhatsAppOnboarding.Domain;
using Main.Integrations.Meta;

namespace Main.Features.WhatsAppOnboarding.Shared;

/// <summary>
///     Provisions the tenant's WhatsApp Flows on a freshly connected WABA: uploads the platform RSA
///     public key and creates + publishes the Login and Booking Flows with their data-exchange endpoint
///     URIs. Shared by Embedded Signup and the manual developer link so flow provisioning has exactly one
///     implementation. Non-fatal by design — the WABA stays connected even when provisioning fails, and
///     <c>ReprovisionWhatsAppFlows</c> can retry later.
/// </summary>
public sealed class WhatsAppFlowProvisioner(
    IWhatsAppBusinessAccountRepository whatsAppBusinessAccountRepository,
    WhatsAppFlowCrypto flowCrypto,
    ILogger<WhatsAppFlowProvisioner> logger
)
{
    public async Task ProvisionAsync(IMetaGraphClient metaGraphClient, WhatsAppBusinessAccount account, string accessToken, CancellationToken cancellationToken)
    {
        var baseUrl = Environment.GetEnvironmentVariable("PUBLIC_URL") ?? "https://app.nerovasystems.com";

        // Meta's Flows engine calls the data-exchange endpoint from its own infrastructure — a
        // localhost PUBLIC_URL would publish flows that can never exchange data. Flag it loudly.
        if (baseUrl.Contains("localhost", StringComparison.OrdinalIgnoreCase) || baseUrl.Contains("127.0.0.1"))
        {
            logger.LogWarning("PUBLIC_URL '{PublicUrl}' is not publicly reachable; published WhatsApp Flows will fail data exchange until it points at a public host", baseUrl);
        }

        // Upload the platform RSA public key to this WABA so Meta can encrypt data-exchange requests.
        await metaGraphClient.UploadFlowPublicKeyAsync(account.MetaWabaId, flowCrypto.PublicKeyPem, accessToken, cancellationToken);

        // Create + publish the Login Flow.
        var loginFlowJson = WhatsAppLoginFlowDefinition.Build();
        var loginEndpointUri = $"{baseUrl}/api/main/whatsapp/flows/login/data";
        var loginFlowId = await metaGraphClient.CreateAndPublishFlowAsync(
            account.MetaWabaId, "Nerova Sign In", "SIGN_IN", loginFlowJson, loginEndpointUri, accessToken, cancellationToken
        );

        // Create + publish the Booking Flow.
        var bookingFlowJson = WhatsAppBookingFlowDefinition.Build();
        var bookingEndpointUri = $"{baseUrl}/api/main/whatsapp/flows/booking/data";
        var bookingFlowId = await metaGraphClient.CreateAndPublishFlowAsync(
            account.MetaWabaId, "Nerova Booking", "APPOINTMENT_BOOKING", bookingFlowJson, bookingEndpointUri, accessToken, cancellationToken
        );

        if (!string.IsNullOrWhiteSpace(loginFlowId) || !string.IsNullOrWhiteSpace(bookingFlowId))
        {
            account.SetFlowIds(bookingFlowId, loginFlowId);
            whatsAppBusinessAccountRepository.Update(account);
        }
    }
}
