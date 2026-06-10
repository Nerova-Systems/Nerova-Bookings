namespace Main.Features.WhatsAppBooking.Infrastructure;

/// <summary>
///     WhatsApp Login/Registration Flow JSON (version 7.3).
///     Screens: DETAILS (name + email + prefilled phone) -> OTP_VERIFY -> CONFIRM (terminal).
///     Email + OTP are mandatory: the email doubles as an account-recovery key so a returning
///     client on a new WhatsApp number is matched by email, verified by OTP, and has the new
///     number written to their record. Phone is prefilled with the verified WhatsApp sender and
///     is read-only; the server always trusts the verified sender number, not a typed value.
///     The data endpoint URI is set via the Flows API (not in the JSON) — see
///     <see cref="Main.Integrations.Meta.IMetaGraphClient.CreateAndPublishFlowAsync" /> /
///     <see cref="Main.Integrations.Meta.IMetaGraphClient.UpdateFlowJsonAsync" />.
/// </summary>
public static class WhatsAppLoginFlowDefinition
{
    public static string Build()
    {
        return """
               {
                 "version": "7.3",
                 "data_api_version": "3.0",
                 "routing_model": {
                   "DETAILS": ["OTP_VERIFY"],
                   "OTP_VERIFY": ["CONFIRM"],
                   "CONFIRM": []
                 },
                 "screens": [
                   {
                     "id": "DETAILS",
                     "title": "Your details",
                     "data": {
                       "name": { "type": "string", "__example__": "" },
                       "email": { "type": "string", "__example__": "" },
                       "phone": { "type": "string", "__example__": "" },
                       "error_message": { "type": "string", "__example__": "" }
                     },
                     "layout": {
                       "type": "SingleColumnLayout",
                       "children": [
                         {
                           "type": "Form",
                           "name": "details_form",
                           "children": [
                             {
                               "type": "TextBody",
                               "text": "Confirm your details to sign in or create an account."
                             },
                             {
                               "type": "TextInput",
                               "required": true,
                               "label": "Full name",
                               "name": "name",
                               "input-type": "text",
                               "init-value": "${data.name}"
                             },
                             {
                               "type": "TextBody",
                               "text": "${data.error_message}"
                             },
                             {
                               "type": "TextInput",
                               "required": true,
                               "label": "Email address",
                               "name": "email",
                               "input-type": "email",
                               "init-value": "${data.email}"
                             },
                             {
                               "type": "TextInput",
                               "label": "Phone number",
                               "name": "phone",
                               "input-type": "phone",
                               "init-value": "${data.phone}",
                               "enabled": false
                             },
                             {
                               "type": "Footer",
                               "label": "Continue",
                               "on-click-action": {
                                 "name": "data_exchange",
                                 "payload": {
                                   "name": "${form.name}",
                                   "email": "${form.email}",
                                   "phone": "${data.phone}"
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
                       "name": { "type": "string", "__example__": "John Doe" },
                       "email": { "type": "string", "__example__": "user@example.com" },
                       "phone": { "type": "string", "__example__": "" },
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
                               "type": "TextBody",
                               "text": "${data.error_message}"
                             },
                             {
                               "type": "TextInput",
                               "required": true,
                               "label": "Verification code",
                               "name": "otp",
                               "input-type": "number"
                             },
                             {
                               "type": "Footer",
                               "label": "Verify",
                               "on-click-action": {
                                 "name": "data_exchange",
                                 "payload": {
                                   "otp": "${form.otp}",
                                   "name": "${data.name}",
                                   "email": "${data.email}",
                                   "phone": "${data.phone}"
                                 }
                               }
                             }
                           ]
                         }
                       ]
                     }
                   },
                   {
                     "id": "CONFIRM",
                     "title": "All set",
                     "terminal": true,
                     "success": true,
                     "data": {
                       "name": { "type": "string", "__example__": "John Doe" },
                       "email": { "type": "string", "__example__": "user@example.com" },
                       "phone": { "type": "string", "__example__": "" }
                     },
                     "layout": {
                       "type": "SingleColumnLayout",
                       "children": [
                         {
                           "type": "TextHeading",
                           "text": "You're all set, ${data.name}!"
                         },
                         {
                           "type": "TextBody",
                           "text": "Email: ${data.email}"
                         },
                         {
                           "type": "TextBody",
                           "text": "Phone: ${data.phone}"
                         },
                         {
                           "type": "Footer",
                           "label": "Confirm",
                           "on-click-action": {
                             "name": "complete",
                             "payload": {
                               "name": "${data.name}",
                               "email": "${data.email}",
                               "phone": "${data.phone}"
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
}
