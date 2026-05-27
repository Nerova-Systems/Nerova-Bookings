using Account.Features.Tenants.Domain;
using Account.Features.WhatsApp.Infrastructure;
using FluentAssertions;
using Xunit;

namespace Account.Tests.WhatsApp;

public sealed class WabaProfileMapperTests
{
    [Theory]
    [InlineData(MetaBusinessVertical.Beauty, "BEAUTY")]
    [InlineData(MetaBusinessVertical.Education, "EDU")]
    [InlineData(MetaBusinessVertical.Health, "HEALTH")]
    [InlineData(MetaBusinessVertical.ProfessionalServices, "PROF_SERVICES")]
    [InlineData(MetaBusinessVertical.Retail, "RETAIL")]
    [InlineData(MetaBusinessVertical.Restaurant, "RESTAURANT")]
    [InlineData(MetaBusinessVertical.Travel, "TRAVEL")]
    [InlineData(MetaBusinessVertical.Other, "OTHER")]
    public void ToWireVertical_MapsAllValues(MetaBusinessVertical vertical, string expected)
    {
        WabaProfileMapper.ToWireVertical(vertical).Should().Be(expected);
    }

    [Fact]
    public void Map_AlwaysIncludesMessagingProductWhatsApp()
    {
        var profile = BrandProfile.Create(
            "Acme", null, null, null, null, null, null, MetaBusinessVertical.Retail
        );

        var dto = WabaProfileMapper.Map(profile, profilePictureHandle: null);

        dto.MessagingProduct.Should().Be("whatsapp");
        dto.Vertical.Should().Be("RETAIL");
        dto.ProfilePictureHandle.Should().BeNull();
    }

    [Fact]
    public void Map_PassesThroughProfilePictureHandle()
    {
        var profile = BrandProfile.Create(
            "Acme", null, null, null, null, null, null, MetaBusinessVertical.Other
        );

        var dto = WabaProfileMapper.Map(profile, profilePictureHandle: "handle_abc");

        dto.ProfilePictureHandle.Should().Be("handle_abc");
    }

    [Fact]
    public void Map_OmitsWebsitesWhenEmpty()
    {
        var profile = BrandProfile.Create(
            "Acme", null, null, null, null, null, [], MetaBusinessVertical.Other
        );

        var dto = WabaProfileMapper.Map(profile, profilePictureHandle: null);

        dto.Websites.Should().BeNull();
    }
}
