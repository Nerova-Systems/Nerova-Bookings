using System.Text.Json;
using FluentValidation;
using JetBrains.Annotations;
using Main.Features.BookingSideEffects.Domain;
using Main.Features.BookingSideEffects.Shared;
using Main.Features.EventTypes.Domain;
using SharedKernel.Cqrs;
using SharedKernel.ExecutionContext;

namespace Main.Features.BookingSideEffects.Commands;

[PublicAPI]
public sealed record CreateWebhookSubscriptionCommand(
    EventTypeId EventTypeId,
    bool Active,
    string SubscriberUrl,
    string? Secret,
    string[] Triggers,
    string PayloadFormat = "cal-com",
    string PayloadVersion = "v1"
) : ICommand, IRequest<Result<WebhookSubscriptionResponse>>;

[PublicAPI]
public sealed record UpdateWebhookSubscriptionCommand(
    EventTypeId EventTypeId,
    WebhookSubscriptionId Id,
    bool Active,
    string SubscriberUrl,
    string? Secret,
    string[] Triggers,
    string PayloadFormat = "cal-com",
    string PayloadVersion = "v1"
) : ICommand, IRequest<Result<WebhookSubscriptionResponse>>;

[PublicAPI]
public sealed record DeleteWebhookSubscriptionCommand(EventTypeId EventTypeId, WebhookSubscriptionId Id) : ICommand, IRequest<Result>;

[PublicAPI]
public sealed record TestWebhookSubscriptionCommand(EventTypeId EventTypeId, WebhookSubscriptionId Id) : ICommand, IRequest<Result<BookingSideEffectDeliverySummaryResponse>>;

public sealed class CreateWebhookSubscriptionValidator : AbstractValidator<CreateWebhookSubscriptionCommand>
{
    public CreateWebhookSubscriptionValidator()
    {
        RuleFor(command => command.SubscriberUrl)
            .NotEmpty()
            .MaximumLength(2048)
            .Must(BeHttpUrl)
            .WithMessage("Webhook subscriber URL must be an HTTP or HTTPS URL.");
        RuleFor(command => command.Secret).MaximumLength(500);
        RuleFor(command => command.Triggers).NotEmpty().WithMessage("At least one webhook trigger is required.");
        RuleForEach(command => command.Triggers).Must(IsSupportedTrigger).WithMessage("Webhook trigger is not supported.");
        RuleFor(command => command.PayloadFormat).NotEmpty().MaximumLength(80);
        RuleFor(command => command.PayloadVersion).NotEmpty().MaximumLength(40);
    }

    private static bool BeHttpUrl(string value)
    {
        return Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https";
    }

    private static bool IsSupportedTrigger(string trigger)
    {
        return BookingSideEffectConstants.SupportedTriggers.Contains(trigger.Trim().ToUpperInvariant(), StringComparer.Ordinal);
    }
}

public sealed class UpdateWebhookSubscriptionValidator : AbstractValidator<UpdateWebhookSubscriptionCommand>
{
    public UpdateWebhookSubscriptionValidator()
    {
        RuleFor(command => new CreateWebhookSubscriptionCommand(command.EventTypeId, command.Active, command.SubscriberUrl, command.Secret, command.Triggers, command.PayloadFormat, command.PayloadVersion))
            .SetValidator(new CreateWebhookSubscriptionValidator());
    }
}

public sealed class CreateWebhookSubscriptionHandler(
    IWebhookSubscriptionRepository webhookSubscriptionRepository,
    IEventTypeRepository eventTypeRepository,
    IExecutionContext executionContext
) : IRequestHandler<CreateWebhookSubscriptionCommand, Result<WebhookSubscriptionResponse>>
{
    public async Task<Result<WebhookSubscriptionResponse>> Handle(CreateWebhookSubscriptionCommand command, CancellationToken cancellationToken)
    {
        var authorization = BookingSideEffectAuthorization.CanManageWebhooks(executionContext);
        if (!authorization.IsSuccess) return Result<WebhookSubscriptionResponse>.From(authorization);

        var eventType = await BookingSideEffectAuthorization.GetOwnedEventTypeAsync(eventTypeRepository, executionContext, command.EventTypeId, cancellationToken);
        if (!eventType.IsSuccess) return Result<WebhookSubscriptionResponse>.From(eventType);

        var subscription = WebhookSubscription.Create(
            executionContext.TenantId!,
            executionContext.UserInfo.Id!,
            command.EventTypeId,
            command.Active,
            command.SubscriberUrl,
            command.Secret,
            command.Triggers,
            command.PayloadFormat,
            command.PayloadVersion
        );
        await webhookSubscriptionRepository.AddAsync(subscription, cancellationToken);
        return WebhookSubscriptionResponse.From(subscription);
    }
}

public sealed class UpdateWebhookSubscriptionHandler(
    IWebhookSubscriptionRepository webhookSubscriptionRepository,
    IEventTypeRepository eventTypeRepository,
    IExecutionContext executionContext
) : IRequestHandler<UpdateWebhookSubscriptionCommand, Result<WebhookSubscriptionResponse>>
{
    public async Task<Result<WebhookSubscriptionResponse>> Handle(UpdateWebhookSubscriptionCommand command, CancellationToken cancellationToken)
    {
        var authorization = BookingSideEffectAuthorization.CanManageWebhooks(executionContext);
        if (!authorization.IsSuccess) return Result<WebhookSubscriptionResponse>.From(authorization);

        var eventType = await BookingSideEffectAuthorization.GetOwnedEventTypeAsync(eventTypeRepository, executionContext, command.EventTypeId, cancellationToken);
        if (!eventType.IsSuccess) return Result<WebhookSubscriptionResponse>.From(eventType);

        var subscription = await webhookSubscriptionRepository.GetByIdAsync(command.Id, cancellationToken);
        if (subscription is null || subscription.EventTypeId != command.EventTypeId || subscription.OwnerUserId != executionContext.UserInfo.Id)
        {
            return Result<WebhookSubscriptionResponse>.NotFound($"Webhook subscription '{command.Id}' was not found.");
        }

        subscription.Update(command.Active, command.SubscriberUrl, command.Secret, command.Triggers, command.PayloadFormat, command.PayloadVersion);
        webhookSubscriptionRepository.Update(subscription);
        return WebhookSubscriptionResponse.From(subscription);
    }
}

public sealed class DeleteWebhookSubscriptionHandler(
    IWebhookSubscriptionRepository webhookSubscriptionRepository,
    IEventTypeRepository eventTypeRepository,
    IExecutionContext executionContext
) : IRequestHandler<DeleteWebhookSubscriptionCommand, Result>
{
    public async Task<Result> Handle(DeleteWebhookSubscriptionCommand command, CancellationToken cancellationToken)
    {
        var authorization = BookingSideEffectAuthorization.CanManageWebhooks(executionContext);
        if (!authorization.IsSuccess) return authorization;

        var eventType = await BookingSideEffectAuthorization.GetOwnedEventTypeAsync(eventTypeRepository, executionContext, command.EventTypeId, cancellationToken);
        if (!eventType.IsSuccess) return Result.From(eventType);

        var subscription = await webhookSubscriptionRepository.GetByIdAsync(command.Id, cancellationToken);
        if (subscription is null || subscription.EventTypeId != command.EventTypeId || subscription.OwnerUserId != executionContext.UserInfo.Id)
        {
            return Result.NotFound($"Webhook subscription '{command.Id}' was not found.");
        }

        webhookSubscriptionRepository.Remove(subscription);
        return Result.Success();
    }
}

public sealed class TestWebhookSubscriptionHandler(
    IWebhookSubscriptionRepository webhookSubscriptionRepository,
    IEventTypeRepository eventTypeRepository,
    IExecutionContext executionContext,
    IBookingSideEffectDeliveryRepository deliveryRepository,
    TimeProvider timeProvider
) : IRequestHandler<TestWebhookSubscriptionCommand, Result<BookingSideEffectDeliverySummaryResponse>>
{
    public async Task<Result<BookingSideEffectDeliverySummaryResponse>> Handle(TestWebhookSubscriptionCommand command, CancellationToken cancellationToken)
    {
        var authorization = BookingSideEffectAuthorization.CanManageWebhooks(executionContext);
        if (!authorization.IsSuccess) return Result<BookingSideEffectDeliverySummaryResponse>.From(authorization);

        var eventType = await BookingSideEffectAuthorization.GetOwnedEventTypeAsync(eventTypeRepository, executionContext, command.EventTypeId, cancellationToken);
        if (!eventType.IsSuccess) return Result<BookingSideEffectDeliverySummaryResponse>.From(eventType);

        var subscription = await webhookSubscriptionRepository.GetByIdAsync(command.Id, cancellationToken);
        if (subscription is null || subscription.EventTypeId != command.EventTypeId || subscription.OwnerUserId != executionContext.UserInfo.Id)
        {
            return Result<BookingSideEffectDeliverySummaryResponse>.NotFound($"Webhook subscription '{command.Id}' was not found.");
        }

        var dedupeKey = $"test:{command.Id}:{timeProvider.GetUtcNow().ToUnixTimeMilliseconds()}";
        var payloadJson = JsonSerializer.Serialize(
            new BookingWebhookDeliveryPayload(
                "WEBHOOK_TEST",
                subscription.Id.Value,
                subscription.SubscriberUrl,
                subscription.Secret,
                subscription.PayloadFormat,
                subscription.PayloadVersion,
                "test-booking",
                command.EventTypeId.Value,
                "Test booking",
                "Test Booker",
                "test@example.com",
                timeProvider.GetUtcNow(),
                timeProvider.GetUtcNow().AddMinutes(30),
                "accepted",
                null,
                null
            )
        );
        var delivery = BookingSideEffectDelivery.Create(executionContext.TenantId!, new Main.Features.Scheduling.Domain.BookingId("book_01HX0000000000000000000000"), command.EventTypeId, "WEBHOOK_TEST", BookingSideEffectConstants.WebhookKind, payloadJson, dedupeKey, timeProvider.GetUtcNow());
        await deliveryRepository.AddAsync(delivery, cancellationToken);
        return new BookingSideEffectDeliverySummaryResponse(delivery.Id.Value, delivery.BookingId.Value, delivery.Trigger, delivery.Kind, delivery.Status, delivery.Attempts, delivery.NextRetryAt, delivery.LastError);
    }
}
