namespace Main.Features.Workflows.Infrastructure;

/// <summary>
///     Twilio REST API credentials. Bound from environment variables at startup.
///     When values are missing, <see cref="TwilioSmsProvider" /> short-circuits and returns
///     <c>NotConfigured</c> instead of throwing so the workflow scheduler keeps ticking.
/// </summary>
public sealed class TwilioOptions
{
    /// <summary>Twilio account SID (starts with "AC...").</summary>
    public string AccountSid { get; set; } = string.Empty;

    /// <summary>Twilio auth token (basic-auth password against AccountSid).</summary>
    public string AuthToken { get; set; } = string.Empty;

    /// <summary>Outbound sender — an E.164 number provisioned on the account.</summary>
    public string FromNumber { get; set; } = string.Empty;

    /// <summary>API base URL — overridable for tests.</summary>
    public string ApiBaseUrl { get; init; } = "https://api.twilio.com/2010-04-01";

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(AccountSid)
        && !string.IsNullOrWhiteSpace(AuthToken)
        && !string.IsNullOrWhiteSpace(FromNumber);
}
