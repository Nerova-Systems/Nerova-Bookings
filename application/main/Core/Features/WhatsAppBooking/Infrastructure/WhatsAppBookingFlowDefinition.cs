namespace Main.Features.WhatsAppBooking.Infrastructure;

/// <summary>
///     WhatsApp Appointment Booking Flow JSON (version 7.3), based on Meta's standard appointment template.
///     Screens: APPOINTMENT (service + date + time) -> SUMMARY (terminal confirm).
///     Identity (name, email) is captured in the login Flow only; the booking flow uses the phone number
///     to look up the existing client record.
///     The data endpoint serves dynamic service lists, dates, time slots, and builds the summary text.
///     Create via Meta's Flow Builder or Flows REST API; set the returned flow_id as
///     <c>WHATSAPP_BOOKING_FLOW_ID</c> in Aspire secrets.
/// </summary>
public static class WhatsAppBookingFlowDefinition
{
    public static string Build()
    {
        return """
               {
                 "version": "7.3",
                 "data_api_version": "3.0",
                 "routing_model": {
                   "APPOINTMENT": ["SUMMARY"],
                   "SUMMARY": []
                 },
                                "screens": [
                   {
                     "id": "APPOINTMENT",
                     "title": "Book appointment",
                     "data": {
                       "service": {
                         "type": "array",
                         "items": {
                           "type": "object",
                           "properties": {
                             "id": { "type": "string" },
                             "title": { "type": "string" }
                           }
                         },
                         "__example__": [{"id": "demo", "title": "Product Demo"}]
                       },
                       "date": {
                         "type": "array",
                         "items": {
                           "type": "object",
                           "properties": {
                             "id": { "type": "string" },
                             "title": { "type": "string" }
                           }
                         },
                         "__example__": []
                       },
                       "time": {
                         "type": "array",
                         "items": {
                           "type": "object",
                           "properties": {
                             "id": { "type": "string" },
                             "title": { "type": "string" }
                           }
                         },
                         "__example__": []
                       },
                       "is_date_enabled": { "type": "boolean", "__example__": false },
                       "is_time_enabled": { "type": "boolean", "__example__": false },
                       "duration_minutes": { "type": "number", "__example__": 30 },
                       "timezone": { "type": "string", "__example__": "Africa/Johannesburg" }
                     },
                     "layout": {
                       "type": "SingleColumnLayout",
                       "children": [
                         {
                           "type": "Form",
                           "name": "appointment_form",
                           "children": [
                             {
                               "type": "Dropdown",
                               "label": "Service",
                               "name": "service",
                               "data-source": "${data.service}",
                               "required": true,
                               "on-select-action": {
                                 "name": "data_exchange",
                                 "payload": {
                                   "trigger": "service_selected",
                                   "service": "${form.service}"
                                 }
                               }
                             },
                             {
                               "type": "Dropdown",
                               "label": "Date",
                               "name": "date",
                               "data-source": "${data.date}",
                               "required": "${data.is_date_enabled}",
                               "enabled": "${data.is_date_enabled}",
                               "on-select-action": {
                                 "name": "data_exchange",
                                 "payload": {
                                   "trigger": "date_selected",
                                   "service": "${form.service}",
                                   "date": "${form.date}"
                                 }
                               }
                             },
                             {
                               "type": "Dropdown",
                               "label": "Time",
                               "name": "time",
                               "data-source": "${data.time}",
                               "required": "${data.is_time_enabled}",
                               "enabled": "${data.is_time_enabled}"
                             },
                             {
                               "type": "Footer",
                               "label": "Review booking",
                               "on-click-action": {
                                 "name": "data_exchange",
                                 "payload": {
                                   "trigger": "continue",
                                   "service_slug": "${form.service}",
                                   "start_time_iso": "${form.time}",
                                   "duration_minutes": "${data.duration_minutes}",
                                   "timezone": "${data.timezone}"
                                 }
                               }
                             }
                           ]
                         }
                       ]
                     }
                   },
                   {
                     "id": "SUMMARY",
                     "title": "Confirm booking",
                     "terminal": true,
                     "success": true,
                     "data": {
                       "summary_text": { "type": "string", "__example__": "Product Demo\nWed 15 Jan at 10:30 (30 min)" },
                       "service_slug": { "type": "string", "__example__": "demo" },
                       "start_time_iso": { "type": "string", "__example__": "2026-01-15T10:30:00Z" },
                       "duration_minutes": { "type": "number", "__example__": 30 },
                       "timezone": { "type": "string", "__example__": "Africa/Johannesburg" }
                     },
                     "layout": {
                       "type": "SingleColumnLayout",
                       "children": [
                         {
                           "type": "Form",
                           "name": "summary_form",
                           "children": [
                             {
                               "type": "TextHeading",
                               "text": "Booking summary"
                             },
                             {
                               "type": "TextBody",
                               "text": "${data.summary_text}"
                             },
                             {
                               "type": "Footer",
                               "label": "Confirm booking",
                               "on-click-action": {
                                 "name": "complete",
                                 "payload": {
                                   "service_slug": "${data.service_slug}",
                                   "start_time_iso": "${data.start_time_iso}",
                                   "duration_minutes": "${data.duration_minutes}",
                                   "timezone": "${data.timezone}"
                                 }
                               }
                             }
                           ]
                         }
                       ]
                     }
                   }
                 ]
               }
               """;
    }
}
