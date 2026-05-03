using SharedKernel.SinglePageApp;

namespace Account.Features.EmailAuthentication.Shared;

internal static class EmailDomainHelper
{
    // Resolves the host portion of PUBLIC_URL (e.g. "app.platformplatform.net"). The OtpAutofill
    // helper renders this as the "@<domain> #<code>" suffix in the email plaintext, which iOS Mail and
    // Gmail use to bind the OTP to the verify-form host so autofill only triggers on the correct site.
    public static string GetPublicHost()
    {
        var publicUrl = Environment.GetEnvironmentVariable(SinglePageAppConfiguration.PublicUrlKey);
        if (string.IsNullOrWhiteSpace(publicUrl)) return string.Empty;
        return Uri.TryCreate(publicUrl, UriKind.Absolute, out var uri) ? uri.Host : string.Empty;
    }
}
