using System.Text.RegularExpressions;

namespace SharedKernel.FeatureFlags;

// Registry mechanism backing the developer-facing definitions in FeatureFlags.cs. Reflects over
// every `public static readonly FeatureFlagDefinition` field declared there, validates them at
// startup, and exposes lookup helpers. Adding a flag does not require touching this file.
public static partial class FeatureFlags
{
    private static readonly FeatureFlagDefinition[] AllFeatureFlags =
        typeof(FeatureFlags)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsInitOnly && f.FieldType == typeof(FeatureFlagDefinition))
            .Select(f => (FeatureFlagDefinition)f.GetValue(null)!)
            .ToArray();

    private static readonly Regex FeatureFlagKeyPattern =
        new("^[a-z0-9]+(-[a-z0-9]+)*$", RegexOptions.Compiled);

    static FeatureFlags()
    {
        ValidateFlags();
    }

    public static FeatureFlagDefinition[] GetAll()
    {
        return AllFeatureFlags;
    }

    public static FeatureFlagDefinition? Get(string key)
    {
        return AllFeatureFlags.FirstOrDefault(f => f.Key == key);
    }

    // Keys are surfaced verbatim in URLs, JWT claim payloads, telemetry property names, frontend
    // route params, and the configurable-feature-flag toggle UI. Lowercase kebab-case keeps every
    // consumer's case-handling simple and removes ambiguity for the comma-separated `feature_flags`
    // JWT claim. Internal so SharedKernel.Tests can pin the pattern.
    internal static bool IsValidKey(string key)
    {
        return FeatureFlagKeyPattern.IsMatch(key);
    }

    // Subtype hierarchy in FeatureFlagDefinition.cs enforces all cross-property invariants at compile
    // time. This method validates what subtypes cannot: key format, parent-dependency existence +
    // cycle-freedom (no circular chains; any finite depth is allowed to support the tier→capability
    // hierarchy: cap-audit-log → tier-enterprise → tier-organizations → tier-teams), and TelemetryName
    // uniqueness (telemetry property names must remain stable forever).
    //
    // The previous "only one level of dependency" restriction was relaxed when tier flags were
    // introduced (f1-tier-flags). Tier flags form a 2-level chain (tier-enterprise depends on
    // tier-organizations which depends on tier-teams), and capability flags add a 3rd level on top.
    // Cycle detection replaces the fixed-depth guard.
    private static void ValidateFlags()
    {
        var featureFlagsByKey = AllFeatureFlags.ToDictionary(f => f.Key);
        var telemetryNamesSeen = new Dictionary<string, string>();

        foreach (var featureFlag in AllFeatureFlags)
        {
            if (featureFlag.Key.Length > 50)
            {
                throw new InvalidOperationException($"Feature flag key '{featureFlag.Key}' exceeds 50 characters.");
            }

            if (!IsValidKey(featureFlag.Key))
            {
                throw new InvalidOperationException($"Feature flag key '{featureFlag.Key}' must be lowercase kebab-case (a-z, 0-9, hyphen). No leading/trailing hyphens, no consecutive hyphens, no other characters.");
            }

            if (featureFlag.ParentDependency is not null)
            {
                if (!featureFlagsByKey.ContainsKey(featureFlag.ParentDependency))
                {
                    throw new InvalidOperationException($"Feature flag '{featureFlag.Key}' references non-existent parent dependency '{featureFlag.ParentDependency}'.");
                }
            }

            if (featureFlag.TelemetryName is not null)
            {
                if (telemetryNamesSeen.TryGetValue(featureFlag.TelemetryName, out var existingKey))
                {
                    throw new InvalidOperationException($"Feature flag '{featureFlag.Key}' uses TelemetryName '{featureFlag.TelemetryName}' which is already used by '{existingKey}'. TelemetryName must be unique across all flags so historical telemetry stays unambiguous.");
                }

                telemetryNamesSeen[featureFlag.TelemetryName] = featureFlag.Key;
            }
        }

        // Cycle detection: walk every flag's ancestry chain. A cycle means we'd loop forever during
        // evaluation. The previous depth guard caught cycles implicitly (depth > 1 = error); the
        // new check is explicit so the error message names the cycle.
        foreach (var featureFlag in AllFeatureFlags)
        {
            var visited = new HashSet<string>();
            var current = featureFlag;
            while (current.ParentDependency is not null)
            {
                if (!visited.Add(current.Key))
                {
                    throw new InvalidOperationException($"Feature flag '{featureFlag.Key}' is part of a circular parent-dependency chain. Circular chains cannot be evaluated.");
                }

                if (!featureFlagsByKey.TryGetValue(current.ParentDependency, out current!)) break;
            }
        }
    }
}
