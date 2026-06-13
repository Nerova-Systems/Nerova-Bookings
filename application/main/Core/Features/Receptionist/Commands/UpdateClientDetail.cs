using FluentValidation;
using JetBrains.Annotations;
using Main.Features.Autonomy.Domain;
using Main.Features.Clients.Domain;
using Main.Features.Scheduling.Domain;
using SharedKernel.Cqrs;
using SharedKernel.Domain;
using SharedKernel.Telemetry;

namespace Main.Features.Receptionist.Commands;

/// <summary>
///     Structured write of one client vertical field by the AI receptionist
///     (docs/vertical-template-fields-spec.md §6). The allowlist is the tenant vertical's catalog
///     entries with AgentAccess ReadWrite — everything else is a recoverable tool error. Every write
///     produces a receipt in the "Handled by Nerova" feed (a completed <see cref="JobRun" />); writes
///     to Constraint-class fields are the owner notification itself — visible, not blocking.
///     Anonymous webhook path: explicit TenantId, unfiltered repos.
/// </summary>
[PublicAPI]
public sealed record UpdateClientDetailFromAgentCommand(
    TenantId TenantId,
    ClientId ClientId,
    string FieldKey,
    string? Value
) : ICommand, IRequest<Result<string>>;

public sealed class UpdateClientDetailFromAgentValidator : AbstractValidator<UpdateClientDetailFromAgentCommand>
{
    public UpdateClientDetailFromAgentValidator()
    {
        RuleFor(command => command.FieldKey).NotEmpty().MaximumLength(80);
        RuleFor(command => command.Value).MaximumLength(4000);
    }
}

public sealed class UpdateClientDetailFromAgentHandler(
    IClientRepository clientRepository,
    ISchedulingProfileRepository schedulingProfileRepository,
    IJobRunRepository jobRunRepository,
    ITelemetryEventsCollector events,
    TimeProvider timeProvider
) : IRequestHandler<UpdateClientDetailFromAgentCommand, Result<string>>
{
    public async Task<Result<string>> Handle(UpdateClientDetailFromAgentCommand command, CancellationToken cancellationToken)
    {
        var client = await clientRepository.GetByIdUnfilteredAsync(command.TenantId, command.ClientId, cancellationToken);
        if (client is null) return Result<string>.NotFound($"Client with id '{command.ClientId}' not found.");

        var profile = await schedulingProfileRepository.GetByTenantIdUnfilteredAsync(command.TenantId, cancellationToken);
        var vertical = profile?.Vertical;
        if (vertical is null or NerovaVertical.Other)
        {
            return Result<string>.BadRequest("This business has no vertical configured, so there are no client detail fields.");
        }

        var definition = VerticalFieldCatalog.Find(vertical.Value, command.FieldKey);
        if (definition is null)
        {
            return Result<string>.BadRequest($"Field '{command.FieldKey}' does not exist for this business.");
        }

        if (definition.AgentAccess != VerticalFieldAgentAccess.ReadWrite)
        {
            return Result<string>.BadRequest($"Field '{command.FieldKey}' is not writable by the assistant.");
        }

        if (command.Value is not null)
        {
            var error = VerticalFieldValueValidator.Validate(definition, command.Value);
            if (error is not null) return Result<string>.BadRequest(error);
        }

        client.SetVerticalField(command.FieldKey, command.Value);
        clientRepository.Update(client);

        // The receipt: a completed JobRun feeds the "Handled by Nerova" activity feed; for Constraint
        // fields this is also the owner notification (L2-style — act + tell). Trigger reference is
        // dated so repeated updates of the same field on the same day stay idempotent in the feed.
        var now = timeProvider.GetUtcNow();
        var receipt = string.IsNullOrWhiteSpace(command.Value)
            ? $"Cleared {definition.Label} for {client.FirstName} {client.LastName}".Trim()
            : $"Noted {definition.Label.ToLowerInvariant()} for {client.FirstName} {client.LastName}: {Truncate(command.Value, 120)}";

        var triggerReference = $"{client.Id}:{definition.Key}:{now:yyyyMMddHHmmss}";
        var jobRun = JobRun.Detect(command.TenantId, "ClientDetailWrite", triggerReference, receipt, null, levelAtRun: 2);
        jobRun.Complete(receipt, now);
        await jobRunRepository.AddAsync(jobRun, cancellationToken);

        events.CollectEvent(new ClientVerticalFieldsUpdated("agent", 1));
        if (definition.Sensitivity == VerticalFieldSensitivity.Constraint)
        {
            events.CollectEvent(new ConstraintFieldFlagged(definition.Key));
        }

        return receipt;
    }

    private static string Truncate(string value, int maxLength)
    {
        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : $"{trimmed[..maxLength]}…";
    }
}
