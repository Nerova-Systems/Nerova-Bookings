using System.Text.RegularExpressions;
using FluentValidation;
using Main.Features.Schedules.Domain;
using SharedKernel.Domain;

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

    public EventTypeInstantMeeting InstantMeeting { get; init; } = new();

    public EventTypeAiVoiceAgent AiVoiceAgent { get; init; } = new();

    public EventTypeTeamAssignment TeamAssignment { get; init; } = new();

    public EventTypeTimezone Timezone { get; init; } = new();

    public EventTypePrivacy Privacy { get; init; } = new();

    public EventTypeEmail Email { get; init; } = new();

    public bool EnablePerHostLocations { get; init; }

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
            : source.Locations.Select(location => new EventTypeLocation(location.Type.Trim(), string.IsNullOrWhiteSpace(location.Value) ? null : location.Value.Trim(), location.DisplayLocationPubliclyToTeam)).ToArray();

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
                .ToDictionary(pair => pair.Key.Trim(), pair => pair.Value, StringComparer.Ordinal),
            InstantMeeting = source.InstantMeeting,
            AiVoiceAgent = source.AiVoiceAgent,
            TeamAssignment = source.TeamAssignment,
            Timezone = source.Timezone,
            Privacy = source.Privacy,
            Email = source.Email,
            EnablePerHostLocations = source.EnablePerHostLocations
        };
    }
}

public sealed class EventTypeSettingsValidator : AbstractValidator<EventTypeSettings>
{
    private const int MaximumPrivateLinkLength = 120;
    private const int MaximumMetadataCount = 50;
    private const int MaximumMetadataKeyLength = 80;
    private const int MaximumMetadataValueLength = 500;

    private static readonly string[] SupportedBookerLayouts = ["month", "week", "column"];

    private static readonly string[] SupportedLocationTypes =
    [
        "address",
        "attendeeAddress",
        "attendeeDefined",
        "attendeePhone",
        "attendee-address",
        "attendee-defined",
        "attendee-phone",
        "in-person",
        "inPerson",
        "integration",
        "link",
        "organizer-default",
        "organizerDefault",
        "organizerDefaultApp",
        "organizersDefaultApp",
        "phone"
    ];

    private static readonly string[] SupportedBookingFieldTypes =
    [
        "address",
        "boolean",
        "checkbox",
        "email",
        "guests",
        "location",
        "multiemail",
        "multiselect",
        "name",
        "notes",
        "number",
        "phone",
        "radio",
        "rescheduleReason",
        "select",
        "splitName",
        "text",
        "textarea",
        "title",
        "url"
    ];

    private static readonly string[] BookingFieldTypesRequiringOptions = ["checkbox", "multiselect", "radio", "select"];
    private static readonly string[] SupportedRecurrenceFrequencies = ["daily", "weekly", "monthly", "yearly"];

    public EventTypeSettingsValidator()
    {
        RuleFor(settings => settings.DurationOptions)
            .Must(options => options.All(option => option is >= 5 and <= 1440))
            .WithMessage("Duration options must be between 5 and 1440 minutes.");
        RuleFor(settings => settings.BookerLayout)
            .Must(layout => IsInSet(layout, SupportedBookerLayouts))
            .WithMessage("Booker layout must be month, week, or column.");
        RuleFor(settings => settings.EventColor)
            .Must(color => color is null || IsHexColor(color))
            .WithMessage("Event color must be a valid hex color.");
        RuleForEach(settings => settings.Locations).ChildRules(location =>
            {
                location.RuleFor(l => l.Type).Length(1, 80).WithMessage("Location type must be between 1 and 80 characters.");
                location.RuleFor(l => l.Type).Must(IsSupportedLocationType).WithMessage("Location type is not supported.").When(l => !string.IsNullOrEmpty(l.Type));
                location.RuleFor(l => l.Value).MaximumLength(500).WithMessage("Location value must be at most 500 characters.");
            }
        );
        RuleForEach(settings => settings.BookingFields).ChildRules(field =>
            {
                field.RuleFor(f => f.Name).NotEmpty().WithMessage("Booking field name is required.");
                field.RuleFor(f => f.Label).NotEmpty().WithMessage("Booking field label is required.");
                field.RuleFor(f => f.Type).NotEmpty().WithMessage("Booking field type is required.");
                field.RuleFor(f => f.Type).Must(type => IsInSet(type, SupportedBookingFieldTypes)).WithMessage("Booking field type is not supported.").When(f => !string.IsNullOrEmpty(f.Type));
                field.RuleFor(f => f.Options)
                    .Must(options => options is { Length: > 0 })
                    .WithMessage("Booking field options are required for this field type.")
                    .When(bookingField => IsInSet(bookingField.Type, BookingFieldTypesRequiringOptions));
            }
        );
        RuleFor(settings => settings.BookingWindow)
            .Must(window => window.FixedStartDate is null || window.FixedEndDate is null || window.FixedStartDate <= window.FixedEndDate)
            .WithMessage("Fixed booking window start date must be before or equal to end date.");
        RuleFor(settings => settings.Limits)
            .Must(limits =>
                IsNonNegative(limits.MaxBookingsPerDay) &&
                IsNonNegative(limits.MaxBookingsPerWeek) &&
                IsNonNegative(limits.MaxBookingsPerMonth) &&
                IsNonNegative(limits.MaxBookingsPerYear) &&
                IsNonNegative(limits.MaxBookingDurationMinutesPerDay) &&
                IsNonNegative(limits.MaxBookingDurationPerDay) &&
                IsNonNegative(limits.MaxBookingDurationPerWeek) &&
                IsNonNegative(limits.MaxBookingDurationPerMonth) &&
                IsNonNegative(limits.MaxBookingDurationPerYear) &&
                IsNonNegative(limits.MaxActiveBookingsPerBooker) &&
                IsNonNegative(limits.FirstAvailableSlotMinutes) &&
                IsNonNegative(limits.OffsetStartMinutes)
            )
            .WithMessage("Event type limits must be non-negative.");
        RuleFor(settings => settings.Recurrence!.Interval)
            .GreaterThan(0)
            .WithMessage("Recurrence interval must be positive.")
            .When(settings => settings.Recurrence is not null);
        RuleFor(settings => settings.Recurrence!.Frequency)
            .Must(frequency => IsInSet(frequency, SupportedRecurrenceFrequencies))
            .WithMessage("Recurrence frequency must be daily, weekly, monthly, or yearly.")
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
        RuleForEach(settings => settings.PrivateLinks)
            .Must(privateLink => privateLink.Trim().Length <= MaximumPrivateLinkLength)
            .WithMessage($"Private link must be at most {MaximumPrivateLinkLength} characters.")
            .Must(IsPrivateLink)
            .WithMessage("Private link must contain only letters, numbers, underscores, and hyphens.");
        RuleFor(settings => settings.Redirects.SuccessUrl)
            .Must(IsHttpUrl)
            .WithMessage("Success redirect URL must be an absolute HTTP or HTTPS URL.");
        RuleFor(settings => settings.Redirects.CancellationUrl)
            .Must(IsHttpUrl)
            .WithMessage("Cancellation redirect URL must be an absolute HTTP or HTTPS URL.");
        RuleFor(settings => settings.InterfaceLanguage)
            .Must(language => language is null || IsLanguageTag(language))
            .WithMessage("Interface language must be a valid language tag.");
        RuleFor(settings => settings.Metadata)
            .Must(metadata => metadata.Count <= MaximumMetadataCount)
            .WithMessage($"Metadata must contain at most {MaximumMetadataCount} entries.");
        RuleFor(settings => settings.Metadata)
            .Must(metadata =>
                metadata.All(pair =>
                    !string.IsNullOrWhiteSpace(pair.Key) &&
                    pair.Key.Length <= MaximumMetadataKeyLength &&
                    pair.Value.Length <= MaximumMetadataValueLength
                )
            )
            .WithMessage($"Metadata keys must be at most {MaximumMetadataKeyLength} characters and values must be at most {MaximumMetadataValueLength} characters.");
        RuleFor(settings => settings.Email.EventName).MaximumLength(200).WithMessage("Custom event name must be at most 200 characters.");
        RuleFor(settings => settings.Email.CustomReplyToEmail).MaximumLength(200).WithMessage("Custom reply-to email must be at most 200 characters.");
        RuleFor(settings => settings.Timezone.TimeZone).MaximumLength(80).WithMessage("Time zone must be at most 80 characters.");
        RuleFor(settings => settings.Timezone.LockedTimeZone).MaximumLength(80).WithMessage("Locked time zone must be at most 80 characters.");
        RuleFor(settings => settings.InstantMeeting.ExpiryTimeOffsetInSeconds)
            .Must(seconds => seconds is null or >= 0)
            .WithMessage("Instant meeting expiry must be non-negative.");
        RuleFor(settings => settings.TeamAssignment.MaxLeadThreshold)
            .Must(value => value is null or > 0)
            .WithMessage("Max lead threshold must be positive.");
    }

    private static bool IsNonNegative(int? value)
    {
        return value is null or >= 0;
    }

    private static bool IsInSet(string? value, string[] supportedValues)
    {
        return value is not null && supportedValues.Contains(value.Trim(), StringComparer.OrdinalIgnoreCase);
    }

    private static bool IsHexColor(string color)
    {
        return Regex.IsMatch(color.Trim(), "^#(?:[0-9a-fA-F]{3}|[0-9a-fA-F]{6})$");
    }

    private static bool IsSupportedLocationType(string type)
    {
        return IsInSet(type, SupportedLocationTypes) || Regex.IsMatch(type, "^[a-z][a-z0-9]*(?:[-_:][a-z0-9]+)+$", RegexOptions.IgnoreCase);
    }

    private static bool IsPrivateLink(string? privateLink)
    {
        return privateLink is not null && Regex.IsMatch(privateLink.Trim(), "^[a-z0-9_-]+$", RegexOptions.IgnoreCase);
    }

    private static bool IsHttpUrl(string? url)
    {
        return url is null || (Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https");
    }

    private static bool IsLanguageTag(string language)
    {
        return Regex.IsMatch(language.Trim(), "^[a-z]{2,3}(?:-[a-z0-9]{2,8})*$", RegexOptions.IgnoreCase);
    }
}

public sealed record EventTypeLocation(string Type, string? Value, bool DisplayLocationPubliclyToTeam = false);

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

    public int? MaxBookingsPerWeek { get; init; }

    public int? MaxBookingsPerMonth { get; init; }

    public int? MaxBookingsPerYear { get; init; }

    public int? MaxBookingDurationMinutesPerDay { get; init; }

    public int? MaxBookingDurationPerDay { get; init; }

    public int? MaxBookingDurationPerWeek { get; init; }

    public int? MaxBookingDurationPerMonth { get; init; }

    public int? MaxBookingDurationPerYear { get; init; }

    public int? MaxActiveBookingsPerBooker { get; init; }

    public int? FirstAvailableSlotMinutes { get; init; }

    public int? OffsetStartMinutes { get; init; }

    public bool OnlyShowFirstAvailableSlot { get; init; }

    public bool ShowOptimizedSlots { get; init; }

    public bool MaxActiveBookingPerBookerOfferReschedule { get; init; }
}

public sealed record EventTypeConfirmationPolicy
{
    public bool RequiresConfirmation { get; init; }

    public bool RequiresBookerEmailVerification { get; init; }

    public bool BlockSlotWhilePending { get; init; }

    public bool RequiresConfirmationForFreeEmail { get; init; }

    public bool RequiresCancellationReason { get; init; }
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

    public bool AllowReschedulingPastBookings { get; init; }

    public bool AllowReschedulingCancelledBookings { get; init; }
}

public sealed record EventTypeInstantMeeting
{
    public int? ExpiryTimeOffsetInSeconds { get; init; }

    public ScheduleId? InstantMeetingScheduleId { get; init; }

    public Dictionary<string, string>? Parameters { get; init; }
}

public sealed record EventTypeAiVoiceAgent
{
    public bool Enabled { get; init; }

    public string? AgentConfig { get; init; }
}

public sealed record EventTypeTeamAssignment
{
    public bool AssignRrMembersUsingSegment { get; init; }

    public string? RrSegmentQueryValue { get; init; }

    public bool IsRrWeightsEnabled { get; init; }

    public int? MaxLeadThreshold { get; init; }

    public bool IncludeNoShowInRrCalculation { get; init; }

    public bool RescheduleWithSameRoundRobinHost { get; init; }

    public bool RrHostSubsetEnabled { get; init; }

    public HostGroup[] HostGroups { get; init; } = [];
}

public sealed record HostGroup(string Id, string Name, UserId[] MemberUserIds);

public sealed record EventTypeTimezone
{
    public string? TimeZone { get; init; }

    public bool LockTimeZoneToggleOnBookingPage { get; init; }

    public string? LockedTimeZone { get; init; }

    public bool UseBookerTimezone { get; init; }

    public ScheduleId? RestrictionScheduleId { get; init; }
}

public sealed record EventTypePrivacy
{
    public bool DisableGuests { get; init; }

    public bool HideCalendarNotes { get; init; }

    public bool HideCalendarEventDetails { get; init; }
}

public sealed record EventTypeEmail
{
    public string? EventName { get; init; }

    public string? CustomReplyToEmail { get; init; }
}

public sealed record EventTypeRedirects
{
    public string? SuccessUrl { get; init; }

    public string? CancellationUrl { get; init; }
}
