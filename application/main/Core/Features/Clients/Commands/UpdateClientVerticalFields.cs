using System.Text.Json;
using FluentValidation;
using JetBrains.Annotations;
using Main.Features.Clients.Domain;
using Main.Features.Clients.Infrastructure;
using Main.Features.Scheduling.Domain;
using Main.Features.Scheduling.Shared;
using SharedKernel.Cqrs;
using SharedKernel.Domain;
using SharedKernel.ExecutionContext;
using SharedKernel.Telemetry;

namespace Main.Features.Clients.Commands;

/// <summary>
///     Updates Standard + Constraint vertical field values on a client
///     (docs/vertical-template-fields-spec.md §5). Every key must exist in the tenant's vertical
///     catalog and every value must pass the catalog-driven validator. Sensitive-class keys are
///     rejected here — they travel exclusively through <see cref="UpdateClientSensitiveFieldsCommand" />.
///     A null/empty value clears the field (empty is a valid state everywhere).
/// </summary>
[PublicAPI]
public sealed record UpdateClientVerticalFieldsCommand(ClientId Id, Dictionary<string, string?> Fields) : ICommand, IRequest<Result>;

public sealed class UpdateClientVerticalFieldsValidator : AbstractValidator<UpdateClientVerticalFieldsCommand>
{
    public UpdateClientVerticalFieldsValidator()
    {
        RuleFor(command => command.Fields).NotNull();
        RuleFor(command => command.Fields.Count).LessThanOrEqualTo(50).WithMessage("At most 50 fields can be updated per request.");
    }
}

public sealed class UpdateClientVerticalFieldsHandler(
    IClientRepository clientRepository,
    ISchedulingProfileRepository schedulingProfileRepository,
    IExecutionContext executionContext,
    ITelemetryEventsCollector events
) : IRequestHandler<UpdateClientVerticalFieldsCommand, Result>
{
    public async Task<Result> Handle(UpdateClientVerticalFieldsCommand command, CancellationToken cancellationToken)
    {
        var tenantId = executionContext.TenantId;
        if (tenantId is null) return Result.Unauthorized("Authentication is required.");

        var client = await clientRepository.GetByIdAsync(command.Id, cancellationToken);
        if (client is null) return Result.NotFound($"Client with id '{command.Id}' not found.");

        var vertical = await ResolveVertical(schedulingProfileRepository, tenantId, cancellationToken);
        if (vertical is null or NerovaVertical.Other)
        {
            return Result.BadRequest("No vertical is configured for this business, so there are no vertical fields to update.");
        }

        foreach (var (key, value) in command.Fields)
        {
            var definition = VerticalFieldCatalog.Find(vertical.Value, key);
            if (definition is null)
            {
                return Result.BadRequest($"Field '{key}' does not exist for the '{vertical}' vertical.");
            }

            if (definition.Sensitivity == VerticalFieldSensitivity.Sensitive)
            {
                return Result.BadRequest($"Field '{key}' is sensitive and cannot be updated through this endpoint.");
            }

            if (value is not null)
            {
                var error = VerticalFieldValueValidator.Validate(definition, value);
                if (error is not null) return Result.BadRequest(error);
            }
        }

        foreach (var (key, value) in command.Fields)
        {
            client.SetVerticalField(key, value);

            var definition = VerticalFieldCatalog.Find(vertical.Value, key)!;
            if (definition.Sensitivity == VerticalFieldSensitivity.Constraint && !string.IsNullOrWhiteSpace(value))
            {
                events.CollectEvent(new ConstraintFieldFlagged(key));
            }
        }

        clientRepository.Update(client);
        events.CollectEvent(new ClientVerticalFieldsUpdated("owner", command.Fields.Count));

        return Result.Success();
    }

    internal static async Task<NerovaVertical?> ResolveVertical(
        ISchedulingProfileRepository schedulingProfileRepository, TenantId tenantId, CancellationToken cancellationToken)
    {
        var profile = await schedulingProfileRepository.GetByTenantIdUnfilteredAsync(tenantId, cancellationToken);
        return profile?.Vertical;
    }
}

/// <summary>
///     Updates Sensitive-class field values (docs/vertical-template-fields-spec.md §3). Owner/Admin only.
///     Values are stored as an encrypted JSON payload via <see cref="FieldProtector" /> — never in
///     cleartext, never visible to agents, never in telemetry (only field keys are collected).
/// </summary>
[PublicAPI]
public sealed record UpdateClientSensitiveFieldsCommand(ClientId Id, Dictionary<string, string?> Fields) : ICommand, IRequest<Result>;

public sealed class UpdateClientSensitiveFieldsValidator : AbstractValidator<UpdateClientSensitiveFieldsCommand>
{
    public UpdateClientSensitiveFieldsValidator()
    {
        RuleFor(command => command.Fields).NotNull();
        RuleFor(command => command.Fields.Count).LessThanOrEqualTo(50).WithMessage("At most 50 fields can be updated per request.");
    }
}

public sealed class UpdateClientSensitiveFieldsHandler(
    IClientRepository clientRepository,
    ISchedulingProfileRepository schedulingProfileRepository,
    FieldProtector fieldProtector,
    IExecutionContext executionContext,
    ITelemetryEventsCollector events
) : IRequestHandler<UpdateClientSensitiveFieldsCommand, Result>
{
    private static readonly JsonSerializerOptions JsonOptions = JsonSerializerOptions.Default;

    public async Task<Result> Handle(UpdateClientSensitiveFieldsCommand command, CancellationToken cancellationToken)
    {
        if (!SchedulingAuthorization.CanManageSchedulingSetup(executionContext.UserInfo))
        {
            return Result.Forbidden("Only owners and admins can update sensitive client fields.");
        }

        var tenantId = executionContext.TenantId;
        if (tenantId is null) return Result.Unauthorized("Authentication is required.");

        var client = await clientRepository.GetByIdAsync(command.Id, cancellationToken);
        if (client is null) return Result.NotFound($"Client with id '{command.Id}' not found.");

        var vertical = await UpdateClientVerticalFieldsHandler.ResolveVertical(schedulingProfileRepository, tenantId, cancellationToken);
        if (vertical is null or NerovaVertical.Other)
        {
            return Result.BadRequest("No vertical is configured for this business, so there are no sensitive fields to update.");
        }

        foreach (var (key, value) in command.Fields)
        {
            var definition = VerticalFieldCatalog.Find(vertical.Value, key);
            if (definition is null)
            {
                return Result.BadRequest($"Field '{key}' does not exist for the '{vertical}' vertical.");
            }

            if (definition.Sensitivity != VerticalFieldSensitivity.Sensitive)
            {
                return Result.BadRequest($"Field '{key}' is not sensitive; update it through the vertical-fields endpoint.");
            }

            if (value is not null)
            {
                var error = VerticalFieldValueValidator.Validate(definition, value);
                if (error is not null) return Result.BadRequest(error);
            }
        }

        var payload = client.SensitiveFields is null
            ? new Dictionary<string, string>()
            : JsonSerializer.Deserialize<Dictionary<string, string>>(fieldProtector.Unprotect(client.SensitiveFields), JsonOptions) ?? new Dictionary<string, string>();

        foreach (var (key, value) in command.Fields)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                payload.Remove(key);
            }
            else
            {
                payload[key] = value.Trim();
            }
        }

        client.SetSensitiveFieldsPayload(payload.Count == 0 ? null : fieldProtector.Protect(JsonSerializer.Serialize(payload, JsonOptions)));
        clientRepository.Update(client);

        events.CollectEvent(new ClientVerticalFieldsUpdated("owner", command.Fields.Count));

        return Result.Success();
    }
}
