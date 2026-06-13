using FluentValidation;
using JetBrains.Annotations;
using Main.Features.WhatsAppOnboarding.Domain;
using Main.Features.WhatsAppOnboarding.Shared;
using Main.Integrations.Meta;
using SharedKernel.Cqrs;
using SharedKernel.ExecutionContext;
using SharedKernel.Telemetry;

namespace Main.Features.WhatsAppOnboarding.Commands;

/// <summary>
///     Links a WhatsApp Business Account using a pre-obtained access token, bypassing the Embedded
///     Signup flow. Developer/owner escape hatch for the one case Embedded Signup cannot serve: a WABA
///     that lives in the same Meta Business Portfolio as the app itself (Meta blocks self-signup there).
///     The token is supplied at call time, protected at rest like every other WABA token, and is expected
///     to be rotated by the owner after development.
/// </summary>
[PublicAPI]
public sealed record LinkManualWabaCommand(string WabaId, string PhoneNumberId, string AccessToken) : ICommand, IRequest<Result>;

public sealed class LinkManualWabaValidator : AbstractValidator<LinkManualWabaCommand>
{
    public LinkManualWabaValidator()
    {
        RuleFor(x => x.WabaId).NotEmpty().WithMessage("The WhatsApp Business Account ID is required.");
        RuleFor(x => x.PhoneNumberId).NotEmpty().WithMessage("The phone number ID is required.");
        RuleFor(x => x.AccessToken).NotEmpty().WithMessage("The access token is required.");
    }
}

public sealed class LinkManualWabaHandler(
    IWhatsAppBusinessAccountRepository whatsAppBusinessAccountRepository,
    MetaGraphClientFactory metaGraphClientFactory,
    WhatsAppAccessTokenProtector accessTokenProtector,
    WhatsAppFlowProvisioner flowProvisioner,
    IExecutionContext executionContext,
    ITelemetryEventsCollector events
) : IRequestHandler<LinkManualWabaCommand, Result>
{
    public async Task<Result> Handle(LinkManualWabaCommand command, CancellationToken cancellationToken)
    {
        if (executionContext.UserInfo.Role != "Owner")
        {
            return Result.Forbidden("Only owners can connect a WhatsApp Business Account.");
        }

        // One WABA per tenant: if the tenant already onboarded, the operation is idempotent.
        var existingAccount = await whatsAppBusinessAccountRepository.GetByTenantAsync(cancellationToken);
        if (existingAccount is not null)
        {
            return Result.Success();
        }

        var metaGraphClient = metaGraphClientFactory.GetClient();
        var accessToken = command.AccessToken;

        if (!await metaGraphClient.RegisterPhoneNumberAsync(command.PhoneNumberId, accessToken, cancellationToken))
        {
            return Result.BadRequest($"Failed to register the WhatsApp phone number '{command.PhoneNumberId}'.");
        }

        if (!await metaGraphClient.SubscribeAppToWabaAsync(command.WabaId, accessToken, cancellationToken))
        {
            return Result.BadRequest($"Failed to subscribe the app to the WhatsApp Business Account '{command.WabaId}'.");
        }

        var waba = await metaGraphClient.GetWabaAsync(command.WabaId, accessToken, cancellationToken);
        if (waba is null)
        {
            return Result.BadRequest($"Failed to retrieve the WhatsApp Business Account '{command.WabaId}'.");
        }

        var phoneNumbers = await metaGraphClient.GetPhoneNumbersAsync(command.WabaId, accessToken, cancellationToken);
        var phoneNumber = phoneNumbers?.FirstOrDefault(p => p.Id == command.PhoneNumberId) ?? phoneNumbers?.FirstOrDefault();
        if (phoneNumber is null)
        {
            return Result.BadRequest($"Failed to retrieve the phone number for the WhatsApp Business Account '{command.WabaId}'.");
        }

        var account = WhatsAppBusinessAccount.Create(
            executionContext.TenantId!,
            waba.Id,
            waba.Name,
            accessTokenProtector.Protect(accessToken),
            WhatsAppPhoneNumber.CreateRegistered(phoneNumber.Id, phoneNumber.DisplayPhoneNumber, phoneNumber.VerifiedName)
        );
        await whatsAppBusinessAccountRepository.AddAsync(account, cancellationToken);

        // Non-fatal: provision WhatsApp Flows and register the RSA public key for the data endpoint.
        // Failures are logged and skipped — the WABA is still connected and the tenant can retry later.
        await flowProvisioner.ProvisionAsync(metaGraphClient, account, accessToken, cancellationToken);

        events.CollectEvent(new WhatsAppBusinessAccountOnboarded(account.Id));

        return Result.Success();
    }
}
