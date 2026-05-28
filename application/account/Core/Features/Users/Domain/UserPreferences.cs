using JetBrains.Annotations;
using SharedKernel.Domain;
using SharedKernel.StronglyTypedIds;

namespace Account.Features.Users.Domain;

/// <summary>
///     Strongly-typed identifier for a <see cref="UserPreferences" /> aggregate.
///     Uses ULID for chronological ordering and global uniqueness. Prefix: <c>uprf</c>.
/// </summary>
[PublicAPI]
[IdPrefix("uprf")]
[JsonConverter(typeof(StronglyTypedIdJsonConverter<string, UserPreferencesId>))]
public sealed record UserPreferencesId(string Value) : StronglyTypedUlid<UserPreferencesId>(Value)
{
    public override string ToString()
    {
        return Value;
    }
}

/// <summary>
///     Per-user display and locale preferences. A single <see cref="UserPreferences" /> row exists
///     per <see cref="User" />, keyed on <see cref="UserId" /> (1:1).
///     <para>
///         Mirrors the cal.com Prisma <c>User</c> preference fields (<c>timeFormat</c>,
///         <c>weekStart</c>, <c>timeZone</c>, <c>locale</c>). Kept as a separate aggregate rather
///         than properties on <see cref="User" /> so the user-facing settings surface evolves
///         independently of the auth/identity model.
///     </para>
///     <para>
///         Defaults are returned by <see cref="CreateDefault" /> when the user has no row yet
///         (24-hour, Monday, <c>en-US</c>, <c>UTC</c>). Persistence is lazy: the first write
///         materialises the row.
///     </para>
///     <para>
///         <see cref="UserPreferences" /> is intentionally NOT <see cref="ITenantScopedEntity" />:
///         a user's preferences follow the user across tenant boundaries (back-office, multi-org
///         lookups). The <see cref="UserId" /> FK cascades on user delete so preferences are
///         cleaned up automatically.
///     </para>
/// </summary>
public sealed class UserPreferences : AggregateRoot<UserPreferencesId>
{
    public const string DefaultLanguage = "en-US";

    public const string DefaultTimeZone = "UTC";

    public const TimeFormat DefaultTimeFormat = TimeFormat.TwentyFourHour;

    public const DayOfWeek DefaultWeekStart = DayOfWeek.Monday;

    private UserPreferences(UserPreferencesId id, UserId userId, TimeFormat timeFormat, DayOfWeek weekStart, string language, string timeZone)
        : base(id)
    {
        UserId = userId;
        TimeFormat = timeFormat;
        WeekStart = weekStart;
        Language = language;
        TimeZone = timeZone;
    }

    /// <summary>The user these preferences belong to. Unique (1:1).</summary>
    public UserId UserId { get; }

    /// <summary>12-hour vs 24-hour clock display. Maps to cal.com <c>User.timeFormat</c>.</summary>
    public TimeFormat TimeFormat { get; private set; }

    /// <summary>First day of the week in calendar surfaces. Maps to cal.com <c>User.weekStart</c>.</summary>
    public DayOfWeek WeekStart { get; private set; }

    /// <summary>BCP-47 language tag (e.g. <c>en-US</c>). Maps to cal.com <c>User.locale</c>.</summary>
    public string Language { get; private set; }

    /// <summary>IANA time zone identifier (e.g. <c>Europe/Copenhagen</c>). Maps to cal.com <c>User.timeZone</c>.</summary>
    public string TimeZone { get; private set; }

    /// <summary>
    ///     Builds an in-memory default preferences object for the given user. Not yet persisted.
    ///     Callers can return this directly on read (for users with no row) or pass it to
    ///     <see cref="IUserPreferencesRepository.AddAsync" /> to materialise it on first write.
    /// </summary>
    public static UserPreferences CreateDefault(UserId userId)
    {
        return new UserPreferences(UserPreferencesId.NewId(), userId, DefaultTimeFormat, DefaultWeekStart, DefaultLanguage, DefaultTimeZone);
    }

    /// <summary>
    ///     Applies a partial update. Any argument left <see langword="null" /> keeps the existing
    ///     value, supporting <c>PATCH</c> semantics. Callers must pre-validate inputs.
    /// </summary>
    public void Update(TimeFormat? timeFormat, DayOfWeek? weekStart, string? language, string? timeZone)
    {
        if (timeFormat.HasValue) TimeFormat = timeFormat.Value;
        if (weekStart.HasValue) WeekStart = weekStart.Value;
        if (language is not null) Language = language;
        if (timeZone is not null) TimeZone = timeZone;
    }
}

[PublicAPI]
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TimeFormat
{
    TwelveHour,
    TwentyFourHour
}
