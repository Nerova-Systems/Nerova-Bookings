using Account.Features.Tenants.Domain;
using FluentAssertions;
using Xunit;

namespace Account.Tests.Tenants;

/// <summary>
///     Phase 7a — <see cref="BrandProfile" /> value object validation. Covers the length / format
///     invariants that <see cref="BrandProfile.Create" /> enforces. The mapper and command layers
///     trust these invariants so the value-object tests are the source of truth.
/// </summary>
public sealed class BrandProfileTests
{
    [Theory]
    [InlineData(BrandProfile.BusinessDisplayNameMaxLength + 1, "businessDisplayName")]
    [InlineData(BrandProfile.BrandAboutTextMaxLength + 1, "brandAboutText")]
    public void Create_ExceedsLengthLimit_Throws(int overflowLength, string field)
    {
        var oversized = new string('x', overflowLength);

        var act = () => BrandProfile.Create(
            businessDisplayName: field == "businessDisplayName" ? oversized : null,
            brandLogoUrl: null,
            brandAboutText: field == "brandAboutText" ? oversized : null,
            brandDescription: null,
            brandAddress: null,
            brandEmail: null,
            brandWebsites: null,
            brandVertical: MetaBusinessVertical.Other
        );

        act.Should().Throw<ArgumentException>().Which.ParamName.Should().Be(field);
    }

    [Fact]
    public void Create_InvalidEmail_Throws()
    {
        var act = () => BrandProfile.Create(
            "Acme", null, null, null, null, "not-an-email", null, MetaBusinessVertical.Other
        );

        act.Should().Throw<ArgumentException>().Which.ParamName.Should().Be("brandEmail");
    }

    [Fact]
    public void Create_WebsiteWithoutScheme_Throws()
    {
        var act = () => BrandProfile.Create(
            "Acme", null, null, null, null, null, ["example.com"], MetaBusinessVertical.Other
        );

        act.Should().Throw<ArgumentException>().Which.ParamName.Should().Be("brandWebsites");
    }

    [Fact]
    public void Create_MoreThanTwoWebsites_Throws()
    {
        var act = () => BrandProfile.Create(
            "Acme", null, null, null, null, null,
            ["https://a.test", "https://b.test", "https://c.test"],
            MetaBusinessVertical.Other
        );

        act.Should().Throw<ArgumentException>().Which.ParamName.Should().Be("brandWebsites");
    }

    [Fact]
    public void Create_WithValidInputs_ReturnsProfile()
    {
        var profile = BrandProfile.Create(
            businessDisplayName: "Acme",
            brandLogoUrl: "/logos/123/abc.png",
            brandAboutText: "Open 9-5",
            brandDescription: "Plumbing services",
            brandAddress: "1 Main St",
            brandEmail: "hi@acme.test",
            brandWebsites: ["https://acme.test"],
            brandVertical: MetaBusinessVertical.ProfessionalServices
        );

        profile.BusinessDisplayName.Should().Be("Acme");
        profile.BrandWebsites.Should().ContainSingle().Which.Should().Be("https://acme.test");
        profile.BrandVertical.Should().Be(MetaBusinessVertical.ProfessionalServices);
    }
}
