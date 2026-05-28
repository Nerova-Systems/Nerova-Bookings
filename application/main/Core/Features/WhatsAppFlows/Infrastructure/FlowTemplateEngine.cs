using System.Text.Json;
using JetBrains.Annotations;
using Main.Features.WhatsAppFlows.Domain;
using Microsoft.Extensions.Configuration;

namespace Main.Features.WhatsAppFlows.Infrastructure;

public interface IFlowTemplateEngine
{
    /// <summary>
    ///     Generates the Meta Flow JSON. The <paramref name="tier" /> parameter applies a
    ///     defensive second layer of tier gating on top of the questionnaire-time enforcement —
    ///     if a tier-restricted feature (staff selection, multiple services, custom questions)
    ///     is present in <paramref name="config" /> but the tenant's current tier no longer
    ///     allows it (e.g. they downgraded after configuring), the engine omits/truncates it
    ///     rather than producing JSON the tenant can't actually use. Defaults to
    ///     <see cref="TenantTier.Enterprise" /> for unit tests / call sites without a tier
    ///     context.
    /// </summary>
    string GenerateFlowJson(TenantFlowConfig config, string businessName, TenantTier tier = TenantTier.Enterprise);
}

/// <summary>
///     Composes a Meta WhatsApp Flow JSON document (data_api_version 3.0, version 7.0) from a
///     <see cref="TenantFlowConfig" />. Pure functional composition — no IO, no state.
///     <para>
///         Each <c>data_exchange</c> action targets the public endpoint at
///         <c>{baseUrl}/api/whatsapp/flows/v1</c>; <c>baseUrl</c> is read from configuration key
///         <c>WhatsApp:EndpointBaseUrl</c>.
///     </para>
/// </summary>
public sealed class FlowTemplateEngine(IConfiguration configuration) : IFlowTemplateEngine
{
    private const string DataApiVersion = "3.0";
    private const string Version = "7.0";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true
    };

    public string GenerateFlowJson(TenantFlowConfig config, string businessName, TenantTier tier = TenantTier.Enterprise)
    {
        var endpointUri = $"{(configuration["WhatsApp:EndpointBaseUrl"] ?? string.Empty).TrimEnd('/')}/api/whatsapp/flows/v1";

        // Defensive tier gating — see interface XML doc. Compute effective flags rather than
        // mutating the config so the persisted record remains a record of intent.
        var includeMultipleServices = config.HasMultipleServices && TierLimits.MultipleServicesInFlow(tier);
        var includeStaffSelection = config.StaffAssignment == StaffAssignment.SpecificStaff && TierLimits.StaffSelectionInFlow(tier);
        var customQuestionLimit = TierLimits.MaxCustomPreBookingQuestions(tier);
        var customQuestions = config.CustomPreBookingQuestions.OrderBy(q => q.Order).ToList();
        if (customQuestionLimit != -1 && customQuestions.Count > customQuestionLimit)
        {
            customQuestions = customQuestions.Take(customQuestionLimit).ToList();
        }

        var screens = new List<FlowScreen>();
        var routing = new Dictionary<string, string[]>();

        // ── Build screens ───────────────────────────────────────────────────
        screens.Add(BuildWelcomeScreen(businessName, includeMultipleServices));

        if (includeMultipleServices)
        {
            screens.Add(BuildSelectServiceScreen(endpointUri));
        }

        screens.Add(BuildSelectDateScreen(config));

        if (includeStaffSelection)
        {
            screens.Add(BuildSelectStaffScreen(endpointUri));
        }

        screens.Add(BuildSelectTimeScreen(endpointUri));

        if (customQuestions.Count > 0)
        {
            screens.Add(BuildCustomQuestionsScreen(customQuestions));
        }

        if (config.PaymentTiming is PaymentTiming.BeforeBooking or PaymentTiming.Deposit)
        {
            screens.Add(BuildPaymentNoticeScreen(config));
        }

        screens.Add(BuildConfirmBookingScreen(endpointUri));
        screens.Add(BuildSuccessScreen());

        // ── Routing model — linear flow between screens ─────────────────────
        for (var i = 0; i < screens.Count - 1; i++)
        {
            routing[screens[i].Id] = [screens[i + 1].Id];
        }
        routing[screens[^1].Id] = [];

        var document = new FlowDocument(DataApiVersion, Version, routing, screens.ToArray());
        return JsonSerializer.Serialize(document, JsonOptions);
    }

    private static FlowScreen BuildWelcomeScreen(string businessName, bool includeMultipleServices)
    {
        return new FlowScreen(
            Id: "WELCOME",
            Title: "Book an appointment",
            Terminal: false,
            Data: null,
            Layout: new FlowLayout(
                [
                    Heading($"Welcome to {businessName}"),
                    Body("Book your appointment in just a few steps."),
                    Footer("Get Started", Action: "navigate", NextName: includeMultipleServices ? "SELECT_SERVICE" : "SELECT_DATE", Payload: null)
                ]
            )
        );
    }

    private static FlowScreen BuildSelectServiceScreen(string endpointUri)
    {
        return new FlowScreen(
            Id: "SELECT_SERVICE",
            Title: "Choose a service",
            Terminal: false,
            Data: null,
            Layout: new FlowLayout(
                [
                    Heading("Choose a service"),
                    new Dictionary<string, object?>
                    {
                        ["type"] = "Dropdown",
                        ["name"] = "service_id",
                        ["label"] = "Service",
                        ["required"] = true,
                        ["data-source"] = new
                        {
                            type = "data-exchange",
                            payload = new { trigger = "load_services" },
                            endpoint = endpointUri
                        }
                    },
                    Footer("Continue", Action: "data_exchange", NextName: null, Payload: new { trigger = "service_selected" })
                ]
            )
        );
    }

    private static FlowScreen BuildSelectDateScreen(TenantFlowConfig config)
    {
        var minDate = config.AllowSameDayBookings
            ? DateTimeOffset.UtcNow.Date.ToString("yyyy-MM-dd")
            : DateTimeOffset.UtcNow.Date.AddDays(1).ToString("yyyy-MM-dd");
        var maxDate = DateTimeOffset.UtcNow.Date.AddDays(config.BookingWindowDays).ToString("yyyy-MM-dd");

        return new FlowScreen(
            Id: "SELECT_DATE",
            Title: "Choose a date",
            Terminal: false,
            Data: null,
            Layout: new FlowLayout(
                [
                    Heading("Choose a date"),
                    new Dictionary<string, object?>
                    {
                        ["type"] = "DatePicker",
                        ["name"] = "booking_date",
                        ["label"] = "Date",
                        ["required"] = true,
                        ["min-date"] = minDate,
                        ["max-date"] = maxDate
                    },
                    Footer("Continue", Action: "navigate", NextName: null, Payload: null)
                ]
            )
        );
    }

    private static FlowScreen BuildSelectStaffScreen(string endpointUri)
    {
        return new FlowScreen(
            Id: "SELECT_STAFF",
            Title: "Choose a staff member",
            Terminal: false,
            Data: null,
            Layout: new FlowLayout(
                [
                    Heading("Choose a staff member"),
                    new Dictionary<string, object?>
                    {
                        ["type"] = "Dropdown",
                        ["name"] = "staff_id",
                        ["label"] = "Staff",
                        ["required"] = true,
                        ["data-source"] = new
                        {
                            type = "data-exchange",
                            payload = new { trigger = "load_staff" },
                            endpoint = endpointUri
                        }
                    },
                    Footer("Continue", Action: "data_exchange", NextName: null, Payload: new { trigger = "staff_selected" })
                ]
            )
        );
    }

    private static FlowScreen BuildSelectTimeScreen(string endpointUri)
    {
        return new FlowScreen(
            Id: "SELECT_TIME",
            Title: "Choose a time",
            Terminal: false,
            Data: null,
            Layout: new FlowLayout(
                [
                    Heading("Choose a time"),
                    new Dictionary<string, object?>
                    {
                        ["type"] = "RadioButtonsGroup",
                        ["name"] = "booking_time",
                        ["label"] = "Available times",
                        ["required"] = true,
                        ["data-source"] = new
                        {
                            type = "data-exchange",
                            payload = new { trigger = "load_times" },
                            endpoint = endpointUri
                        }
                    },
                    Footer("Continue", Action: "navigate", NextName: null, Payload: null)
                ]
            )
        );
    }

    private static FlowScreen BuildCustomQuestionsScreen(List<CustomQuestion> questions)
    {
        var children = new List<object> { Heading("A few quick questions") };
        foreach (var q in questions)
        {
            children.Add(q.QuestionType switch
                {
                    CustomQuestionType.Text => (object)new Dictionary<string, object?>
                    {
                        ["type"] = "TextInput",
                        ["name"] = $"q_{q.Order}",
                        ["label"] = q.QuestionText,
                        ["required"] = q.IsRequired
                    },
                    CustomQuestionType.MultipleChoice => new Dictionary<string, object?>
                    {
                        ["type"] = "Dropdown",
                        ["name"] = $"q_{q.Order}",
                        ["label"] = q.QuestionText,
                        ["required"] = q.IsRequired,
                        ["data-source"] = (q.Choices ?? [])
                            .Select((c, idx) => new { id = $"opt_{idx}", title = c })
                            .ToArray()
                    },
                    CustomQuestionType.YesNo => new Dictionary<string, object?>
                    {
                        ["type"] = "RadioButtonsGroup",
                        ["name"] = $"q_{q.Order}",
                        ["label"] = q.QuestionText,
                        ["required"] = q.IsRequired,
                        ["data-source"] = new[]
                        {
                            new { id = "yes", title = "Yes" },
                            new { id = "no", title = "No" }
                        }
                    },
                    _ => new Dictionary<string, object?>()
                }
            );
        }
        children.Add(Footer("Continue", Action: "navigate", NextName: null, Payload: null));

        return new FlowScreen(
            Id: "CUSTOM_QUESTIONS",
            Title: "Questions",
            Terminal: false,
            Data: null,
            Layout: new FlowLayout(children.ToArray())
        );
    }

    private static FlowScreen BuildPaymentNoticeScreen(TenantFlowConfig config)
    {
        var text = config.PaymentTiming == PaymentTiming.Deposit
            ? $"A deposit of {(config.DepositAmountCents ?? 0) / 100m:F2} is required. You'll receive a payment link after confirming."
            : "Payment will be required before your booking is confirmed. You'll receive a payment link after this step.";

        return new FlowScreen(
            Id: "PAYMENT_NOTICE",
            Title: "Payment",
            Terminal: false,
            Data: null,
            Layout: new FlowLayout(
                [
                    Heading("Payment"),
                    Body(text),
                    Footer("Continue", Action: "navigate", NextName: null, Payload: null)
                ]
            )
        );
    }

    private static FlowScreen BuildConfirmBookingScreen(string endpointUri)
    {
        return new FlowScreen(
            Id: "CONFIRM_BOOKING",
            Title: "Confirm your booking",
            Terminal: false,
            Data: null,
            Layout: new FlowLayout(
                [
                    Heading("Please confirm"),
                    Body("Review your booking details before submitting."),
                    Footer("Confirm", Action: "data_exchange", NextName: null, Payload: new { trigger = "create_booking" })
                ]
            )
        );
    }

    private static FlowScreen BuildSuccessScreen()
    {
        return new FlowScreen(
            Id: "SUCCESS",
            Title: "Booked!",
            Terminal: true,
            Data: null,
            Layout: new FlowLayout(
                [
                    Heading("You're booked!"),
                    Body("We've sent you a confirmation. See you soon.")
                ]
            )
        );
    }

    private static string NextScreenAfterWelcome(TenantFlowConfig config)
    {
        // Retained for API compatibility / unit tests that may exist; production rendering
        // path now uses the effective includeMultipleServices flag computed against tier.
        return config.HasMultipleServices ? "SELECT_SERVICE" : "SELECT_DATE";
    }

    // ── Component factory helpers ───────────────────────────────────────────

    private static Dictionary<string, object?> Heading(string text)
    {
        return new Dictionary<string, object?>
        {
            ["type"] = "TextHeading",
            ["text"] = text
        };
    }

    private static Dictionary<string, object?> Body(string text)
    {
        return new Dictionary<string, object?>
        {
            ["type"] = "TextBody",
            ["text"] = text
        };
    }

    private static Dictionary<string, object?> Footer(string label, string Action, string? NextName, object? Payload)
    {
        var onClick = new Dictionary<string, object?> { ["name"] = Action };
        if (Action == "navigate" && NextName is not null)
        {
            onClick["next"] = new { type = "screen", name = NextName };
        }
        if (Payload is not null)
        {
            onClick["payload"] = Payload;
        }
        return new Dictionary<string, object?>
        {
            ["type"] = "Footer",
            ["label"] = label,
            ["on-click-action"] = onClick
        };
    }

    // ── Internal records ────────────────────────────────────────────────────

    [PublicAPI]
    private sealed record FlowDocument(
        string DataApiVersion,
        string Version,
        Dictionary<string, string[]> RoutingModel,
        FlowScreen[] Screens
    );

    [PublicAPI]
    private sealed record FlowScreen(
        string Id,
        string Title,
        bool Terminal,
        object? Data,
        FlowLayout Layout
    );

    [PublicAPI]
    private sealed record FlowLayout(object[] Children)
    {
        public string Type { get; init; } = "SingleColumnLayout";
    }
}
