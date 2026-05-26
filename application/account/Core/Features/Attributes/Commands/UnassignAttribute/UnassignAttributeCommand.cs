using Account.Features.Attributes.Domain;
using Account.Features.AuditLog.Domain;
using Account.Features.Memberships.Domain;
using Account.Features.Permissions.Domain;
using Account.Features.Permissions.Pipeline;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Http;
using SharedKernel.AuditLog;
using SharedKernel.Cqrs;
using SharedKernel.Domain;
using SharedKernel.ExecutionContext;
using SharedKernel.Telemetry;
using FeatureFlagDefinitions = SharedKernel.FeatureFlags.FeatureFlags;

namespace Account.Features.Attributes.Commands.UnassignAttribute;

[PublicAPI]
[RequirePermission(PermissionResource.Attribute, PermissionAction.Delete, PermissionScope.Organization)]
public sealed record UnassignAttributeCommand : ICommand, IRequest<Result>
{
    public required MembershipId MembershipId { get; init; }

    public required AttributeId AttributeId { get; init; }

    /// <summary>
    ///     When specified, removes only the assignment for this specific option.
    ///     When <see langword="null" />, removes all assignments for (membership, attribute).
    /// </summary>
    public AttributeOptionId? OptionId { get; init; }
}

public sealed class UnassignAttributeHandler(
    IAttributeAssignmentRepository assignmentRepository,
    IAuditLogEmitter auditLogEmitter,
    IHttpContextAccessor httpContextAccessor,
    ITelemetryEventsCollector events,
    IExecutionContext executionContext
) : IRequestHandler<UnassignAttributeCommand, Result>
{
    public async Task<Result> Handle(UnassignAttributeCommand command, CancellationToken cancellationToken)
    {
        if (!executionContext.UserInfo.IsFeatureFlagEnabled(FeatureFlagDefinitions.CapAttributes.Key))
        {
            return Result.Forbidden("The attributes feature is not enabled for this organization.");
        }

        var orgId = executionContext.ActiveOrgId!;

        if (command.OptionId is not null)
        {
            // Remove specific (membership, attribute, option) assignment.
            var assignment = await assignmentRepository.GetByMembershipAttributeOptionAsync(
                command.MembershipId, command.AttributeId, command.OptionId, cancellationToken
            );

            if (assignment is null)
            {
                return Result.NotFound("Assignment not found.");
            }

            if (assignment.TenantId != orgId)
            {
                return Result.Forbidden("You do not have access to this assignment.");
            }

            assignmentRepository.Remove(assignment);

            await EmitAuditAsync(assignment.Id.ToString(), orgId, cancellationToken);
            events.CollectEvent(new AttributeUnassigned(command.MembershipId, command.AttributeId));
        }
        else
        {
            // Remove all assignments for (membership, attribute).
            var assignments = await assignmentRepository.GetByMembershipAsync(command.MembershipId, cancellationToken);
            var toRemove = assignments
                .Where(a => a.AttributeId == command.AttributeId && a.TenantId == orgId)
                .ToList();

            if (toRemove.Count == 0)
            {
                return Result.NotFound("No assignments found for this membership and attribute.");
            }

            foreach (var assignment in toRemove)
            {
                assignmentRepository.Remove(assignment);
                events.CollectEvent(new AttributeUnassigned(command.MembershipId, command.AttributeId));
            }

            await EmitAuditAsync(command.AttributeId.ToString(), orgId, cancellationToken);
        }

        return Result.Success();
    }

    private Task EmitAuditAsync(string resourceId, TenantId orgId, CancellationToken cancellationToken)
    {
        return auditLogEmitter.EmitAsync(new AuditLogEvent(
                orgId,
                executionContext.UserInfo.Id!,
                executionContext.UserInfo.Email ?? string.Empty,
                nameof(AuditResource.Attribute),
                nameof(AuditAction.Deleted),
                resourceId,
                IpAddress: httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString(),
                UserAgent: httpContextAccessor.HttpContext?.Request.Headers.UserAgent.ToString()
            ), cancellationToken
        );
    }
}
