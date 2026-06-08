namespace Main.Features.WhatsAppBooking.Infrastructure;

/// <summary>
///     WhatsApp Login/Registration Flow JSON (version 7.3).
///     Screens: SIGN_IN (email) -> OTP_VERIFY, or SIGN_IN -> SIGN_UP -> OTP_VERIFY -> SUCCESS.
///     The data endpoint determines routing based on whether the email is already a known client.
///     Create via Meta''s Flow Builder or Flows REST API; set the returned flow_id as
///     <c>WHATSAPP_LOGIN_FLOW_ID</c> in Aspire secrets.
/// </summary>
public static class WhatsAppLoginFlowDefinition
{
    public static string Build(string dataEndpointUrl) => $$"""
        {
          "version": "7.3",
          "data_api_version": "3.0",
          "routing_model": {
            "SIGN_IN": ["OTP_VERIFY", "SIGN_UP"],
            "SIGN_UP": ["OTP_VERIFY"],
            "OTP_VERIFY": ["SUCCESS"],
            "SUCCESS": []
          },
          "screens": [
            {
              "id": "SIGN_IN",
              "title": "Sign in",
              "data": {
                "error_message": { "type": "string", "__example__": "" }
              },
              "layout": {
                "type": "SingleColumnLayout",
                "children": [
                  {
                    "type": "Form",
                    "name": "sign_in_form",
                    "children": [
                      {
                        "type": "TextBody",
                        "text": "Enter your email address to sign in or create an account."
                      },
                      {
                        "type": "TextInput",
                        "required": true,
                        "label": "Email address",
                        "name": "email",
                        "input-type": "email",
                        "error-message": "${data.error_message}"
                      },
                      {
                        "type": "EmbeddedLink",
                        "text": "Don't have an account? Sign up",
                        "on-click-action": {
                          "name": "navigate",
                          "next": { "type": "screen", "name": "SIGN_UP" },
                          "payload": {}
                        }
                      },
                      {
                        "type": "Footer",
                        "label": "Continue",
                        "on-click-action": {
                          "name": "data_exchange",
                          "payload": { "email": "${form.email}" }
                        }
                      }
                    ]
                  }
                ]
              }
            },
            {
              "id": "SIGN_UP",
              "title": "Create account",
              "data": {
                "email": { "type": "string", "__example__": "" }
              },
              "layout": {
                "type": "SingleColumnLayout",
                "children": [
                  {
                    "type": "Form",
                    "name": "sign_up_form",
                    "children": [
                      {
                        "type": "TextInput",
                        "required": true,
                        "label": "First name",
                        "name": "first_name",
                        "input-type": "text"
                      },
                      {
                        "type": "TextInput",
                        "required": true,
                        "label": "Last name",
                        "name": "last_name",
                        "input-type": "text"
                      },
                      {
                        "type": "TextInput",
                        "required": true,
                        "label": "Email address",
                        "name": "email",
                        "input-type": "email",
                        "value": "${data.email}"
                      },
                      {
                        "type": "Footer",
                        "label": "Send verification code",
                        "on-click-action": {
                          "name": "data_exchange",
                          "payload": {
                            "first_name": "${form.first_name}",
                            "last_name": "${form.last_name}",
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
              "id": "OTP_VERIFY",
              "title": "Verify email",
              "data": {
                "email": { "type": "string", "__example__": "user@example.com" },
                "name": { "type": "string", "__example__": "John" },
                "error_message": { "type": "string", "__example__": "" }
              },
              "layout": {
                "type": "SingleColumnLayout",
                "children": [
                  {
                    "type": "Form",
                    "name": "otp_form",
                    "children": [
                      {
                        "type": "TextBody",
                        "text": "A 6-digit code was sent to ${data.email}. It expires in 15 minutes."
                      },
                      {
                        "type": "TextInput",
                        "required": true,
                        "label": "Verification code",
                        "name": "otp",
                        "input-type": "number",
                        "error-message": "${data.error_message}"
                      },
                      {
                        "type": "Footer",
                        "label": "Verify",
                        "on-click-action": {
                          "name": "data_exchange",
                          "payload": {
                            "otp": "${form.otp}",
                            "name": "${data.name}",
                            "email": "${data.email}"
                          }
                        }
                      }
                    ]
                  }
                ]
              }
            },
            {
              "id": "SUCCESS",
              "title": "You're in!",
              "terminal": true,
              "success": true,
              "data": {
                "name": { "type": "string", "__example__": "John" },
                "email": { "type": "string", "__example__": "user@example.com" }
              },
              "layout": {
                "type": "SingleColumnLayout",
                "children": [
                  {
                    "type": "TextHeading",
                    "text": "Welcome, ${data.name}!"
                  },
                  {
                    "type": "TextBody",
                    "text": "You're signed in. Tap below to book your appointment."
                  },
                  {
                    "type": "Footer",
                    "label": "Book appointment",
                    "on-click-action": {
                      "name": "data_exchange",
                      "payload": {
                        "name": "${data.name}",
                        "email": "${data.email}"
                      }
                    }
                  }
                ]
              }
            }
          ]
        }
        """;
}
