using Account.Features.Tenants.Domain;
using Account.Features.WhatsApp.Domain;
using FluentAssertions;
using SharedKernel.Domain;
using Xunit;

namespace Account.Tests.WhatsApp;

/// <summary>
///     Unit coverage for <see cref="WabaConfiguration.TrySyncVerifiedNameToBrandProfile" />.
///     The sync is a guarded operation — it only runs when the display name is Approved and the
///     tenant has not edited their BusinessDisplayName locally since the request was submitted.
/// </summary>
public sealed class VerifiedNamePlumbingTests
{
    private static readonly TenantId TenantId = new(8800);
    private static readonly DateTimeOffset Now = new(2026, 6, 1, 12, 0, 0, TimeSpan.Zero);

    private static WabaConfiguration NewApprovedConfig(string requestedName, string verifiedName)
    {
        var config = WabaConfiguration.Create(TenantId, "waba_1", "phone_1", "+27 11 000 0000");
        config.SetWabaAccessToken("token_1");
        config.RequestDisplayNameChange(requestedName, Now);
        config.MarkDisplayNameReviewResult(MetaNameStatus.APPROVED, verifiedName, Now.AddDays(2));
        return config;
    }

    private static BrandProfile ProfileWithDisplayName(string? displayName)
    {
        return BrandProfile.Create(
            businessDisplayName: displayName,
            brandLogoUrl: null,
            brandAboutText: null,
            brandDescription: null,
            brandAddress: null,
            brandEmail: null,
            brandWebsites: null,
            brandVertical: MetaBusinessVertical.Other
        );
    }

    [Fact]
    public void TrySyncVerifiedNameToBrandProfile_WhenApprovedAndNamesMatch_ReturnsSyncedProfile()
    {
        var config = NewApprovedConfig("Acme Studio", "Acme Studio");
        var profile = ProfileWithDisplayName("Acme Studio");

        var result = config.TrySyncVerifiedNameToBrandProfile(profile);

        result.Should().NotBeNull();
        result!.BusinessDisplayName.Should().Be("Acme Studio");
    }

    [Fact]
    public void TrySyncVerifiedNameToBrandProfile_WhenUserEditedLocalName_ReturnsNull()
    {
        // Tenant submitted "Acme Studio" to Meta, but later edited their local name to "Acme"
        var config = NewApprovedConfig("Acme Studio", "Acme Studio");
        var profile = ProfileWithDisplayName("Acme");

        var result = config.TrySyncVerifiedNameToBrandProfile(profile);

        result.Should().BeNull("user-edited name must not be overwritten");
    }

    [Fact]
    public void TrySyncVerifiedNameToBrandProfile_WhenVerifiedNameIsNull_ReturnsNull()
    {
        var config = WabaConfiguration.Create(TenantId, "waba_1", "phone_1", "+27 11 000 0000");
        config.SetWabaAccessToken("token_1");
        config.RequestDisplayNameChange("Acme Studio", Now);
        // Meta returns APPROVED but without a verified_name (edge case)
        config.MarkDisplayNameReviewResult(MetaNameStatus.APPROVED, verifiedName: null, Now.AddDays(2));
        var profile = ProfileWithDisplayName("Acme Studio");

        var result = config.TrySyncVerifiedNameToBrandProfile(profile);

        result.Should().BeNull("null VerifiedName must be a no-op");
    }

    [Fact]
    public void TrySyncVerifiedNameToBrandProfile_WhenNotApproved_ReturnsNull()
    {
        var config = WabaConfiguration.Create(TenantId, "waba_1", "phone_1", "+27 11 000 0000");
        config.SetWabaAccessToken("token_1");
        config.RequestDisplayNameChange("Acme Studio", Now);
        // Status remains PendingReview — poller hasn't transitioned it yet
        var profile = ProfileWithDisplayName("Acme Studio");

        var result = config.TrySyncVerifiedNameToBrandProfile(profile);

        result.Should().BeNull("sync must not run while review is pending");
    }

    [Fact]
    public void TrySyncVerifiedNameToBrandProfile_WhenBrandProfileIsNull_ReturnsNull()
    {
        var config = NewApprovedConfig("Acme Studio", "Acme Studio");

        var result = config.TrySyncVerifiedNameToBrandProfile(null);

        result.Should().BeNull("null brand profile is a no-op");
    }

    [Fact]
    public void TrySyncVerifiedNameToBrandProfile_WhenDeclined_ReturnsNull()
    {
        var config = WabaConfiguration.Create(TenantId, "waba_1", "phone_1", "+27 11 000 0000");
        config.SetWabaAccessToken("token_1");
        config.RequestDisplayNameChange("Acme Studio", Now);
        config.MarkDisplayNameReviewResult(MetaNameStatus.DECLINED, verifiedName: null, Now.AddDays(2));
        var profile = ProfileWithDisplayName("Acme Studio");

        var result = config.TrySyncVerifiedNameToBrandProfile(profile);

        result.Should().BeNull("declined review must not sync");
    }

    [Fact]
    public void TrySyncVerifiedNameToBrandProfile_PreservesAllOtherProfileFields()
    {
        var config = NewApprovedConfig("Old Name", "Verified Name");
        var profile = BrandProfile.Create(
            businessDisplayName: "Old Name",
            brandLogoUrl: "/logos/123.png",
            brandAboutText: "Open 9-5",
            brandDescription: "Quality service",
            brandAddress: "1 Main St",
            brandEmail: "hi@acme.test",
            brandWebsites: ["https://acme.test"],
            brandVertical: MetaBusinessVertical.Retail
        );

        var result = config.TrySyncVerifiedNameToBrandProfile(profile);

        result.Should().NotBeNull();
        result!.BusinessDisplayName.Should().Be("Verified Name");
        result.BrandLogoUrl.Should().Be("/logos/123.png");
        result.BrandAboutText.Should().Be("Open 9-5");
        result.BrandDescription.Should().Be("Quality service");
        result.BrandAddress.Should().Be("1 Main St");
        result.BrandEmail.Should().Be("hi@acme.test");
        result.BrandWebsites.Should().ContainSingle().Which.Should().Be("https://acme.test");
        result.BrandVertical.Should().Be(MetaBusinessVertical.Retail);
    }
}
