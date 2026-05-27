using Account.Features.Tenants.Domain;
using JetBrains.Annotations;

namespace Account.Features.WhatsApp.Infrastructure;

/// <summary>
///     Maps the in-domain <see cref="BrandProfile" /> to the Meta wire shape
///     (<see cref="WabaProfileDto" />). Logo handling is the responsibility of the sync job —
///     this mapper does not invent a <c>profile_picture_handle</c>; the caller passes the handle
///     it received from the resumable upload (or <see langword="null" /> when the logo hasn't
///     changed).
/// </summary>
[PublicAPI]
public static class WabaProfileMapper
{
    public static WabaProfileDto Map(BrandProfile profile, string? profilePictureHandle)
    {
        ArgumentNullException.ThrowIfNull(profile);

        // Defensive truncation: Meta will 400 on overflow even though BrandProfile.Create has
        // already validated. The mapper is the wire-side boundary so we keep the safety net.
        var about = Truncate(profile.BrandAboutText, BrandProfile.BrandAboutTextMaxLength);
        var description = Truncate(profile.BrandDescription, BrandProfile.BrandDescriptionMaxLength);
        var address = Truncate(profile.BrandAddress, BrandProfile.BrandAddressMaxLength);
        var email = Truncate(profile.BrandEmail, BrandProfile.BrandEmailMaxLength);

        var websites = profile.BrandWebsites is { Count: > 0 }
            ? profile.BrandWebsites
                .Take(BrandProfile.MaxBrandWebsites)
                .Select(NormalizeWebsite)
                .ToArray()
            : null;

        return new WabaProfileDto(
            MessagingProduct: "whatsapp",
            About: about,
            Address: address,
            Description: description,
            Email: email,
            Vertical: ToWireVertical(profile.BrandVertical),
            Websites: websites,
            ProfilePictureHandle: profilePictureHandle
        );
    }

    /// <summary>
    ///     Translates the C# enum value to the wire code Meta expects on the
    ///     <c>vertical</c> field of <c>whatsapp_business_profile</c>.
    /// </summary>
    public static string ToWireVertical(MetaBusinessVertical vertical)
    {
        return vertical switch
        {
            MetaBusinessVertical.Beauty => "BEAUTY",
            MetaBusinessVertical.Education => "EDU",
            MetaBusinessVertical.Health => "HEALTH",
            MetaBusinessVertical.ProfessionalServices => "PROF_SERVICES",
            MetaBusinessVertical.Retail => "RETAIL",
            MetaBusinessVertical.Restaurant => "RESTAURANT",
            MetaBusinessVertical.Travel => "TRAVEL",
            MetaBusinessVertical.Other => "OTHER",
            _ => "OTHER"
        };
    }

    /// <summary>
    ///     Tenants commonly paste websites without a scheme ("example.com"). Meta rejects schemeless
    ///     URLs, so we prepend <c>https://</c> when it's missing. <see cref="BrandProfile.Create" />
    ///     enforces the scheme on input, but this method is also called on the in-domain value, so
    ///     the normalization is defensive against historical data.
    /// </summary>
    private static string NormalizeWebsite(string website)
    {
        var trimmed = website.Trim();
        if (trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return trimmed;
        }

        return "https://" + trimmed;
    }

    private static string? Truncate(string? value, int max)
    {
        if (value is null) return null;
        return value.Length <= max ? value : value[..max];
    }
}
