using System.Text.Json;
using Account.Features.AttributeSync.Domain;
using Account.Features.AttributeSync.Infrastructure;
using Account.Features.Memberships.Domain;
using Account.Features.Permissions.Domain;
using Account.Features.Permissions.Pipeline;
using JetBrains.Annotations;
using SharedKernel.Cqrs;
using SharedKernel.Domain;
using SharedKernel.ExecutionContext;
using FeatureFlagDefinitions = SharedKernel.FeatureFlags.FeatureFlags;

namespace Account.Features.AttributeSync.Commands.ApplyAttributeSync;

/// <summary>
///     Admin-triggered manual sync for a specific membership.
///     Useful for debugging or back-filling sync when rules were added after the last SSO login.
/// </summary>
[PublicAPI]
[RequirePermission(PermissionResource.OrgSettings, PermissionAction.Manage, PermissionScope.Organization)]
public sealed record ApplyAttributeSyncCommand : ICommand, IRequest<Result>
{
    public MembershipId MembershipId { get; init; } = default!;

    /// <summary>Claims to apply. Keys are claim types; values are JSON-serialised claim values.</summary>
    public IReadOnlyDictionary<string, JsonElement> Claims { get; init; } = new Dictionary<string, JsonElement>();
}

public sealed class ApplyAttributeSyncHandler(
    AttributeSyncService syncService,
    IMembershipRepository membershipRepository,
    IExecutionContext executionContext
) : IRequestHandler<ApplyAttributeSyncCommand, Result>
{
    public async Task<Result> Handle(ApplyAttributeSyncCommand command, CancellationToken cancellationToken)
    {
        if (!executionContext.UserInfo.IsFeatureFlagEnabled(FeatureFlagDefinitions.CapIntegrationAttributeSync.Key))
            return Result.Forbidden("The IdP attribute sync feature is not enabled for this organization.");

        var orgId = executionContext.ActiveOrgId!;

        var membership = await membershipRepository.GetByIdAsync(command.MembershipId, cancellationToken);
        if (membership is null)
            return Result.NotFound($"Membership '{command.MembershipId}' not found.");

        if (membership.TenantId != orgId)
            return Result.Forbidden("You do not have access to this membership.");

        await syncService.ApplyAsync(
            command.MembershipId,
            orgId,
            SyncSource.AdminManual,
            command.Claims,
            cancellationToken);

        return Result.Success();
    }
}
