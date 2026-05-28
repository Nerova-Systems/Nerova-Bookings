using Account.Features.Attributes.Domain;
using Account.Features.Memberships.Domain;
using Account.Features.Tenants.Domain;
using FluentAssertions;
using SharedKernel.Domain;
using Xunit;
using OrgAttribute = Account.Features.Attributes.Domain.Attribute;

namespace Account.Tests.Attributes;

/// <summary>
///     Unit tests for <see cref="System.Attribute" /> and <see cref="Features.Attributes.Domain.AttributeAssignment" />
///     aggregate invariants.
///     Pure in-memory — no database required.
/// </summary>
public sealed class AttributeTests
{
    private static readonly TenantId OrgTenantId = TenantId.NewId();

    // ──────────────────────────────────────────────────────────────────────────
    // OrgAttribute.Create
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Create_ShouldSetFieldsCorrectly()
    {
        var attribute = OrgAttribute.Create(OrgTenantId, TenantKind.Organization, "Department", AttributeType.Text);

        attribute.TenantId.Should().Be(OrgTenantId);
        attribute.Name.Should().Be("Department");
        attribute.Type.Should().Be(AttributeType.Text);
        attribute.Enabled.Should().BeTrue();
        attribute.IsLocked.Should().BeFalse();
        attribute.IsWeightsEnabled.Should().BeFalse();
        attribute.Options.Should().BeEmpty();
    }

    [Fact]
    public void Create_ShouldGenerateSlugFromName()
    {
        var attribute = OrgAttribute.Create(OrgTenantId, TenantKind.Organization, "My Department", AttributeType.Text);

        attribute.Slug.Should().Be("my-department");
    }

    [Fact]
    public void Create_ShouldNormalizeSlugUnderscoresToDashes()
    {
        var attribute = OrgAttribute.Create(OrgTenantId, TenantKind.Organization, "skill_level", AttributeType.Text);

        attribute.Slug.Should().Be("skill-level");
    }

    [Fact]
    public void Create_ForNonOrgTenant_ShouldThrow()
    {
        var act = () => OrgAttribute.Create(OrgTenantId, TenantKind.Solo, "Name", AttributeType.Text);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Create_IdShouldHaveAttrPrefix()
    {
        var attribute = OrgAttribute.Create(OrgTenantId, TenantKind.Organization, "Name", AttributeType.Text);

        attribute.Id.ToString().Should().StartWith("attr_");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Attribute.Update
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Update_ShouldMutateNameAndFlags()
    {
        var attribute = OrgAttribute.Create(OrgTenantId, TenantKind.Organization, "Old Name", AttributeType.Text);

        attribute.Update("New Name", true, true, false);

        attribute.Name.Should().Be("New Name");
        attribute.IsLocked.Should().BeTrue();
        attribute.IsWeightsEnabled.Should().BeTrue();
        attribute.Enabled.Should().BeFalse();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Attribute.AddOption
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void AddOption_ShouldReturnNewOptionAndAddToCollection()
    {
        var attribute = OrgAttribute.Create(OrgTenantId, TenantKind.Organization, "Role", AttributeType.SingleSelect);

        var option = attribute.AddOption("Engineer");

        option.Value.Should().Be("Engineer");
        option.Id.ToString().Should().StartWith("atop_");
        attribute.Options.Should().ContainSingle(o => o.Id == option.Id);
    }

    [Fact]
    public void AddOption_MultipleTimes_ShouldAccumulateOptions()
    {
        var attribute = OrgAttribute.Create(OrgTenantId, TenantKind.Organization, "Role", AttributeType.MultiSelect);

        attribute.AddOption("Engineer");
        attribute.AddOption("Designer");
        attribute.AddOption("Manager");

        attribute.Options.Should().HaveCount(3);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Attribute.UpdateOption
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void UpdateOption_WhenExists_ShouldReturnTrue()
    {
        var attribute = OrgAttribute.Create(OrgTenantId, TenantKind.Organization, "Role", AttributeType.SingleSelect);
        var option = attribute.AddOption("Old Value");

        var result = attribute.UpdateOption(option.Id, "New Value", true, ["child-a"]);

        result.Should().BeTrue();
        attribute.Options.Single(o => o.Id == option.Id).Value.Should().Be("New Value");
    }

    [Fact]
    public void UpdateOption_WhenNotFound_ShouldReturnFalse()
    {
        var attribute = OrgAttribute.Create(OrgTenantId, TenantKind.Organization, "Role", AttributeType.SingleSelect);

        var result = attribute.UpdateOption(AttributeOptionId.NewId(), "Value", false, []);

        result.Should().BeFalse();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Attribute.RemoveOption
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void RemoveOption_WhenExists_ShouldReturnTrueAndReduceCount()
    {
        var attribute = OrgAttribute.Create(OrgTenantId, TenantKind.Organization, "Role", AttributeType.SingleSelect);
        var opt1 = attribute.AddOption("A");
        attribute.AddOption("B");

        var result = attribute.RemoveOption(opt1.Id);

        result.Should().BeTrue();
        attribute.Options.Should().ContainSingle(o => o.Value == "B");
    }

    [Fact]
    public void RemoveOption_WhenNotFound_ShouldReturnFalse()
    {
        var attribute = OrgAttribute.Create(OrgTenantId, TenantKind.Organization, "Role", AttributeType.SingleSelect);

        var result = attribute.RemoveOption(AttributeOptionId.NewId());

        result.Should().BeFalse();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // AttributeAssignment.Create
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void AttributeAssignment_Create_ShouldSetAllFields()
    {
        var membershipId = MembershipId.NewId();
        var attributeId = AttributeId.NewId();

        var assignment = AttributeAssignment.Create(OrgTenantId, membershipId, attributeId, null, "Engineering", null);

        assignment.TenantId.Should().Be(OrgTenantId);
        assignment.MembershipId.Should().Be(membershipId);
        assignment.AttributeId.Should().Be(attributeId);
        assignment.AttributeOptionId.Should().BeNull();
        assignment.Value.Should().Be("Engineering");
        assignment.Weight.Should().BeNull();
        assignment.Id.ToString().Should().StartWith("atas_");
    }
}
