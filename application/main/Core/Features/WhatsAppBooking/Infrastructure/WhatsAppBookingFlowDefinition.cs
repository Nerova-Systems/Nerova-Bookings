namespace Main.Features.WhatsAppBooking.Infrastructure;

/// <summary>
///     WhatsApp Appointment Booking Flow JSON (version 7.3), based on Meta's standard appointment template.
///     Screens: APPOINTMENT (service + date + time) -> DETAILS (name, email) -> SUMMARY (terminal confirm).
///     The data endpoint serves dynamic service lists, dates, and time slots.
///     Create via Meta's Flow Builder or Flows REST API; set the returned flow_id as
///     <c>WHATSAPP_BOOKING_FLOW_ID</c> in Aspire secrets.
/// </summary>
public static class WhatsAppBookingFlowDefinition
{
    public static string Build(string dataEndpointUrl) => $$"""
        {
          "version": "7.3",
          "data_api_version": "3.0",
          "routing_model": {
            "APPOINTMENT": ["DETAILS"],
            "DETAILS": ["SUMMARY"],
            "SUMMARY": []
          },
          "data_channel_uri": "{{dataEndpointUrl}}",
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
                        "label": "Continue",
                        "on-click-action": {
                          "name": "navigate",
                          "next": { "type": "screen", "name": "DETAILS" },
                          "payload": {
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
              "id": "DETAILS",
              "title": "Your details",
              "data": {
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
                    "name": "details_form",
                    "children": [
                      {
                        "type": "TextInput",
                        "label": "Full name",
                        "name": "name",
                        "required": true,
                        "input-type": "text"
                      },
                      {
                        "type": "TextInput",
                        "label": "Email address",
                        "name": "email",
                        "required": true,
                        "input-type": "email"
                      },
                      {
                        "type": "Footer",
                        "label": "Review booking",
                        "on-click-action": {
                          "name": "data_exchange",
                          "payload": {
                            "service_slug": "${data.service_slug}",
                            "start_time_iso": "${data.start_time_iso}",
                            "duration_minutes": "${data.duration_minutes}",
                            "timezone": "${data.timezone}",
                            "name": "${form.name}",
                            "email": "${form.email}"
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
                "summary": { "type": "string", "__example__": "Product Demo\nWed 15 Jan at 10:30" },
                "service_slug": { "type": "string", "__example__": "demo" },
                "start_time_iso": { "type": "string", "__example__": "2026-01-15T10:30:00Z" },
                "duration_minutes": { "type": "number", "__example__": 30 },
                "timezone": { "type": "string", "__example__": "Africa/Johannesburg" },
                "name": { "type": "string", "__example__": "John Doe" },
                "email": { "type": "string", "__example__": "john@example.com" }
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
                        "text": "${data.summary}"
                      },
                      {
                        "type": "Footer",
                        "label": "Confirm booking",
                        "on-click-action": {
                          "name": "data_exchange",
                          "payload": {
                            "service_slug": "${data.service_slug}",
                            "start_time_iso": "${data.start_time_iso}",
                            "duration_minutes": "${data.duration_minutes}",
                            "timezone": "${data.timezone}",
                            "booker_name": "${data.name}",
                            "booker_email": "${data.email}"
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
