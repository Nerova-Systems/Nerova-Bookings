using SharedKernel.Cqrs;
using SharedKernel.ExecutionContext;

namespace Main.Features.Insights.Shared;

/// <summary>
///     Translates the current <see cref="IExecutionContext" /> into an <see cref="InsightsScope" /> that is
///     applied as WHERE filters on every insights query.
/// </summary>
public sealed class InsightsScopeResolver(IExecutionContext executionContext)
{
    /// <summary>
    ///     Returns <see langword="null" /> when the request is unauthenticated or missing tenant context.
    /// </summary>
    public InsightsScope? TryResolve()
    {
        var userInfo = executionContext.UserInfo;
        var tenantId = userInfo.TenantId;
        var userId = userInfo.Id;

        if (tenantId is null || userId is null) return null;

        // ActiveTeamId is set from the JWT active_team_id claim when the user switches to a team.
        return new InsightsScope(tenantId, executionContext.ActiveTeamId, userId);
    }

    /// <summary>
    ///     Returns a <see cref="Result{InsightsScope}" /> carrying Unauthorized or Forbidden on failure,
    ///     or the resolved scope on success. Handlers call this instead of duplicating the 3-line preamble.
    /// </summary>
    public Result<InsightsScope> ResolveWithAccess()
    {
        var scope = TryResolve();
        if (scope is null) return Result<InsightsScope>.Unauthorized(InsightsAuthorization.InsightsUnauthorizedMessage);
        if (!HasInsightsAccess()) return Result<InsightsScope>.Forbidden(InsightsAuthorization.InsightsFeatureDisabledMessage);
        return scope;
    }

    /// <summary>
    ///     Returns <see langword="true" /> when the current user has the <c>cap-insights</c> feature flag enabled.
    /// </summary>
    public bool HasInsightsAccess()
    {
        return executionContext.UserInfo.IsFeatureFlagEnabled(InsightsAuthorization.InsightsFeatureFlagKey);
    }
}
