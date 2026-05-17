using System.Text.Json.Serialization;

namespace Main.Features.EventTypes.Domain;

public sealed record EventTypeSettings
{
    public int[] DurationOptions { get; init; } = [];

    public EventTypeLocation[] Locations { get; init; } = [];

    public EventTypeBookingField[] BookingFields { get; init; } = [];

    public string BookerLayout { get; init; } = "month";

    public string? EventColor { get; init; }

    public EventTypeBookingWindow BookingWindow { get; init; } = new();

    public EventTypeLimits Limits { get; init; } = new();

    public EventTypeConfirmationPolicy ConfirmationPolicy { get; init; } = new();

    public EventTypeRecurrence? Recurrence { get; init; }

    public EventTypeSeats Seats { get; init; } = new();

    public string[] PrivateLinks { get; init; } = [];

    public EventTypeCancellationPolicy CancellationPolicy { get; init; } = new();

    public EventTypeReschedulePolicy ReschedulePolicy { get; init; } = new();

    public EventTypeRedirects Redirects { get; init; } = new();

    public string? InterfaceLanguage { get; init; }

    public Dictionary<string, string> Metadata { get; init; } = new(StringComparer.Ordinal);

    public static EventTypeSettings Default(int durationMinutes, string? locationType, string? locationValue)
    {
        return Normalize(new EventTypeSettings(), durationMinutes, locationType, locationValue);
    }

    public static EventTypeSettings Normalize(
        EventTypeSettings? settings,
        int durationMinutes,
        string? locationType,
        string? locationValue
    )
    {
        var source = settings ?? new EventTypeSettings();
        var durationOptions = source.DurationOptions.Length == 0
            ? [durationMinutes]
            : source.DurationOptions.Distinct().Order().ToArray();
        var locations = source.Locations.Length == 0 && !string.IsNullOrWhiteSpace(locationType)
            ? [new EventTypeLocation(locationType.Trim(), string.IsNullOrWhiteSpace(locationValue) ? null : locationValue.Trim())]
            : source.Locations;

        return source with
        {
            DurationOptions = durationOptions,
            Locations = locations,
            BookingFields = source.BookingFields,
            BookerLayout = string.IsNullOrWhiteSpace(source.BookerLayout) ? "month" : source.BookerLayout.Trim(),
            EventColor = string.IsNullOrWhiteSpace(source.EventColor) ? null : source.EventColor.Trim(),
            BookingWindow = source.BookingWindow,
            Limits = source.Limits,
            ConfirmationPolicy = source.ConfirmationPolicy,
            Recurrence = source.Recurrence,
            Seats = source.Seats,
            PrivateLinks = source.PrivateLinks
                .Where(link => !string.IsNullOrWhiteSpace(link))
                .Select(link => link.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            CancellationPolicy = source.CancellationPolicy,
            ReschedulePolicy = source.ReschedulePolicy,
            Redirects = source.Redirects,
            InterfaceLanguage = string.IsNullOrWhiteSpace(source.InterfaceLanguage) ? null : source.InterfaceLanguage.Trim(),
            Metadata = source.Metadata
                .Where(pair => !string.IsNullOrWhiteSpace(pair.Key))
                .ToDictionary(pair => pair.Key.Trim(), pair => pair.Value, StringComparer.Ordinal)
        };
    }
}

public sealed record EventTypeLocation(string Type, string? Value);

public sealed record EventTypeBookingField(
    string Name,
    string Label,
    string Type,
    bool Required,
    string[] Options
);

public sealed record EventTypeBookingWindow
{
    public int? RollingWindowDays { get; init; }

    public DateOnly? FixedStartDate { get; init; }

    public DateOnly? FixedEndDate { get; init; }
}

public sealed record EventTypeLimits
{
    public int? MaxBookingsPerDay { get; init; }

    public int? MaxBookingDurationMinutesPerDay { get; init; }

    public int? MaxActiveBookingsPerBooker { get; init; }

    public int? FirstAvailableSlotMinutes { get; init; }

    public int? OffsetStartMinutes { get; init; }
}

public sealed record EventTypeConfirmationPolicy
{
    public bool RequiresConfirmation { get; init; }

    public bool RequiresBookerEmailVerification { get; init; }
}

public sealed record EventTypeRecurrence
{
    public string Frequency { get; init; } = "weekly";

    public int Interval { get; init; } = 1;

    public int? Count { get; init; }
}

public sealed record EventTypeSeats
{
    public bool Enabled { get; init; }

    public int? Capacity { get; init; }

    public bool ShowAttendeeInfo { get; init; }
}

public sealed record EventTypeCancellationPolicy
{
    public bool AllowCancellation { get; init; } = true;

    public int? MinimumNoticeMinutes { get; init; }
}

public sealed record EventTypeReschedulePolicy
{
    public bool AllowReschedule { get; init; } = true;

    public int? MinimumNoticeMinutes { get; init; }
}

public sealed record EventTypeRedirects
{
    public string? SuccessUrl { get; init; }

    public string? CancellationUrl { get; init; }
}

[JsonSerializable(typeof(EventTypeSettings))]
internal sealed partial class EventTypeSettingsJsonContext : JsonSerializerContext;
