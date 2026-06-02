using FluentValidation;
using JetBrains.Annotations;
using Main.Features.WhatsAppMessaging.Domain;
using Main.Features.WhatsAppOnboarding.Domain;
using Main.Features.WhatsAppOnboarding.Shared;
using Main.Integrations.Meta;
using SharedKernel.Cqrs;
using SharedKernel.ExecutionContext;
using SharedKernel.Telemetry;

namespace Main.Features.WhatsAppMessaging.Commands;

[PublicAPI]
public sealed record SendWhatsAppMessageCommand(string To, string Text) : ICommand, IRequest<Result<SendWhatsAppMessageResponse>>;

[PublicAPI]
public sealed record SendWhatsAppMessageResponse(string MessageId);

public sealed class SendWhatsAppMessageValidator : AbstractValidator<SendWhatsAppMessageCommand>
{
    public SendWhatsAppMessageValidator()
    {
        RuleFor(x => x.To).NotEmpty().WithMessage("The recipient phone number is required.");
        RuleFor(x => x.Text).NotEmpty().WithMessage("The message text is required.")
            .MaximumLength(4096).WithMessage("The message text must not exceed 4096 characters.");
    }
}

public sealed class SendWhatsAppMessageHandler(
    IWhatsAppBusinessAccountRepository whatsAppBusinessAccountRepository,
    IWhatsAppMessageRepository whatsAppMessageRepository,
    MetaGraphClientFactory metaGraphClientFactory,
    WhatsAppAccessTokenProtector accessTokenProtector,
    IExecutionContext executionContext,
    ITelemetryEventsCollector events,
    TimeProvider timeProvider
) : IRequestHandler<SendWhatsAppMessageCommand, Result<SendWhatsAppMessageResponse>>
{
    public async Task<Result<SendWhatsAppMessageResponse>> Handle(SendWhatsAppMessageCommand command, CancellationToken cancellationToken)
    {
        var account = await whatsAppBusinessAccountRepository.GetByTenantAsync(cancellationToken);
        if (account is null)
        {
            return Result<SendWhatsAppMessageResponse>.BadRequest("No WhatsApp Business Account is connected for this tenant.");
        }

        var accessToken = accessTokenProtector.Unprotect(account.AccessToken);
        if (accessToken is null)
        {
            return Result<SendWhatsAppMessageResponse>.BadRequest("Your WhatsApp account connection is no longer valid. Please re-connect your WhatsApp account.");
        }
        var phoneNumberId = account.PhoneNumber.MetaPhoneNumberId;

        var metaGraphClient = metaGraphClientFactory.GetClient();
        var metaMessageId = await metaGraphClient.SendTextMessageAsync(phoneNumberId, accessToken, command.To, command.Text, cancellationToken);
        if (metaMessageId is null)
        {
            return Result<SendWhatsAppMessageResponse>.BadRequest("Failed to send the WhatsApp message. Please try again.");
        }

        var message = WhatsAppMessage.CreateOutbound(
            executionContext.TenantId!,
            metaMessageId,
            account.PhoneNumber.DisplayPhoneNumber,
            command.To,
            command.Text,
            timeProvider.GetUtcNow()
        );
        await whatsAppMessageRepository.AddAsync(message, cancellationToken);

        events.CollectEvent(new WhatsAppMessageSent(message.Id));

        return new SendWhatsAppMessageResponse(message.Id.Value);
    }
}
