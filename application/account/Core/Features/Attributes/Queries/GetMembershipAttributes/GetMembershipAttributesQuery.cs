using Account.Features.Attributes.Domain;
using Account.Features.Memberships.Domain;
using Account.Features.Permissions.Domain;
using Account.Features.Permissions.Pipeline;
using JetBrains.Annotations;
using SharedKernel.Cqrs;
using SharedKernel.ExecutionContext;
using FeatureFlagDefinitions = SharedKernel.FeatureFlags.FeatureFlags;

namespace Account.Features.Attributes.Queries.GetMembershipAttributes;

[PublicAPI]
[RequirePermission(PermissionResource.Attribute, PermissionAction.Read, PermissionScope.Organization)]
public sealed record GetMembershipAttributesQuery : IRequest<Result<AttributeAssignmentResponse[]>>
{
    public MembershipId MembershipId { get; init; } = default!;
}

public sealed class GetMembershipAttributesHandler(
    IAttributeAssignmentRepository assignmentRepository,
    IMembershipRepository membershipRepository,
    IExecutionContext executionContext
) : IRequestHandler<GetMembershipAttributesQuery, Result<AttributeAssignmentResponse[]>>
{
    public async Task<Result<AttributeAssignmentResponse[]>> Handle(
        GetMembershipAttributesQuery query,
        CancellationToken cancellationToken)
    {
        if (!executionContext.UserInfo.IsFeatureFlagEnabled(FeatureFlagDefinitions.CapAttributes.Key))
            return Result<AttributeAssignmentResponse[]>.Forbidden("The attributes feature is not enabled for this organization.");

        var orgId = executionContext.ActiveOrgId!;

        // Verify the membership belongs to this org.
        var membership = await membershipRepository.GetByIdAsync(query.MembershipId, cancellationToken);
        if (membership is null)
            return Result<AttributeAssignmentResponse[]>.NotFound($"Membership '{query.MembershipId}' not found.");

        if (membership.TenantId != orgId)
            return Result<AttributeAssignmentResponse[]>.Forbidden("You do not have access to this membership.");

        var assignments = await assignmentRepository.GetByMembershipAsync(query.MembershipId, cancellationToken);

        return assignments.Select(a => a.ToResponse()).ToArray();
    }
}
