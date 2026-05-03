using OpenTelemetry;
using SharedKernel.Configuration;

namespace SharedKernel.Telemetry;

// Stamps deployment provenance (commit SHA + GitHub Actions run ID, baked into the assembly via
// AssemblyMetadata attributes) onto every span on start. Activity tags surface as customDimensions
// on AppRequests / AppDependencies in Application Insights -- arbitrary OpenTelemetry resource attributes
// are stored in an internal hash table and aren't queryable, so resource attributes don't work for
// this. The values are read once at startup; this class does no per-request work beyond two SetTag
// calls.
public sealed class DeploymentTagsProcessor : BaseProcessor<Activity>
{
    private static readonly KeyValuePair<string, object?>[] Tags = BuildTags();

    public override void OnStart(Activity activity)
    {
        foreach (var (key, value) in Tags)
        {
            activity.SetTag(key, value);
        }
    }

    private static KeyValuePair<string, object?>[] BuildTags()
    {
        var tags = new List<KeyValuePair<string, object?>>(2);
        if (SharedInfrastructureConfiguration.DeploymentCommitHash is not null)
        {
            tags.Add(new KeyValuePair<string, object?>("deployment.commit_hash", SharedInfrastructureConfiguration.DeploymentCommitHash));
        }

        if (SharedInfrastructureConfiguration.DeploymentGithubActionId is not null)
        {
            tags.Add(new KeyValuePair<string, object?>("deployment.github_action_id", SharedInfrastructureConfiguration.DeploymentGithubActionId));
        }

        return tags.ToArray();
    }
}
