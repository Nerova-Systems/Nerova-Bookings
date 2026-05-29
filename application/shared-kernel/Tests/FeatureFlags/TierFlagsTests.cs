using FluentAssertions;
using SharedKernel.FeatureFlags;
using Xunit;
using FeatureFlagRegistry = SharedKernel.FeatureFlags.FeatureFlags;

namespace SharedKernel.Tests.FeatureFlags;

/// <summary>
///     Registry-level tests for the tier + capability flag taxonomy introduced in f1-tier-flags.
///     These tests exercise the definition registry only (no DB, no evaluator). Evaluator-level
///     parent-gating tests live in Account.Tests.FeatureFlags.FeatureFlagEvaluatorTests.
/// </summary>
public sealed class TierFlagsTests
{
    private static readonly IReadOnlyList<FeatureFlagDefinition> AllFlags = FeatureFlagRegistry.GetAll();

    // Expected full set of tier + capability flag keys.
    private static readonly string[] TierKeys = ["tier-teams", "tier-organizations", "tier-enterprise"];

    private static readonly string[] CapabilityKeys =
    [
        // tier-teams
        "cap-managed-event-types",
        "cap-round-robin",
        "cap-collective",
        // tier-organizations
        "cap-attributes",
        "cap-custom-smtp",
        "cap-org-billing",
        "cap-delegation-credentials",
        "cap-sso-microsoft",
        "cap-sso-google",
        "cap-integration-attribute-sync",
        // tier-enterprise
        "cap-audit-log",
        "cap-workflows",
        "cap-api-keys",
        "cap-impersonation",
        "cap-insights"
    ];

    [Fact]
    public void Registry_ShouldContainAllThreeTierFlags()
    {
        var registeredKeys = AllFlags.Select(f => f.Key).ToHashSet();
        TierKeys.Should().AllSatisfy(key => registeredKeys.Should().Contain(key));
    }

    [Fact]
    public void Registry_ShouldContainAllFifteenCapabilityFlags()
    {
        var registeredKeys = AllFlags.Select(f => f.Key).ToHashSet();
        CapabilityKeys.Should().HaveCount(15);
        CapabilityKeys.Should().AllSatisfy(key => registeredKeys.Should().Contain(key));
    }

    [Fact]
    public void Registry_TotalFlagCount_ShouldBeSeventeenPlusExistingFlags()
    {
        // 7 pre-existing flags + 3 tier + 15 capability + 1 integration + 1 WhatsApp Flows = 27 total.
        AllFlags.Should().HaveCount(27);
    }

    [Theory]
    [InlineData("tier-teams")]
    [InlineData("tier-organizations")]
    [InlineData("tier-enterprise")]
    [InlineData("cap-managed-event-types")]
    [InlineData("cap-round-robin")]
    [InlineData("cap-collective")]
    [InlineData("cap-attributes")]
    [InlineData("cap-custom-smtp")]
    [InlineData("cap-org-billing")]
    [InlineData("cap-delegation-credentials")]
    [InlineData("cap-sso-microsoft")]
    [InlineData("cap-sso-google")]
    [InlineData("cap-integration-attribute-sync")]
    [InlineData("cap-audit-log")]
    [InlineData("cap-workflows")]
    [InlineData("cap-api-keys")]
    [InlineData("cap-impersonation")]
    [InlineData("cap-insights")]
    public void AllNewFlags_ShouldBeTenantAdminManagedFlags(string key)
    {
        var flag = FeatureFlagRegistry.Get(key);
        flag.Should().NotBeNull($"flag '{key}' must be registered");
        flag.Should().BeOfType<TenantAdminManagedFlag>($"flag '{key}' must be a TenantAdminManagedFlag");
    }

    [Theory]
    [InlineData("tier-teams")]
    [InlineData("tier-organizations")]
    [InlineData("tier-enterprise")]
    [InlineData("cap-managed-event-types")]
    [InlineData("cap-round-robin")]
    [InlineData("cap-collective")]
    [InlineData("cap-attributes")]
    [InlineData("cap-custom-smtp")]
    [InlineData("cap-org-billing")]
    [InlineData("cap-delegation-credentials")]
    [InlineData("cap-sso-microsoft")]
    [InlineData("cap-sso-google")]
    [InlineData("cap-integration-attribute-sync")]
    [InlineData("cap-audit-log")]
    [InlineData("cap-workflows")]
    [InlineData("cap-api-keys")]
    [InlineData("cap-impersonation")]
    [InlineData("cap-insights")]
    public void AllNewFlags_ShouldHaveKillSwitchEnabled_DefaultingOff(string key)
    {
        // IsKillSwitchEnabled=true → reconciler creates base row inactive → default OFF per tenant.
        var flag = FeatureFlagRegistry.Get(key);
        flag!.IsKillSwitchEnabled.Should().BeTrue($"flag '{key}' must default OFF (IsKillSwitchEnabled=true)");
    }

    [Theory]
    [InlineData("tier-teams")]
    [InlineData("tier-organizations")]
    [InlineData("tier-enterprise")]
    [InlineData("cap-managed-event-types")]
    [InlineData("cap-round-robin")]
    [InlineData("cap-collective")]
    [InlineData("cap-attributes")]
    [InlineData("cap-custom-smtp")]
    [InlineData("cap-org-billing")]
    [InlineData("cap-delegation-credentials")]
    [InlineData("cap-sso-microsoft")]
    [InlineData("cap-sso-google")]
    [InlineData("cap-integration-attribute-sync")]
    [InlineData("cap-audit-log")]
    [InlineData("cap-workflows")]
    [InlineData("cap-api-keys")]
    [InlineData("cap-impersonation")]
    [InlineData("cap-insights")]
    public void AllNewFlags_ShouldBeTenantScopedAndSystemAdminManaged(string key)
    {
        var flag = FeatureFlagRegistry.Get(key);
        flag!.Scope.Should().Be(FeatureFlagScope.Tenant, $"flag '{key}' must be Tenant-scoped");
        flag.AdminLevel.Should().Be(FeatureFlagAdminLevel.SystemAdmin, $"flag '{key}' must be SystemAdmin-managed");
    }

    [Fact]
    public void TierTeams_ShouldHaveNoParentDependency()
    {
        FeatureFlagRegistry.Get("tier-teams")!.ParentDependency.Should().BeNull();
    }

    [Fact]
    public void TierOrganizations_ShouldDependOnTierTeams()
    {
        FeatureFlagRegistry.Get("tier-organizations")!.ParentDependency.Should().Be("tier-teams");
    }

    [Fact]
    public void TierEnterprise_ShouldDependOnTierOrganizations()
    {
        FeatureFlagRegistry.Get("tier-enterprise")!.ParentDependency.Should().Be("tier-organizations");
    }

    [Theory]
    [InlineData("cap-managed-event-types")]
    [InlineData("cap-round-robin")]
    [InlineData("cap-collective")]
    public void CapabilityFlagsOnTierTeams_ShouldDependOnTierTeams(string key)
    {
        FeatureFlagRegistry.Get(key)!.ParentDependency.Should().Be("tier-teams", $"flag '{key}' must be gated on tier-teams");
    }

    [Theory]
    [InlineData("cap-attributes")]
    [InlineData("cap-custom-smtp")]
    [InlineData("cap-org-billing")]
    [InlineData("cap-delegation-credentials")]
    [InlineData("cap-sso-microsoft")]
    [InlineData("cap-sso-google")]
    [InlineData("cap-integration-attribute-sync")]
    public void CapabilityFlagsOnTierOrganizations_ShouldDependOnTierOrganizations(string key)
    {
        FeatureFlagRegistry.Get(key)!.ParentDependency.Should().Be("tier-organizations", $"flag '{key}' must be gated on tier-organizations");
    }

    [Theory]
    [InlineData("cap-audit-log")]
    [InlineData("cap-workflows")]
    [InlineData("cap-api-keys")]
    [InlineData("cap-impersonation")]
    [InlineData("cap-insights")]
    public void CapabilityFlagsOnTierEnterprise_ShouldDependOnTierEnterprise(string key)
    {
        FeatureFlagRegistry.Get(key)!.ParentDependency.Should().Be("tier-enterprise", $"flag '{key}' must be gated on tier-enterprise");
    }

    [Fact]
    public void RegistryValidation_ShouldNotThrow_WithMultiLevelTierChain()
    {
        // FeatureFlags static constructor calls ValidateFlags(). Accessing any member forces
        // it to run. If the multi-level chain (enterprise → organizations → teams) were invalid
        // the static constructor would have thrown before any test ran. This test makes the
        // intent explicit by calling GetAll() and asserting it completes without error.
        Func<FeatureFlagDefinition[]> act = FeatureFlagRegistry.GetAll;
        act.Should().NotThrow("the tier ParentDependency chain is valid and must not be rejected by the validator");
    }
}
