using System.Text.Json.Serialization;
using Account.Features.Users.Domain;
using JetBrains.Annotations;
using SharedKernel.Cqrs;

namespace Account.Features.Users.Queries;

[PublicAPI]
public sealed record GetCurrentUserPreferencesQuery : IRequest<Result<UserPreferencesResponse>>;

[PublicAPI]
public sealed record UserPreferencesResponse(
    TimeFormat TimeFormat,
    [property: JsonConverter(typeof(JsonStringEnumConverter<DayOfWeek>))] DayOfWeek WeekStart,
    string Language,
    string TimeZone
)
{
    public static UserPreferencesResponse FromAggregateOrDefault(UserPreferences? preferences)
    {
        return preferences is null
            ? new UserPreferencesResponse(
                UserPreferences.DefaultTimeFormat,
                UserPreferences.DefaultWeekStart,
                UserPreferences.DefaultLanguage,
                UserPreferences.DefaultTimeZone
            )
            : new UserPreferencesResponse(preferences.TimeFormat, preferences.WeekStart, preferences.Language, preferences.TimeZone);
    }
}

public sealed class GetCurrentUserPreferencesHandler(IUserRepository userRepository, IUserPreferencesRepository preferencesRepository)
    : IRequestHandler<GetCurrentUserPreferencesQuery, Result<UserPreferencesResponse>>
{
    public async Task<Result<UserPreferencesResponse>> Handle(GetCurrentUserPreferencesQuery query, CancellationToken cancellationToken)
    {
        var user = await userRepository.GetLoggedInUserAsync(cancellationToken);
        var preferences = await preferencesRepository.GetByUserIdAsync(user.Id, cancellationToken);
        return UserPreferencesResponse.FromAggregateOrDefault(preferences);
    }
}
