using System.Collections.Immutable;
using FluentValidation;
using JetBrains.Annotations;
using Main.Features.Receptionist.Domain;
using SharedKernel.Cqrs;
using SharedKernel.ExecutionContext;
using SharedKernel.Telemetry;

namespace Main.Features.Receptionist.Commands;

/// <summary>
///     Owner-side configuration of the AI receptionist: the per-tenant on switch (the kill switch — spec
///     §6.5.7) plus the persona inputs (tone, languages, business notes). Creates the settings row on
///     first save.
/// </summary>
[PublicAPI]
public sealed record UpdateReceptionistSettingsCommand(bool IsEnabled, ReceptionistTone Tone, string[] Languages, string? FaqNotes, string? OwnerPhoneNumber = null)
    : ICommand, IRequest<Result>;

public sealed class UpdateReceptionistSettingsValidator : AbstractValidator<UpdateReceptionistSettingsCommand>
{
    public UpdateReceptionistSettingsValidator()
    {
        RuleFor(command => command.Languages)
            .Must(languages => languages.Length is >= 1 and <= 5 && languages.All(language => language.Trim().Length is >= 2 and <= 40))
            .WithMessage("Between 1 and 5 languages of 2 to 40 characters are required.");
        RuleFor(command => command.FaqNotes).MaximumLength(ReceptionistSettings.MaxFaqNotesLength).WithMessage($"Business notes must be at most {ReceptionistSettings.MaxFaqNotesLength} characters.");
    }
}

public sealed class UpdateReceptionistSettingsHandler(
    IReceptionistSettingsRepository receptionistSettingsRepository,
    IExecutionContext executionContext,
    ITelemetryEventsCollector events
) : IRequestHandler<UpdateReceptionistSettingsCommand, Result>
{
    public async Task<Result> Handle(UpdateReceptionistSettingsCommand command, CancellationToken cancellationToken)
    {
        var tenantId = executionContext.TenantId;
        if (tenantId is null)
        {
            return Result.Unauthorized("Authentication is required.");
        }

        var settings = await receptionistSettingsRepository.GetByTenantAsync(cancellationToken);
        var isNew = settings is null;
        settings ??= ReceptionistSettings.Create(tenantId);

        var languages = command.Languages.Select(language => language.Trim()).Where(language => language.Length > 0).ToImmutableArray();
        settings.Update(command.IsEnabled, command.Tone, languages, command.FaqNotes, command.OwnerPhoneNumber);

        if (isNew)
        {
            await receptionistSettingsRepository.AddAsync(settings, cancellationToken);
        }
        else
        {
            receptionistSettingsRepository.Update(settings);
        }

        events.CollectEvent(new ReceptionistSettingsUpdated(command.IsEnabled, command.Tone));

        return Result.Success();
    }
}
