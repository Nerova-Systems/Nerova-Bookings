using System.Net.Mail;
using JetBrains.Annotations;

namespace Account.Features.Tenants.Domain;

/// <summary>
///     Meta WhatsApp Business "vertical" enum surfaced on the business profile. These are the
///     values Meta accepts on the <c>vertical</c> field of <c>whatsapp_business_profile</c>.
///     The wire codes (<c>BEAUTY</c>, <c>EDU</c>, ...) are produced by <c>WabaProfileMapper</c>
///     when serializing for the Graph API; this enum stays in C# casing for the domain.
/// </summary>
[PublicAPI]
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MetaBusinessVertical
{
    Beauty,
    Education,
    Health,
    ProfessionalServices,
    Retail,
    Restaurant,
    Travel,
    Other
}

/// <summary>
///     A tenant's WhatsApp-facing brand profile. Mirrors the Meta
///     <c>whatsapp_business_profile</c> shape but holds the validated, in-domain form.
///     Field length limits below match the Meta API and so are enforced here too — Meta will
///     400 us if we exceed them, and we'd rather fail at the boundary than after a sync attempt.
///     <para>
///         Deviates from the original spec in two ways:
///         <list type="bullet">
///             <item>
///                 The logo is stored as a blob URL string (<c>BrandLogoUrl</c>) instead of a
///                 dedicated <c>BlobId</c> type. The Account SCS has no <c>BlobId</c>; the
///                 existing <c>Tenant.Logo</c> and <c>User.Avatar</c> features both use string
///                 URLs of the form <c>/{container}/{tenant}/...</c>.
///             </item>
///             <item>
///                 <see cref="Create" /> throws <see cref="ArgumentException" /> on invalid input
///                 rather than a custom <c>DomainException</c>. The codebase intentionally has
///                 no <c>DomainException</c> base type — invariant violations throw the standard
///                 framework exceptions and are translated by the global exception handler.
///             </item>
///         </list>
///     </para>
/// </summary>
[PublicAPI]
public sealed record BrandProfile
{
    public const int BusinessDisplayNameMaxLength = 50;
    public const int BrandAboutTextMaxLength = 139;
    public const int BrandDescriptionMaxLength = 512;
    public const int BrandAddressMaxLength = 256;
    public const int BrandEmailMaxLength = 128;
    public const int BrandWebsiteMaxLength = 256;
    public const int MaxBrandWebsites = 2;

    private BrandProfile(
        string? businessDisplayName,
        string? brandLogoUrl,
        string? brandAboutText,
        string? brandDescription,
        string? brandAddress,
        string? brandEmail,
        IReadOnlyList<string> brandWebsites,
        MetaBusinessVertical brandVertical)
    {
        BusinessDisplayName = businessDisplayName;
        BrandLogoUrl = brandLogoUrl;
        BrandAboutText = brandAboutText;
        BrandDescription = brandDescription;
        BrandAddress = brandAddress;
        BrandEmail = brandEmail;
        BrandWebsites = brandWebsites;
        BrandVertical = brandVertical;
    }

    // Parameterless ctor required by EF Core for owned-type materialization.
    private BrandProfile() : this(null, null, null, null, null, null, [], MetaBusinessVertical.Other)
    {
    }

    public string? BusinessDisplayName { get; private init; }

    public string? BrandLogoUrl { get; private init; }

    public string? BrandAboutText { get; private init; }

    public string? BrandDescription { get; private init; }

    public string? BrandAddress { get; private init; }

    public string? BrandEmail { get; private init; }

    public IReadOnlyList<string> BrandWebsites { get; private init; } = [];

    public MetaBusinessVertical BrandVertical { get; private init; } = MetaBusinessVertical.Other;

    /// <summary>
    ///     Validates and constructs a <see cref="BrandProfile" />. Throws
    ///     <see cref="ArgumentException" /> when any field violates its length, format, or count
    ///     limit. Callers in handlers should translate the failure into a <c>Result.BadRequest</c>.
    /// </summary>
    public static BrandProfile Create(
        string? businessDisplayName,
        string? brandLogoUrl,
        string? brandAboutText,
        string? brandDescription,
        string? brandAddress,
        string? brandEmail,
        IReadOnlyList<string>? brandWebsites,
        MetaBusinessVertical brandVertical)
    {
        EnsureMaxLength(businessDisplayName, BusinessDisplayNameMaxLength, nameof(businessDisplayName));
        EnsureMaxLength(brandAboutText, BrandAboutTextMaxLength, nameof(brandAboutText));
        EnsureMaxLength(brandDescription, BrandDescriptionMaxLength, nameof(brandDescription));
        EnsureMaxLength(brandAddress, BrandAddressMaxLength, nameof(brandAddress));
        EnsureMaxLength(brandEmail, BrandEmailMaxLength, nameof(brandEmail));

        if (!string.IsNullOrWhiteSpace(brandEmail) && !IsValidEmail(brandEmail))
        {
            throw new ArgumentException($"'{brandEmail}' is not a valid email address.", nameof(brandEmail));
        }

        var websites = brandWebsites is null ? [] : brandWebsites.ToArray();
        if (websites.Length > MaxBrandWebsites)
        {
            throw new ArgumentException(
                $"At most {MaxBrandWebsites} brand websites are allowed; got {websites.Length}.",
                nameof(brandWebsites)
            );
        }

        foreach (var website in websites)
        {
            if (string.IsNullOrWhiteSpace(website))
            {
                throw new ArgumentException("Brand websites cannot be empty.", nameof(brandWebsites));
            }

            if (website.Length > BrandWebsiteMaxLength)
            {
                throw new ArgumentException(
                    $"Brand website exceeds {BrandWebsiteMaxLength} characters: '{website}'.",
                    nameof(brandWebsites)
                );
            }

            if (!website.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                && !website.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException(
                    $"Brand website must start with http:// or https://: '{website}'.",
                    nameof(brandWebsites)
                );
            }
        }

        return new BrandProfile(
            businessDisplayName,
            brandLogoUrl,
            brandAboutText,
            brandDescription,
            brandAddress,
            brandEmail,
            websites,
            brandVertical
        );
    }

    private static void EnsureMaxLength(string? value, int maxLength, string fieldName)
    {
        if (value is not null && value.Length > maxLength)
        {
            throw new ArgumentException($"{fieldName} exceeds {maxLength} characters.", fieldName);
        }
    }

    private static bool IsValidEmail(string email)
    {
        try
        {
            var addr = new MailAddress(email);
            return addr.Address == email;
        }
        catch
        {
            return false;
        }
    }
}
