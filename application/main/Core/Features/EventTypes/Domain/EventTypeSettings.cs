using FluentValidation;

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
            : source.Locations.Select(location => new EventTypeLocation(location.Type.Trim(), string.IsNullOrWhiteSpace(location.Value) ? null : location.Value.Trim())).ToArray();

        return source with
        {
            DurationOptions = durationOptions,
            Locations = locations,
            BookingFields = source.BookingFields
                .Select(field => field with
                    {
                        Name = field.Name.Trim(),
                        Label = field.Label.Trim(),
                        Type = field.Type.Trim(),
                        Options = field.Options
                            .Where(option => !string.IsNullOrWhiteSpace(option))
                            .Select(option => option.Trim())
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                            .ToArray()
                    }
                )
                .ToArray(),
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

public sealed class EventTypeSettingsValidator : AbstractValidator<EventTypeSettings>
{
    public EventTypeSettingsValidator()
    {
        RuleFor(settings => settings.DurationOptions)
            .Must(options => options.All(option => option is >= 5 and <= 1440))
            .WithMessage("Duration options must be between 5 and 1440 minutes.");
        RuleForEach(settings => settings.Locations).ChildRules(location =>
            {
                location.RuleFor(l => l.Type).Length(1, 80).WithMessage("Location type must be between 1 and 80 characters.");
                location.RuleFor(l => l.Value).MaximumLength(500).WithMessage("Location value must be at most 500 characters.");
            }
        );
        RuleForEach(settings => settings.BookingFields).ChildRules(field =>
            {
                field.RuleFor(f => f.Name).NotEmpty().WithMessage("Booking field name is required.");
                field.RuleFor(f => f.Label).NotEmpty().WithMessage("Booking field label is required.");
                field.RuleFor(f => f.Type).NotEmpty().WithMessage("Booking field type is required.");
            }
        );
        RuleFor(settings => settings.BookingWindow)
            .Must(window => window.FixedStartDate is null || window.FixedEndDate is null || window.FixedStartDate <= window.FixedEndDate)
            .WithMessage("Fixed booking window start date must be before or equal to end date.");
        RuleFor(settings => settings.Limits)
            .Must(limits =>
                IsNonNegative(limits.MaxBookingsPerDay) &&
                IsNonNegative(limits.MaxBookingDurationMinutesPerDay) &&
                IsNonNegative(limits.MaxActiveBookingsPerBooker) &&
                IsNonNegative(limits.FirstAvailableSlotMinutes) &&
                IsNonNegative(limits.OffsetStartMinutes)
            )
            .WithMessage("Event type limits must be non-negative.");
        RuleFor(settings => settings.Recurrence!.Interval)
            .GreaterThan(0)
            .WithMessage("Recurrence interval must be positive.")
            .When(settings => settings.Recurrence is not null);
        RuleFor(settings => settings.Recurrence!.Count)
            .Must(count => count is null or > 0)
            .WithMessage("Recurrence count must be positive.")
            .When(settings => settings.Recurrence is not null);
        RuleFor(settings => settings.Seats.Capacity)
            .Must(capacity => capacity is > 0)
            .WithMessage("Seats capacity must be positive when seats are enabled.")
            .When(settings => settings.Seats.Enabled);
        RuleFor(settings => settings)
            .Must(settings => settings.Recurrence is null || !settings.Seats.Enabled)
            .WithMessage("Recurring event types cannot use seats.");
    }

    private static bool IsNonNegative(int? value)
    {
        return value is null or >= 0;
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
