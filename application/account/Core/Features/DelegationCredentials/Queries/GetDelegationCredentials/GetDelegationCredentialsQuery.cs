using Account.Features.DelegationCredentials.Domain;
using Account.Features.Permissions.Domain;
using Account.Features.Permissions.Pipeline;
using JetBrains.Annotations;
using SharedKernel.Cqrs;
using SharedKernel.DelegationCredentials;
using SharedKernel.ExecutionContext;
using FeatureFlagDefinitions = SharedKernel.FeatureFlags.FeatureFlags;

namespace Account.Features.DelegationCredentials.Queries.GetDelegationCredentials;

/// <summary>
///     Returns all delegation credentials for the active organization.
///     Key blobs are never included in the response.
///     Requires <c>OrgSettings.Read</c> permission at organization scope.
/// </summary>
[PublicAPI]
[RequirePermission(PermissionResource.OrgSettings, PermissionAction.Read, PermissionScope.Organization)]
public sealed record GetDelegationCredentialsQuery : IRequest<Result<DelegationCredentialResponse[]>>;

[PublicAPI]
public sealed record DelegationCredentialResponse(
    string Id,
    WorkspacePlatform Platform,
    string Domain,
    DelegationCredentialStatus Status,
    DateTimeOffset? LastTestedAt,
    CredentialTestStatus? LastTestStatus,
    string? LastTestError,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ModifiedAt);

public sealed class GetDelegationCredentialsHandler(
    IDelegationCredentialRepository credentialRepository,
    IExecutionContext executionContext) : IRequestHandler<GetDelegationCredentialsQuery, Result<DelegationCredentialResponse[]>>
{
    public async Task<Result<DelegationCredentialResponse[]>> Handle(
        GetDelegationCredentialsQuery query,
        CancellationToken cancellationToken)
    {
        if (!executionContext.UserInfo.IsFeatureFlagEnabled(FeatureFlagDefinitions.CapDelegationCredentials.Key))
            return Result<DelegationCredentialResponse[]>.Forbidden("The delegation credentials feature is not enabled for this organization.");

        var orgId = executionContext.ActiveOrgId!;
        var credentials = await credentialRepository.GetAllByOrgIdAsync(orgId, cancellationToken);

        var responses = credentials.Select(c => new DelegationCredentialResponse(
            Id: c.Id.ToString(),
            Platform: c.Platform,
            Domain: c.Domain,
            Status: c.Status,
            LastTestedAt: c.LastTestedAt,
            LastTestStatus: c.LastTestStatus,
            LastTestError: c.LastTestError,
            CreatedAt: c.CreatedAt,
            ModifiedAt: c.ModifiedAt)).ToArray();

        return responses;
    }
}
