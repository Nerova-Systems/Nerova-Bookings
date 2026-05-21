using Account.Features.FeatureFlags.Domain;
using SharedKernel.Domain;
using SharedKernel.FeatureFlags;

namespace Account.Features.FeatureFlags.Shared;

/// <summary>
///     Resolves the enabled feature flags for a (tenant, user) context. Precedence runs base-row-active
///     and parent-dependency as gates, then manual override > AB inclusion pin > rollout bucket > default
///     off. Pins are unconditional: AlwaysOn forces inclusion even at 0% rollout, NeverOn forces exclusion
///     even at 100%. Plan-gated flags participate as manual overrides because the Paystack pipeline writes
///     them as Source=Plan tenant rows. The four BackOffice query mirrors apply the same ordering.
/// </summary>
public sealed class FeatureFlagEvaluator(IFeatureFlagRepository featureFlagRepository)
{
    // The definitions source defaults to the reflected registry but is overridable for tests that need
    // to exercise specific topologies (e.g., parent-dependency gating) without contributing test-only
    // flags to the production registry.
    public Func<FeatureFlagDefinition[]> DefinitionsProvider { get; init; } = SharedKernel.FeatureFlags.FeatureFlags.GetAll;

    public async Task<IReadOnlyList<string>> EvaluateAsync(
        TenantId tenantId,
        UserId userId,
        int tenantRolloutBucket,
        int? userRolloutBucket,
        AbInclusionPin? tenantAbInclusionPin,
        AbInclusionPin? userAbInclusionPin,
        CancellationToken cancellationToken
    )
    {
        var allRows = await featureFlagRepository.GetAllRelevantRowsAsync(tenantId, userId, cancellationToken);
        var enabledFeatureFlags = new List<string>();

        var definitions = DefinitionsProvider();

        // Sort feature flags so parents are evaluated before children
        var sorted = SortByParentDependencyFirst(definitions);

        var enabledFeatureFlagSet = new HashSet<string>();

        foreach (var definition in sorted)
        {
            if (definition.Scope == FeatureFlagScope.System) continue;

            var baseRow = allRows.FirstOrDefault(f => f.FlagKey == definition.Key && f.TenantId is null && f.UserId is null);
            if (baseRow is null) continue;

            if (!baseRow.IsActive) continue;

            if (definition.ParentDependency is not null && !enabledFeatureFlagSet.Contains(definition.ParentDependency)) continue;

            var isEnabled = definition.Scope switch
            {
                FeatureFlagScope.Tenant => EvaluateTenantScope(definition, baseRow, allRows, tenantId, tenantRolloutBucket, tenantAbInclusionPin),
                FeatureFlagScope.User => EvaluateUserScope(definition, baseRow, allRows, tenantId, userId, userRolloutBucket, userAbInclusionPin),
                _ => false
            };

            if (!isEnabled) continue;

            enabledFeatureFlagSet.Add(definition.Key);
            enabledFeatureFlags.Add(definition.Key);
        }

        return enabledFeatureFlags;
    }

    private static bool EvaluateTenantScope(FeatureFlagDefinition definition, FeatureFlag baseRow, FeatureFlag[] allRows, TenantId tenantId, int tenantRolloutBucket, AbInclusionPin? tenantAbInclusionPin)
    {
        var tenantOverride = allRows.FirstOrDefault(f => f.FlagKey == definition.Key && f.TenantId == tenantId && f.UserId is null);
        if (tenantOverride is not null) return tenantOverride.IsActive;

        if (!definition.IsAbTestEligible) return false;

        // Precedence (matches .claude/rules/backend/backend.md): manual override (above) > pin > rollout.
        // Pins are unconditional: AlwaysOn includes the tenant even when rollout is at 0%/unset, and
        // NeverOn excludes the tenant even at 100% rollout. This is what the project-level precedence
        // story documents and what admins expect from a pin labelled "always on".
        if (tenantAbInclusionPin is AbInclusionPin.AlwaysOn) return true;
        if (tenantAbInclusionPin is AbInclusionPin.NeverOn) return false;

        if (baseRow.BucketStart is null || baseRow.BucketEnd is null) return false;

        return RolloutBucketHasher.IsInRolloutBucketRange(tenantRolloutBucket, baseRow.BucketStart.Value, baseRow.BucketEnd.Value);
    }

    private static bool EvaluateUserScope(FeatureFlagDefinition definition, FeatureFlag baseRow, FeatureFlag[] allRows, TenantId tenantId, UserId userId, int? userRolloutBucket, AbInclusionPin? userAbInclusionPin)
    {
        var userOverride = allRows.FirstOrDefault(f => f.FlagKey == definition.Key && f.TenantId == tenantId && f.UserId == userId);
        if (userOverride is not null) return userOverride.IsActive;

        if (!definition.IsAbTestEligible) return false;

        if (userAbInclusionPin is AbInclusionPin.AlwaysOn) return true;
        if (userAbInclusionPin is AbInclusionPin.NeverOn) return false;

        if (baseRow.BucketStart is null || baseRow.BucketEnd is null) return false;
        if (userRolloutBucket is null) return false;

        return RolloutBucketHasher.IsInRolloutBucketRange(userRolloutBucket.Value, baseRow.BucketStart.Value, baseRow.BucketEnd.Value);
    }

    // Topological sort: guarantees every flag's parent is in the sorted output before the flag
    // itself, regardless of chain depth. Previously a 2-pass sort sufficed because only one level
    // of dependency existed; the tier-flag chain (tier-enterprise → tier-organizations → tier-teams)
    // and the cap-flag chain above it (cap-* → tier-enterprise) require a proper graph walk.
    //
    // Uses a depth-first post-order traversal (standard DFS toposort). The cycle guard in
    // FeatureFlags.ValidateFlags runs at startup, so no cycle detection is needed here.
    private static FeatureFlagDefinition[] SortByParentDependencyFirst(FeatureFlagDefinition[] definitions)
    {
        var byKey = definitions.ToDictionary(d => d.Key);
        var result = new List<FeatureFlagDefinition>(definitions.Length);
        var visited = new HashSet<string>(definitions.Length);

        void Visit(FeatureFlagDefinition definition)
        {
            if (!visited.Add(definition.Key)) return;

            if (definition.ParentDependency is not null && byKey.TryGetValue(definition.ParentDependency, out var parent))
            {
                Visit(parent);
            }

            result.Add(definition);
        }

        foreach (var definition in definitions)
        {
            Visit(definition);
        }

        return result.ToArray();
    }
}
