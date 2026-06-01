// This file contains all the telemetry events that are collected by the application. Telemetry events are important
// to understand how the application is being used and collect valuable information for the business. Quality is
// important, and keeping all the telemetry events in one place makes it easier to maintain high quality.
// This particular includes the naming of the telemetry events (which should be in past tense) and the properties that
// are collected with each telemetry event. Since missing or bad data cannot be fixed, it is important to have a good
// data quality from the start.

using Main.Features.WhatsAppMessaging.Domain;
using Main.Features.WhatsAppOnboarding.Domain;
using SharedKernel.Telemetry;

namespace Main.Features;

public sealed class WhatsAppBusinessAccountOnboarded(WhatsAppBusinessAccountId whatsAppBusinessAccountId)
    : TelemetryEvent(("whats_app_business_account_id", whatsAppBusinessAccountId));

public sealed class WhatsAppMessageReceived(WhatsAppMessageId whatsAppMessageId)
    : TelemetryEvent(("whats_app_message_id", whatsAppMessageId));

public sealed class WhatsAppMessageSent(WhatsAppMessageId whatsAppMessageId)
    : TelemetryEvent(("whats_app_message_id", whatsAppMessageId));


