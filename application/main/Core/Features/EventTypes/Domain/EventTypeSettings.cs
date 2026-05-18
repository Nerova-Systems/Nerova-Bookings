using System.Text.Json;
using System.Text.RegularExpressions;
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

    public EventTypePrivateLink[] PrivateLinks { get; init; } = [];

    public EventTypeSelectedCalendar[] SelectedCalendars { get; init; } = [];

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
                        Placeholder = string.IsNullOrWhiteSpace(field.Placeholder) ? null : field.Placeholder.Trim(),
                        LabelAsSafeHtml = string.IsNullOrWhiteSpace(field.LabelAsSafeHtml) ? null : field.LabelAsSafeHtml.Trim(),
                        DefaultLabel = string.IsNullOrWhiteSpace(field.DefaultLabel) ? null : field.DefaultLabel.Trim(),
                        DefaultPlaceholder = string.IsNullOrWhiteSpace(field.DefaultPlaceholder) ? null : field.DefaultPlaceholder.Trim(),
                        Editable = string.IsNullOrWhiteSpace(field.Editable) ? "user" : field.Editable.Trim(),
                        ExcludeEmails = string.IsNullOrWhiteSpace(field.ExcludeEmails) ? null : field.ExcludeEmails.Trim(),
                        RequireEmails = string.IsNullOrWhiteSpace(field.RequireEmails) ? null : field.RequireEmails.Trim(),
                        Options = field.Options
                        .Where(option => !string.IsNullOrWhiteSpace(option.Label) || !string.IsNullOrWhiteSpace(option.Value))
                        .Select(option =>
                            {
                                var label = string.IsNullOrWhiteSpace(option.Label) ? option.Value.Trim() : option.Label.Trim();
                                var value = string.IsNullOrWhiteSpace(option.Value) ? label : option.Value.Trim();
                                return option with { Label = label, Value = value };
                            }
                        )
                        .DistinctBy(option => option.Value, StringComparer.OrdinalIgnoreCase)
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
                .Where(privateLink => !string.IsNullOrWhiteSpace(privateLink.Link))
                .Select(privateLink => privateLink with { Link = privateLink.Link.Trim() })
                .DistinctBy(privateLink => privateLink.Link, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            SelectedCalendars = source.SelectedCalendars
                .Where(calendar => !string.IsNullOrWhiteSpace(calendar.Integration) && !string.IsNullOrWhiteSpace(calendar.ExternalId))
                .Select(calendar => calendar with
                    {
                        Integration = calendar.Integration.Trim(),
                        ExternalId = calendar.ExternalId.Trim(),
                        CredentialId = string.IsNullOrWhiteSpace(calendar.CredentialId) ? null : calendar.CredentialId.Trim()
                    }
                )
                .DistinctBy(calendar => $"{calendar.Integration}:{calendar.ExternalId}:{calendar.CredentialId}", StringComparer.OrdinalIgnoreCase)
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
        "radioInput",
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
                field.RuleFor(f => f.Editable).Must(IsSupportedEditable).WithMessage("Booking field editable state is not supported.");
                field.RuleFor(f => f.Placeholder).MaximumLength(500);
                field.RuleFor(f => f.MinLength).Must(value => value is null or >= 0).WithMessage("Booking field minimum length must be non-negative.");
                field.RuleFor(f => f.MaxLength).Must(value => value is null or >= 0).WithMessage("Booking field maximum length must be non-negative.");
                field.RuleFor(f => f).Must(f => f.MinLength is null || f.MaxLength is null || f.MinLength <= f.MaxLength).WithMessage("Booking field minimum length must be less than or equal to maximum length.");
                field.RuleFor(f => f.Options)
                    .Must(options => options is { Length: > 0 })
                    .WithMessage("Booking field options are required for this field type.")
                    .When(bookingField => IsInSet(bookingField.Type, BookingFieldTypesRequiringOptions));
                field.RuleForEach(f => f.Options).ChildRules(option =>
                    {
                        option.RuleFor(o => o.Label).NotEmpty().WithMessage("Booking field option label is required.");
                        option.RuleFor(o => o.Value).NotEmpty().WithMessage("Booking field option value is required.");
                        option.RuleFor(o => o.Price).Must(price => price is null or >= 0).WithMessage("Booking field option price must be non-negative.");
                    }
                );
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
        RuleForEach(settings => settings.PrivateLinks).ChildRules(privateLink =>
            {
                privateLink.RuleFor(link => link.Link)
                    .Must(link => link.Trim().Length <= MaximumPrivateLinkLength)
                    .WithMessage($"Private link must be at most {MaximumPrivateLinkLength} characters.")
                    .Must(IsPrivateLink)
                    .WithMessage("Private link must contain only letters, numbers, underscores, and hyphens.");
                privateLink.RuleFor(link => link.MaxUsageCount)
                    .Must(maxUsageCount => maxUsageCount is null or > 0)
                    .WithMessage("Private link usage limit must be positive when set.");
                privateLink.RuleFor(link => link.UsageCount)
                    .GreaterThanOrEqualTo(0)
                    .WithMessage("Private link usage count must be non-negative.");
            }
        );
        RuleForEach(settings => settings.SelectedCalendars).ChildRules(calendar =>
            {
                calendar.RuleFor(c => c.Integration).NotEmpty().MaximumLength(120);
                calendar.RuleFor(c => c.ExternalId).NotEmpty().MaximumLength(500);
                calendar.RuleFor(c => c.CredentialId).MaximumLength(120);
            }
        );
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

    private static bool IsSupportedEditable(string? editable)
    {
        return editable is "system" or "system-but-optional" or "system-but-hidden" or "user" or "user-readonly";
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

public sealed record EventTypeLocation(string Type, string? Value);

public sealed record EventTypeBookingField
{
    public string Name { get; init; } = string.Empty;

    public string Label { get; init; } = string.Empty;

    public string Type { get; init; } = string.Empty;

    public bool Required { get; init; }

    public EventTypeBookingFieldOption[] Options { get; init; } = [];

    public string? LabelAsSafeHtml { get; init; }

    public string? DefaultLabel { get; init; }

    public string? Placeholder { get; init; }

    public string? DefaultPlaceholder { get; init; }

    public int? MinLength { get; init; }

    public int? MaxLength { get; init; }

    public string? ExcludeEmails { get; init; }

    public string? RequireEmails { get; init; }

    public decimal? Price { get; init; }

    public string? GetOptionsAt { get; init; }

    public Dictionary<string, EventTypeBookingFieldOptionInput> OptionsInputs { get; init; } = new(StringComparer.Ordinal);

    public string? Variant { get; init; }

    public Dictionary<string, object?>? VariantsConfig { get; init; }

    public EventTypeBookingFieldView[] Views { get; init; } = [];

    public bool HideWhenJustOneOption { get; init; }

    public bool Hidden { get; init; }

    public string Editable { get; init; } = "user";

    public EventTypeBookingFieldSource[] Sources { get; init; } = [];

    public bool DisableOnPrefill { get; init; }
}

[JsonConverter(typeof(EventTypeBookingFieldOptionJsonConverter))]
public sealed record EventTypeBookingFieldOption
{
    public EventTypeBookingFieldOption()
    {
    }

    public EventTypeBookingFieldOption(string label, string value, decimal? price = null)
    {
        Label = label;
        Value = value;
        Price = price;
    }

    public string Label { get; init; } = string.Empty;

    public string Value { get; init; } = string.Empty;

    public decimal? Price { get; init; }
}

public sealed record EventTypeBookingFieldOptionInput(string Type, bool Required, string? Placeholder);

public sealed record EventTypeBookingFieldView(string Label, string Id, string? Description);

public sealed record EventTypeBookingFieldSource(string Id, string Type, string Label, string? EditUrl, bool? FieldRequired);

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

public sealed record EventTypeSelectedCalendar
{
    public string Integration { get; init; } = string.Empty;

    public string ExternalId { get; init; } = string.Empty;

    public string? CredentialId { get; init; }
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

[JsonConverter(typeof(EventTypePrivateLinkJsonConverter))]
public sealed record EventTypePrivateLink
{
    public EventTypePrivateLink()
    {
    }

    public EventTypePrivateLink(string link, DateTimeOffset? expiresAt = null, int? maxUsageCount = null, int usageCount = 0)
    {
        Link = link;
        ExpiresAt = expiresAt;
        MaxUsageCount = maxUsageCount;
        UsageCount = usageCount;
    }

    public string Link { get; init; } = string.Empty;

    public DateTimeOffset? ExpiresAt { get; init; }

    public int? MaxUsageCount { get; init; }

    public int UsageCount { get; init; }
}

public sealed class EventTypeBookingFieldOptionJsonConverter : JsonConverter<EventTypeBookingFieldOption>
{
    public override EventTypeBookingFieldOption Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            var legacyValue = reader.GetString() ?? string.Empty;
            return new EventTypeBookingFieldOption(legacyValue, legacyValue);
        }

        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;
        var label = ReadString(root, "label") ?? ReadString(root, "Label") ?? ReadString(root, "value") ?? ReadString(root, "Value") ?? string.Empty;
        var value = ReadString(root, "value") ?? ReadString(root, "Value") ?? label;
        var price = ReadDecimal(root, "price") ?? ReadDecimal(root, "Price");
        return new EventTypeBookingFieldOption(label, value, price);
    }

    public override void Write(Utf8JsonWriter writer, EventTypeBookingFieldOption value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("label", value.Label);
        writer.WriteString("value", value.Value);
        if (value.Price is not null)
        {
            writer.WriteNumber("price", value.Price.Value);
        }

        writer.WriteEndObject();
    }

    private static string? ReadString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
    }

    private static decimal? ReadDecimal(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        return property.TryGetDecimal(out var value) ? value : null;
    }
}

public sealed class EventTypePrivateLinkJsonConverter : JsonConverter<EventTypePrivateLink>
{
    public override EventTypePrivateLink Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            return new EventTypePrivateLink(reader.GetString() ?? string.Empty);
        }

        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;
        var link = ReadString(root, "link") ?? ReadString(root, "Link") ?? string.Empty;
        var expiresAt = ReadDateTimeOffset(root, "expiresAt") ?? ReadDateTimeOffset(root, "ExpiresAt");
        var maxUsageCount = ReadInt(root, "maxUsageCount") ?? ReadInt(root, "MaxUsageCount");
        var usageCount = ReadInt(root, "usageCount") ?? ReadInt(root, "UsageCount") ?? 0;
        return new EventTypePrivateLink(link, expiresAt, maxUsageCount, usageCount);
    }

    public override void Write(Utf8JsonWriter writer, EventTypePrivateLink value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("link", value.Link);
        if (value.ExpiresAt is not null)
        {
            writer.WriteString("expiresAt", value.ExpiresAt.Value);
        }

        if (value.MaxUsageCount is not null)
        {
            writer.WriteNumber("maxUsageCount", value.MaxUsageCount.Value);
        }

        writer.WriteNumber("usageCount", value.UsageCount);
        writer.WriteEndObject();
    }

    private static string? ReadString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
    }

    private static int? ReadInt(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        return property.TryGetInt32(out var value) ? value : null;
    }

    private static DateTimeOffset? ReadDateTimeOffset(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        return property.TryGetDateTimeOffset(out var value) ? value : null;
    }
}
