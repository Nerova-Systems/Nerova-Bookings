using JetBrains.Annotations;
using Main.Features.WhatsAppBooking.Infrastructure;
using Main.Features.WhatsAppOnboarding.Domain;
using Main.Features.WhatsAppOnboarding.Shared;
using Main.Integrations.Meta;
using SharedKernel.Cqrs;
using SharedKernel.ExecutionContext;

namespace Main.Features.WhatsAppOnboarding.Commands;

[PublicAPI]
public sealed record ReprovisionWhatsAppFlowsCommand : ICommand, IRequest<Result>;

/// <summary>
///     Re-uploads the correct Flow JSON to the tenant's existing WhatsApp Flows and re-publishes them.
///     Used when the initial provisioning uploaded the wrong JSON (e.g. Meta's default placeholder).
///     Also re-registers the RSA public key for the data endpoint.
///     Non-destructive: does not delete or recreate flows — only updates the JSON asset and publishes.
/// </summary>
public sealed class ReprovisionWhatsAppFlowsHandler(
    IWhatsAppBusinessAccountRepository whatsAppBusinessAccountRepository,
    MetaGraphClientFactory metaGraphClientFactory,
    WhatsAppAccessTokenProtector accessTokenProtector,
    WhatsAppFlowCrypto flowCrypto,
    IExecutionContext executionContext
) : IRequestHandler<ReprovisionWhatsAppFlowsCommand, Result>
{
    public async Task<Result> Handle(ReprovisionWhatsAppFlowsCommand command, CancellationToken cancellationToken)
    {
        if (executionContext.UserInfo.Role != "Owner")
        {
            return Result.Forbidden("Only owners can reprovision WhatsApp Flows.");
        }

        var account = await whatsAppBusinessAccountRepository.GetByTenantAsync(cancellationToken);
        if (account is null)
        {
            return Result.BadRequest("No WhatsApp Business Account connected. Complete embedded signup first.");
        }

        var metaGraphClient = metaGraphClientFactory.GetClient();
        var accessToken = accessTokenProtector.Unprotect(account.AccessToken);
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return Result.BadRequest("Could not decrypt the access token. Please reconnect your WhatsApp account.");
        }

        var baseUrl = Environment.GetEnvironmentVariable("PUBLIC_URL") ?? "https://app.nerovasystems.com";

        // Always re-register the RSA public key (idempotent).
        await metaGraphClient.UploadFlowPublicKeyAsync(account.MetaWabaId, flowCrypto.PublicKeyPem, accessToken, cancellationToken);

        var loginFlowJson = WhatsAppLoginFlowDefinition.Build($"{baseUrl}/api/main/whatsapp/flows/login/data");
        var bookingFlowJson = WhatsAppBookingFlowDefinition.Build($"{baseUrl}/api/main/whatsapp/flows/booking/data");

        var loginFlowId = account.LoginFlowId;
        var bookingFlowId = account.BookingFlowId;

        // Update existing flows if IDs are known, otherwise create new ones.
        if (!string.IsNullOrWhiteSpace(loginFlowId))
        {
            await metaGraphClient.UpdateFlowJsonAsync(loginFlowId, loginFlowJson, accessToken, cancellationToken);
        }
        else
        {
            loginFlowId = await metaGraphClient.CreateAndPublishFlowAsync(account.MetaWabaId, "Nerova Sign In", "SIGN_IN", loginFlowJson, accessToken, cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(bookingFlowId))
        {
            await metaGraphClient.UpdateFlowJsonAsync(bookingFlowId, bookingFlowJson, accessToken, cancellationToken);
        }
        else
        {
            bookingFlowId = await metaGraphClient.CreateAndPublishFlowAsync(account.MetaWabaId, "Nerova Booking", "APPOINTMENT_BOOKING", bookingFlowJson, accessToken, cancellationToken);
        }

        account.SetFlowIds(bookingFlowId, loginFlowId);
        whatsAppBusinessAccountRepository.Update(account);

        return Result.Success();
    }
}
