using Account.Features.WhatsApp.Infrastructure;
using FluentAssertions;
using Xunit;

namespace Account.Tests.WhatsApp;

/// <summary>
///     Unit coverage for <see cref="WabaProfileDriftDetector.DiffFields" />. Drift detection is the
///     gate that decides whether a sync is even enqueued, so each field's comparator gets a row.
///     End-to-end tenant scanning + outbox enqueue is exercised by the integration-style worker
///     tests; this file pins the pure comparison rules so a regression in one field surfaces with a
///     focused failure message rather than a 500 in a higher-level test.
/// </summary>
public sealed class WabaProfileDriftJobTests
{
    private static WabaProfileDto Expected(
        string? about = "about",
        string? address = "addr",
        string? description = "desc",
        string? email = "e@x.com",
        string? vertical = "RETAIL",
        IReadOnlyList<string>? websites = null
    ) => new(
        MessagingProduct: "whatsapp",
        About: about,
        Address: address,
        Description: description,
        Email: email,
        Vertical: vertical,
        Websites: websites ?? ["https://a.com"],
        ProfilePictureHandle: null
    );

    private static RemoteWabaProfileDto Remote(
        string? about = "about",
        string? address = "addr",
        string? description = "desc",
        string? email = "e@x.com",
        string? vertical = "RETAIL",
        IReadOnlyList<string>? websites = null,
        string? profilePictureUrl = null
    ) => new(
        About: about,
        Address: address,
        Description: description,
        Email: email,
        Vertical: vertical,
        Websites: websites ?? ["https://a.com"],
        ProfilePictureUrl: profilePictureUrl
    );

    [Fact]
    public void DiffFields_WhenAllFieldsMatch_ReturnsEmpty()
    {
        WabaProfileDriftDetector.DiffFields(Expected(), Remote(), localBrandLogoUrl: null)
            .Should().BeEmpty();
    }

    [Fact]
    public void DiffFields_WhenAboutDiffers_FlagsAbout()
    {
        WabaProfileDriftDetector.DiffFields(Expected(about: "x"), Remote(about: "y"), null)
            .Should().ContainSingle().Which.Should().Be("about");
    }

    [Fact]
    public void DiffFields_WhenAddressDiffers_FlagsAddress()
    {
        WabaProfileDriftDetector.DiffFields(Expected(address: "a"), Remote(address: "b"), null)
            .Should().Contain("address");
    }

    [Fact]
    public void DiffFields_WhenDescriptionDiffers_FlagsDescription()
    {
        WabaProfileDriftDetector.DiffFields(Expected(description: "a"), Remote(description: "b"), null)
            .Should().Contain("description");
    }

    [Fact]
    public void DiffFields_WhenEmailDiffers_FlagsEmail()
    {
        WabaProfileDriftDetector.DiffFields(Expected(email: "a@x.com"), Remote(email: "b@x.com"), null)
            .Should().Contain("email");
    }

    [Fact]
    public void DiffFields_WhenVerticalDiffers_FlagsVertical()
    {
        WabaProfileDriftDetector.DiffFields(Expected(vertical: "RETAIL"), Remote(vertical: "TRAVEL"), null)
            .Should().Contain("vertical");
    }

    [Fact]
    public void DiffFields_WhenWebsitesOrderDiffers_FlagsWebsites()
    {
        // SequenceEqual: order matters per WABA convention (primary, secondary).
        WabaProfileDriftDetector.DiffFields(
            Expected(websites: ["https://a.com", "https://b.com"]),
            Remote(websites: ["https://b.com", "https://a.com"]),
            null
        ).Should().Contain("websites");
    }

    [Fact]
    public void DiffFields_WhenWebsitesContentDiffers_FlagsWebsites()
    {
        WabaProfileDriftDetector.DiffFields(
            Expected(websites: ["https://a.com"]),
            Remote(websites: ["https://b.com"]),
            null
        ).Should().Contain("websites");
    }

    [Fact]
    public void DiffFields_WhenWebsitesBothNullOrEmpty_DoesNotFlagWebsites()
    {
        WabaProfileDriftDetector.DiffFields(
            Expected(websites: []),
            Remote(websites: []),
            null
        ).Should().NotContain("websites");
    }

    [Fact]
    public void DiffFields_WhenLocalHasLogoButRemoteDoesNot_FlagsProfilePicture()
    {
        WabaProfileDriftDetector.DiffFields(Expected(), Remote(profilePictureUrl: null), localBrandLogoUrl: "/blob/x.png")
            .Should().Contain("profile_picture");
    }

    [Fact]
    public void DiffFields_WhenRemoteHasLogoButLocalDoesNot_FlagsProfilePicture()
    {
        WabaProfileDriftDetector.DiffFields(Expected(), Remote(profilePictureUrl: "https://cdn/x.png"), localBrandLogoUrl: null)
            .Should().Contain("profile_picture");
    }

    [Fact]
    public void DiffFields_WhenBothHaveLogos_DoesNotFlagProfilePicture()
    {
        // Presence-only comparison: we never compare CDN bytes because Meta rewrites the URL.
        // A hash-based check is done at upload-time in the processor, not here.
        WabaProfileDriftDetector.DiffFields(
            Expected(),
            Remote(profilePictureUrl: "https://cdn/different.png"),
            localBrandLogoUrl: "/blob/local.png"
        ).Should().NotContain("profile_picture");
    }

    [Fact]
    public void DiffFields_WhenMultipleFieldsDiffer_ReturnsAll()
    {
        WabaProfileDriftDetector.DiffFields(
            Expected(about: "local", email: "local@x.com"),
            Remote(about: "remote", email: "remote@x.com"),
            null
        ).Should().BeEquivalentTo("about", "email");
    }
}
