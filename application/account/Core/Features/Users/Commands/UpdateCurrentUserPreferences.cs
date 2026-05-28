using Account.Features.Users.Domain;
using Account.Features.Users.Queries;
using FluentValidation;
using JetBrains.Annotations;
using SharedKernel.Cqrs;
using SharedKernel.SinglePageApp;
using SharedKernel.Telemetry;

namespace Account.Features.Users.Commands;

/// <summary>
///     Partial update of the current user's preferences. <see langword="null" /> fields are left
///     unchanged (<c>PATCH</c> semantics). On first write, a default row is materialised, then the
///     incoming fields are applied.
/// </summary>
[PublicAPI]
public sealed record UpdateCurrentUserPreferencesCommand(
    TimeFormat? TimeFormat,
    DayOfWeek? WeekStart,
    string? Language,
    string? TimeZone
) : ICommand, IRequest<Result<UserPreferencesResponse>>;

public sealed class UpdateCurrentUserPreferencesValidator : AbstractValidator<UpdateCurrentUserPreferencesCommand>
{
    public UpdateCurrentUserPreferencesValidator()
    {
        RuleFor(x => x.Language!)
            .Must(lang => SinglePageAppConfiguration.SupportedLocalizations.Contains(lang))
            .When(x => x.Language is not null)
            .WithMessage($"Language must be one of the following: {string.Join(", ", SinglePageAppConfiguration.SupportedLocalizations)}");

        RuleFor(x => x.TimeZone!)
            .Must(IsValidIanaTimeZone)
            .When(x => x.TimeZone is not null)
            .WithMessage("Time zone must be a valid IANA identifier (e.g. 'UTC', 'Europe/Copenhagen').");
    }

    private static bool IsValidIanaTimeZone(string timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId)) return false;

        try
        {
            // .NET 8+ resolves IANA IDs on all supported platforms (Windows uses ICU).
            TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            return true;
        }
        catch (TimeZoneNotFoundException)
        {
            return false;
        }
        catch (InvalidTimeZoneException)
        {
            return false;
        }
    }
}

public sealed class UpdateCurrentUserPreferencesHandler(
    IUserRepository userRepository,
    IUserPreferencesRepository preferencesRepository,
    ITelemetryEventsCollector events
) : IRequestHandler<UpdateCurrentUserPreferencesCommand, Result<UserPreferencesResponse>>
{
    public async Task<Result<UserPreferencesResponse>> Handle(UpdateCurrentUserPreferencesCommand command, CancellationToken cancellationToken)
    {
        var user = await userRepository.GetLoggedInUserAsync(cancellationToken);
        var preferences = await preferencesRepository.GetByUserIdAsync(user.Id, cancellationToken);

        if (preferences is null)
        {
            preferences = UserPreferences.CreateDefault(user.Id);
            preferences.Update(command.TimeFormat, command.WeekStart, command.Language, command.TimeZone);
            await preferencesRepository.AddAsync(preferences, cancellationToken);
        }
        else
        {
            preferences.Update(command.TimeFormat, command.WeekStart, command.Language, command.TimeZone);
            preferencesRepository.Update(preferences);
        }

        events.CollectEvent(new UserPreferencesUpdated(user.Id));

        return new UserPreferencesResponse(preferences.TimeFormat, preferences.WeekStart, preferences.Language, preferences.TimeZone);
    }
}
