using FluentValidation;
using JetBrains.Annotations;
using Main.Features.Connectors.Domain;
using Main.Features.Connectors.Shared;
using Main.Features.EventTypes.Domain;
using Main.Features.EventTypes.Shared;
using SharedKernel.Cqrs;
using SharedKernel.ExecutionContext;

namespace Main.Features.Connectors.Commands;

[PublicAPI]
public sealed record UpdateSelectedCalendarsCommand(EventTypeId EventTypeId, EventTypeSelectedCalendar[] SelectedCalendars) : ICommand, IRequest<Result<EventTypeResponse>>;

[PublicAPI]
public sealed record UpdateDestinationCalendarCommand(EventTypeId EventTypeId, EventTypeDestinationCalendar? DestinationCalendar) : ICommand, IRequest<Result<EventTypeResponse>>;

[PublicAPI]
public sealed record UpdateDefaultConferencingCommand(EventTypeId EventTypeId, EventTypeDefaultConferencing? DefaultConferencing) : ICommand, IRequest<Result<EventTypeResponse>>;

public sealed class UpdateSelectedCalendarsValidator : AbstractValidator<UpdateSelectedCalendarsCommand>
{
    public UpdateSelectedCalendarsValidator()
    {
        RuleForEach(command => command.SelectedCalendars).ChildRules(calendar =>
            {
                calendar.RuleFor(c => c.Integration).NotEmpty().MaximumLength(120);
                calendar.RuleFor(c => c.ExternalId).NotEmpty().MaximumLength(500);
                calendar.RuleFor(c => c.CredentialId).MaximumLength(120);
            }
        );
    }
}

public sealed class UpdateDestinationCalendarValidator : AbstractValidator<UpdateDestinationCalendarCommand>
{
    public UpdateDestinationCalendarValidator()
    {
        RuleFor(command => command.DestinationCalendar).ChildRules(calendar =>
            {
                calendar.RuleFor(c => c!.Integration).NotEmpty().MaximumLength(120);
                calendar.RuleFor(c => c!.ExternalId).NotEmpty().MaximumLength(500);
                calendar.RuleFor(c => c!.CredentialId).MaximumLength(120);
            }
        ).When(command => command.DestinationCalendar is not null);
    }
}

public sealed class UpdateDefaultConferencingValidator : AbstractValidator<UpdateDefaultConferencingCommand>
{
    public UpdateDefaultConferencingValidator()
    {
        RuleFor(command => command.DefaultConferencing).ChildRules(conferencing =>
            {
                conferencing.RuleFor(c => c!.App).NotEmpty().MaximumLength(120);
                conferencing.RuleFor(c => c!.CredentialId).MaximumLength(120);
            }
        ).When(command => command.DefaultConferencing is not null);
    }
}

public sealed class UpdateSelectedCalendarsHandler(
    IEventTypeRepository eventTypeRepository,
    IConnectorCredentialRepository connectorCredentialRepository,
    IExecutionContext executionContext
) : IRequestHandler<UpdateSelectedCalendarsCommand, Result<EventTypeResponse>>
{
    public async Task<Result<EventTypeResponse>> Handle(UpdateSelectedCalendarsCommand command, CancellationToken cancellationToken)
    {
        var authorization = CoreConnectorAuthorization.CanManageConnectors(executionContext);
        if (!authorization.IsSuccess) return Result<EventTypeResponse>.From(authorization);

        var eventType = await CoreConnectorAuthorization.GetOwnedEventTypeAsync(eventTypeRepository, executionContext, command.EventTypeId, cancellationToken);
        if (!eventType.IsSuccess) return Result<EventTypeResponse>.From(eventType);

        foreach (var calendar in command.SelectedCalendars)
        {
            var result = await ValidateCalendarCredentialAsync(calendar.Integration, calendar.ExternalId, calendar.CredentialId, cancellationToken);
            if (!result.IsSuccess) return Result<EventTypeResponse>.From(result);
        }

        eventType.Value!.UpdateSettings(eventType.Value.Settings with { SelectedCalendars = command.SelectedCalendars });
        eventTypeRepository.Update(eventType.Value);
        return EventTypeResponse.From(eventType.Value);
    }

    private async Task<Result> ValidateCalendarCredentialAsync(string integration, string externalId, string? credentialId, CancellationToken cancellationToken)
    {
        if (!CoreConnectorConstants.IsCoreCalendar(integration)) return Result.BadRequest($"Calendar integration '{integration}' is not supported.");
        if (string.IsNullOrWhiteSpace(credentialId)) return Result.Success();

        var credential = await connectorCredentialRepository.GetOwnedAsync(executionContext.TenantId!, executionContext.UserInfo.Id!, credentialId, cancellationToken);
        if (credential is null) return Result.BadRequest($"Connector credential '{credentialId}' was not found.");
        if (!credential.Integration.Equals(integration.Trim(), StringComparison.OrdinalIgnoreCase)) return Result.BadRequest($"Connector credential '{credentialId}' does not match integration '{integration}'.");
        if (credential.Calendars.All(calendar => !calendar.ExternalId.Equals(externalId.Trim(), StringComparison.OrdinalIgnoreCase))) return Result.BadRequest($"Calendar '{externalId}' was not found for connector credential '{credentialId}'.");
        return Result.Success();
    }
}

public sealed class UpdateDestinationCalendarHandler(
    IEventTypeRepository eventTypeRepository,
    IConnectorCredentialRepository connectorCredentialRepository,
    IExecutionContext executionContext
) : IRequestHandler<UpdateDestinationCalendarCommand, Result<EventTypeResponse>>
{
    public async Task<Result<EventTypeResponse>> Handle(UpdateDestinationCalendarCommand command, CancellationToken cancellationToken)
    {
        var authorization = CoreConnectorAuthorization.CanManageConnectors(executionContext);
        if (!authorization.IsSuccess) return Result<EventTypeResponse>.From(authorization);

        var eventType = await CoreConnectorAuthorization.GetOwnedEventTypeAsync(eventTypeRepository, executionContext, command.EventTypeId, cancellationToken);
        if (!eventType.IsSuccess) return Result<EventTypeResponse>.From(eventType);

        if (command.DestinationCalendar is not null)
        {
            var result = await ValidateDestinationCalendarAsync(command.DestinationCalendar, cancellationToken);
            if (!result.IsSuccess) return Result<EventTypeResponse>.From(result);
        }

        eventType.Value!.UpdateSettings(eventType.Value.Settings with { DestinationCalendar = command.DestinationCalendar });
        eventTypeRepository.Update(eventType.Value);
        return EventTypeResponse.From(eventType.Value);
    }

    private async Task<Result> ValidateDestinationCalendarAsync(EventTypeDestinationCalendar destinationCalendar, CancellationToken cancellationToken)
    {
        if (!CoreConnectorConstants.IsCoreCalendar(destinationCalendar.Integration)) return Result.BadRequest($"Calendar integration '{destinationCalendar.Integration}' is not supported.");
        if (string.IsNullOrWhiteSpace(destinationCalendar.CredentialId)) return Result.Success();

        var credential = await connectorCredentialRepository.GetOwnedAsync(executionContext.TenantId!, executionContext.UserInfo.Id!, destinationCalendar.CredentialId, cancellationToken);
        if (credential is null) return Result.BadRequest($"Connector credential '{destinationCalendar.CredentialId}' was not found.");
        if (!credential.Integration.Equals(destinationCalendar.Integration.Trim(), StringComparison.OrdinalIgnoreCase)) return Result.BadRequest($"Connector credential '{destinationCalendar.CredentialId}' does not match integration '{destinationCalendar.Integration}'.");
        if (credential.Calendars.All(calendar => !calendar.ExternalId.Equals(destinationCalendar.ExternalId.Trim(), StringComparison.OrdinalIgnoreCase))) return Result.BadRequest($"Calendar '{destinationCalendar.ExternalId}' was not found for connector credential '{destinationCalendar.CredentialId}'.");
        return Result.Success();
    }
}

public sealed class UpdateDefaultConferencingHandler(
    IEventTypeRepository eventTypeRepository,
    IConnectorCredentialRepository connectorCredentialRepository,
    IExecutionContext executionContext
) : IRequestHandler<UpdateDefaultConferencingCommand, Result<EventTypeResponse>>
{
    public async Task<Result<EventTypeResponse>> Handle(UpdateDefaultConferencingCommand command, CancellationToken cancellationToken)
    {
        var authorization = CoreConnectorAuthorization.CanManageConferencing(executionContext);
        if (!authorization.IsSuccess) return Result<EventTypeResponse>.From(authorization);

        var eventType = await CoreConnectorAuthorization.GetOwnedEventTypeAsync(eventTypeRepository, executionContext, command.EventTypeId, cancellationToken);
        if (!eventType.IsSuccess) return Result<EventTypeResponse>.From(eventType);

        if (command.DefaultConferencing is not null)
        {
            var result = await ValidateConferencingCredentialAsync(command.DefaultConferencing, cancellationToken);
            if (!result.IsSuccess) return Result<EventTypeResponse>.From(result);
        }

        eventType.Value!.UpdateSettings(eventType.Value.Settings with { DefaultConferencing = command.DefaultConferencing });
        eventTypeRepository.Update(eventType.Value);
        return EventTypeResponse.From(eventType.Value);
    }

    private async Task<Result> ValidateConferencingCredentialAsync(EventTypeDefaultConferencing conferencing, CancellationToken cancellationToken)
    {
        if (!CoreConnectorConstants.IsCoreConferencing(conferencing.App)) return Result.BadRequest($"Conferencing app '{conferencing.App}' is not supported.");
        if (string.IsNullOrWhiteSpace(conferencing.CredentialId)) return Result.Success();

        var credential = await connectorCredentialRepository.GetOwnedAsync(executionContext.TenantId!, executionContext.UserInfo.Id!, conferencing.CredentialId, cancellationToken);
        if (credential is null) return Result.BadRequest($"Connector credential '{conferencing.CredentialId}' was not found.");
        var expectedIntegration = ExpectedCredentialIntegration(conferencing.App);
        if (!credential.Integration.Equals(expectedIntegration, StringComparison.OrdinalIgnoreCase)) return Result.BadRequest($"Connector credential '{conferencing.CredentialId}' does not match conferencing app '{conferencing.App}'.");
        return Result.Success();
    }

    private static string ExpectedCredentialIntegration(string app)
    {
        return app.ToLowerInvariant() switch
        {
            CoreConnectorConstants.GoogleMeet => CoreConnectorConstants.GoogleCalendar,
            CoreConnectorConstants.Office365Video => CoreConnectorConstants.Office365Calendar,
            CoreConnectorConstants.ZoomVideo => CoreConnectorConstants.ZoomVideo,
            _ => app
        };
    }
}
