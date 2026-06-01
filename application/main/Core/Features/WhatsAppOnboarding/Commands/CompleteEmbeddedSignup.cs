using FluentValidation;
using JetBrains.Annotations;
using Main.Features.WhatsAppOnboarding.Domain;
using Main.Features.WhatsAppOnboarding.Shared;
using Main.Integrations.Meta;
using SharedKernel.Cqrs;
using SharedKernel.ExecutionContext;
using SharedKernel.Telemetry;

namespace Main.Features.WhatsAppOnboarding.Commands;

[PublicAPI]
public sealed record CompleteEmbeddedSignupCommand(string Code, string WabaId, string PhoneNumberId) : ICommand, IRequest<Result>;

public sealed class CompleteEmbeddedSignupValidator : AbstractValidator<CompleteEmbeddedSignupCommand>
{
    public CompleteEmbeddedSignupValidator()
    {
        RuleFor(x => x.Code).NotEmpty().WithMessage("The authorization code is required.");
        RuleFor(x => x.WabaId).NotEmpty().WithMessage("The WhatsApp Business Account ID is required.");
        RuleFor(x => x.PhoneNumberId).NotEmpty().WithMessage("The phone number ID is required.");
    }
}

public sealed class CompleteEmbeddedSignupHandler(
    IWhatsAppBusinessAccountRepository whatsAppBusinessAccountRepository,
    MetaGraphClientFactory metaGraphClientFactory,
    WhatsAppAccessTokenProtector accessTokenProtector,
    IExecutionContext executionContext,
    ITelemetryEventsCollector events
) : IRequestHandler<CompleteEmbeddedSignupCommand, Result>
{
    public async Task<Result> Handle(CompleteEmbeddedSignupCommand command, CancellationToken cancellationToken)
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

        var accessToken = await metaGraphClient.ExchangeCodeForTokenAsync(command.Code, cancellationToken);
        if (accessToken is null)
        {
            return Result.BadRequest("Failed to exchange the authorization code for an access token.");
        }

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

        events.CollectEvent(new WhatsAppBusinessAccountOnboarded(account.Id));

        return Result.Success();
    }
}
