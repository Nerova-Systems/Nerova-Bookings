using Account.Features.Tenants.Domain;
using FluentAssertions;
using Xunit;

namespace Account.Tests.Tenants;

/// <summary>
///     Unit coverage for <see cref="VerticalDeriver" />. Each mapping keyword and the default
///     fallback get their own row so regressions localise immediately.
/// </summary>
public sealed class VerticalDeriverTests
{
    [Theory]
    [InlineData("salon", MetaBusinessVertical.Beauty)]
    [InlineData("barber", MetaBusinessVertical.Beauty)]
    [InlineData("beauty", MetaBusinessVertical.Beauty)]
    [InlineData("hair", MetaBusinessVertical.Beauty)]
    [InlineData("tutor", MetaBusinessVertical.Education)]
    [InlineData("education", MetaBusinessVertical.Education)]
    [InlineData("school", MetaBusinessVertical.Education)]
    [InlineData("clinic", MetaBusinessVertical.Health)]
    [InlineData("medical", MetaBusinessVertical.Health)]
    [InlineData("health", MetaBusinessVertical.Health)]
    [InlineData("doctor", MetaBusinessVertical.Health)]
    [InlineData("dentist", MetaBusinessVertical.Health)]
    [InlineData("trainer", MetaBusinessVertical.ProfessionalServices)]
    [InlineData("gym", MetaBusinessVertical.ProfessionalServices)]
    [InlineData("fitness", MetaBusinessVertical.ProfessionalServices)]
    [InlineData("personal training", MetaBusinessVertical.ProfessionalServices)]
    [InlineData("restaurant", MetaBusinessVertical.Restaurant)]
    [InlineData("cafe", MetaBusinessVertical.Restaurant)]
    [InlineData("food", MetaBusinessVertical.Restaurant)]
    [InlineData("retail", MetaBusinessVertical.Retail)]
    [InlineData("shop", MetaBusinessVertical.Retail)]
    [InlineData("store", MetaBusinessVertical.Retail)]
    [InlineData("travel", MetaBusinessVertical.Travel)]
    [InlineData("accommodation", MetaBusinessVertical.Travel)]
    [InlineData("hotel", MetaBusinessVertical.Travel)]
    public void DeriveFrom_KnownKeyword_ReturnsExpectedVertical(string category, MetaBusinessVertical expected)
    {
        VerticalDeriver.DeriveFrom(category).Should().Be(expected);
    }

    [Theory]
    [InlineData("SALON")]
    [InlineData("Hair Salon")]
    [InlineData("BEAUTY SPA")]
    [InlineData("Dental CLINIC")]
    public void DeriveFrom_UpperCaseOrMixedCase_IsCaseInsensitive(string category)
    {
        var result = VerticalDeriver.DeriveFrom(category);

        result.Should().NotBe(MetaBusinessVertical.Other);
    }

    [Theory]
    [InlineData("Top Hair Studio")]
    [InlineData("City Dental Clinic")]
    [InlineData("Online Fitness Coaching")]
    public void DeriveFrom_CategoryContainsKeywordAsSubstring_Matches(string category)
    {
        var result = VerticalDeriver.DeriveFrom(category);

        result.Should().NotBe(MetaBusinessVertical.Other);
    }

    [Theory]
    [InlineData("photography")]
    [InlineData("legal")]
    [InlineData("accounting")]
    [InlineData("")]
    [InlineData("   ")]
    public void DeriveFrom_UnknownCategory_ReturnsOther(string category)
    {
        VerticalDeriver.DeriveFrom(category).Should().Be(MetaBusinessVertical.Other);
    }

    [Fact]
    public void DeriveFrom_NullInput_ReturnsOther()
    {
        VerticalDeriver.DeriveFrom(null).Should().Be(MetaBusinessVertical.Other);
    }
}
