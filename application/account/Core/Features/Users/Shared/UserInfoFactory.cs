using Account.Features.FeatureFlags.Shared;
using Account.Features.OrgProfiles.Domain;
using Account.Features.Subscriptions.Domain;
using Account.Features.Tenants.Domain;
using Account.Features.Users.Domain;
using SharedKernel.Authentication;
using SharedKernel.Authentication.TokenGeneration;
using SharedKernel.Cqrs;
using SharedKernel.Domain;

namespace Account.Features.Users.Shared;

/// <summary>
///     Factory for creating UserInfo instances with tenant information.
///     Centralizes the logic for creating UserInfo to follow SRP and avoid duplication.
/// </summary>
public sealed class UserInfoFactory(
    ITenantRepository tenantRepository,
    ISubscriptionRepository subscriptionRepository,
    FeatureFlagEvaluator featureFlagEvaluator,
    PlanBasedFeatureFlagEvaluator planBasedFeatureFlagEvaluator
)
{
    /// <summary>
    ///     Creates a UserInfo instance from a User entity, including tenant name.
    ///     Returns a failure result if the tenant has been soft-deleted.
    ///     <para>
    ///         The optional <paramref name="activeTeamId" />, <paramref name="activeOrgId" />, and
    ///         <paramref name="activeOrgProfileId" /> parameters are forwarded into the resulting
    ///         <see cref="UserInfo" /> and subsequently encoded as JWT claims by
    ///         <see cref="AccessTokenGenerator" />. Pass non-null values only when issuing tokens
    ///         for a team- or org-scoped session (e.g., after a scope switch).
    ///     </para>
    /// </summary>
    public async Task<Result<UserInfo>> CreateUserInfoAsync(
        User user,
        SessionId? sessionId,
        CancellationToken cancellationToken,
        TenantId? activeTeamId = null,
        TenantId? activeOrgId = null,
        OrgProfileId? activeOrgProfileId = null)
    {
        var tenant = await tenantRepository.GetByIdAsync(user.TenantId, cancellationToken);
        if (tenant is null) return Result<UserInfo>.BadRequest("Tenant has been deleted.");

        var subscription = await subscriptionRepository.GetByTenantIdUnfilteredAsync(user.TenantId, cancellationToken)
                           ?? throw new InvalidOperationException($"Subscription not found for tenant '{user.TenantId}'.");

        await planBasedFeatureFlagEvaluator.EvaluatePlanFlagsForTenantAsync(tenant.Id, subscription.Plan, cancellationToken);

        var enabledFlags = await featureFlagEvaluator.EvaluateAsync(
            tenant.Id, user.Id, tenant.RolloutBucket, user.RolloutBucket, tenant.AbInclusionPin, user.AbInclusionPin, cancellationToken
        );

        return new UserInfo
        {
            IsAuthenticated = true,
            Id = user.Id,
            TenantId = user.TenantId,
            SessionId = sessionId,
            Role = user.Role.ToString(),
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Title = user.Title,
            AvatarUrl = user.Avatar.Url,
            TenantName = tenant.Name,
            TenantLogoUrl = tenant.Logo.Url,
            SubscriptionPlan = subscription.Plan.ToString(),
            Locale = user.Locale,
            IsInternalUser = user.IsInternalUser,
            FeatureFlags = new HashSet<string>(enabledFlags),
            TenantRolloutBucket = tenant.RolloutBucket,
            UserRolloutBucket = user.RolloutBucket,
            ActiveTeamId = activeTeamId,
            ActiveOrgId = activeOrgId,
            ActiveOrgProfileId = activeOrgProfileId?.Value
        };
    }
}
