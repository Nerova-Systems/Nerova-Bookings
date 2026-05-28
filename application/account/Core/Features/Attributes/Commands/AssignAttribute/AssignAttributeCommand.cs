using Account.Features.Attributes.Domain;
using Account.Features.AuditLog.Domain;
using Account.Features.Memberships.Domain;
using Account.Features.Permissions.Domain;
using Account.Features.Permissions.Pipeline;
using FluentValidation;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Http;
using SharedKernel.AuditLog;
using SharedKernel.Cqrs;
using SharedKernel.ExecutionContext;
using SharedKernel.Telemetry;
using FeatureFlagDefinitions = SharedKernel.FeatureFlags.FeatureFlags;

namespace Account.Features.Attributes.Commands.AssignAttribute;

[PublicAPI]
[RequirePermission(PermissionResource.Attribute, PermissionAction.Update, PermissionScope.Organization)]
public sealed record AssignAttributeCommand : ICommand, IRequest<Result<AttributeAssignmentResponse>>
{
    [JsonIgnore] // Removes this property from the API contract
    public MembershipId MembershipId { get; init; } = null!;

    [JsonIgnore] // Removes this property from the API contract
    public AttributeId AttributeId { get; init; } = null!;

    /// <summary>
    ///     Option ID for <see cref="AttributeType.SingleSelect" /> and
    ///     <see cref="AttributeType.MultiSelect" /> attribute types.
    /// </summary>
    public AttributeOptionId? OptionId { get; init; }

    /// <summary>
    ///     Free-text or numeric value for <see cref="AttributeType.Text" /> and
    ///     <see cref="AttributeType.Number" /> attribute types.
    /// </summary>
    public string? Value { get; init; }

    public int? Weight { get; init; }
}

public sealed class AssignAttributeValidator : AbstractValidator<AssignAttributeCommand>
{
    public AssignAttributeValidator()
    {
        RuleFor(x => x.Value)
            .MaximumLength(1000)
            .When(x => x.Value is not null);
    }
}

public sealed class AssignAttributeHandler(
    IAttributeRepository attributeRepository,
    IAttributeAssignmentRepository assignmentRepository,
    IMembershipRepository membershipRepository,
    IAuditLogEmitter auditLogEmitter,
    IHttpContextAccessor httpContextAccessor,
    ITelemetryEventsCollector events,
    IExecutionContext executionContext
) : IRequestHandler<AssignAttributeCommand, Result<AttributeAssignmentResponse>>
{
    public async Task<Result<AttributeAssignmentResponse>> Handle(AssignAttributeCommand command, CancellationToken cancellationToken)
    {
        if (!executionContext.UserInfo.IsFeatureFlagEnabled(FeatureFlagDefinitions.CapAttributes.Key))
        {
            return Result<AttributeAssignmentResponse>.Forbidden("The attributes feature is not enabled for this organization.");
        }

        var orgId = executionContext.ActiveOrgId!;

        // Validate attribute belongs to this org.
        var attribute = await attributeRepository.GetByIdUnfilteredAsync(command.AttributeId, cancellationToken);
        if (attribute is null)
        {
            return Result<AttributeAssignmentResponse>.NotFound($"Attribute '{command.AttributeId}' not found.");
        }

        if (attribute.TenantId != orgId)
        {
            return Result<AttributeAssignmentResponse>.Forbidden("You do not have access to this attribute.");
        }

        // Validate membership belongs to this org.
        var membership = await membershipRepository.GetByIdAsync(command.MembershipId, cancellationToken);
        if (membership is null)
        {
            return Result<AttributeAssignmentResponse>.NotFound($"Membership '{command.MembershipId}' not found.");
        }

        if (membership.TenantId != orgId)
        {
            return Result<AttributeAssignmentResponse>.Forbidden("You do not have access to this membership.");
        }

        // Validate option exists on this attribute (for select types).
        if (command.OptionId is not null && attribute.Options.All(o => o.Id != command.OptionId))
        {
            return Result<AttributeAssignmentResponse>.BadRequest($"Option '{command.OptionId}' does not belong to attribute '{command.AttributeId}'.");
        }

        // Type-specific cross-validation.
        switch (attribute.Type)
        {
            case AttributeType.Text or AttributeType.Number when command.OptionId is not null:
                return Result<AttributeAssignmentResponse>.BadRequest("Text and Number attributes do not accept an option ID.");
            case AttributeType.SingleSelect or AttributeType.MultiSelect when command.Value is not null:
                return Result<AttributeAssignmentResponse>.BadRequest("Select attributes do not accept a free-text value.");
            case AttributeType.SingleSelect or AttributeType.MultiSelect when command.OptionId is null:
                return Result<AttributeAssignmentResponse>.BadRequest("An option ID is required for SingleSelect and MultiSelect attributes.");
        }

        // For SINGLE_SELECT: remove any existing assignment for this (membership, attribute) pair
        // so we end up with exactly one option selected.
        if (attribute.Type == AttributeType.SingleSelect)
        {
            var existing = await assignmentRepository.GetByAttributeAsync(attribute.Id, cancellationToken);
            var toRemove = existing.Where(a => a.MembershipId == command.MembershipId).ToList();
            foreach (var old in toRemove)
            {
                assignmentRepository.Remove(old);
            }
        }

        // Upsert: find exact match for (membership, attribute, option) and update or create.
        var assignment = await assignmentRepository.GetByMembershipAttributeOptionAsync(
            command.MembershipId, command.AttributeId, command.OptionId, cancellationToken
        );

        bool isNew;
        if (assignment is not null)
        {
            assignment.UpdateValue(command.Value, command.OptionId, command.Weight);
            assignmentRepository.Update(assignment);
            isNew = false;
        }
        else
        {
            assignment = AttributeAssignment.Create(orgId, command.MembershipId, command.AttributeId,
                command.OptionId, command.Value, command.Weight
            );
            await assignmentRepository.AddAsync(assignment, cancellationToken);
            isNew = true;
        }

        await auditLogEmitter.EmitAsync(new AuditLogEvent(
                orgId,
                executionContext.UserInfo.Id!,
                executionContext.UserInfo.Email ?? string.Empty,
                nameof(AuditResource.Attribute),
                (isNew ? AuditAction.Created : AuditAction.Updated).ToString(),
                assignment.Id.ToString(),
                IpAddress: httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString(),
                UserAgent: httpContextAccessor.HttpContext?.Request.Headers.UserAgent.ToString()
            ), cancellationToken
        );

        events.CollectEvent(new AttributeAssigned(command.MembershipId, command.AttributeId));

        return assignment.ToResponse();
    }
}
