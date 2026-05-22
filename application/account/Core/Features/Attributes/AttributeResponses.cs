using Account.Features.Attributes.Domain;
using Account.Features.Memberships.Domain;
using JetBrains.Annotations;

namespace Account.Features.Attributes;

/// <summary>Serialisable representation of an <see cref="AttributeOption" /> as returned by the API.</summary>
[PublicAPI]
public sealed record AttributeOptionResponse(
    AttributeOptionId Id,
    string Value,
    string Slug,
    bool IsGroup,
    string[] Contains);

/// <summary>Serialisable representation of an <see cref="Domain.Attribute" /> as returned by the API.</summary>
[PublicAPI]
public sealed record AttributeResponse(
    AttributeId Id,
    string Name,
    string Slug,
    AttributeType Type,
    bool IsWeightsEnabled,
    bool Enabled,
    bool IsLocked,
    AttributeOptionResponse[] Options,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ModifiedAt);

/// <summary>Serialisable representation of an <see cref="AttributeAssignment" /> as returned by the API.</summary>
[PublicAPI]
public sealed record AttributeAssignmentResponse(
    AttributeAssignmentId Id,
    MembershipId MembershipId,
    AttributeId AttributeId,
    string? Value,
    AttributeOptionId? AttributeOptionId,
    int? Weight);

/// <summary>Mapping helpers shared across attribute command and query handlers.</summary>
public static class AttributeMappings
{
    public static AttributeOptionResponse ToResponse(this AttributeOption option) =>
        new(
            Id: option.Id,
            Value: option.Value,
            Slug: option.Slug,
            IsGroup: option.IsGroup,
            Contains: option.Contains);

    public static AttributeResponse ToResponse(this Domain.Attribute attribute) =>
        new(
            Id: attribute.Id,
            Name: attribute.Name,
            Slug: attribute.Slug,
            Type: attribute.Type,
            IsWeightsEnabled: attribute.IsWeightsEnabled,
            Enabled: attribute.Enabled,
            IsLocked: attribute.IsLocked,
            Options: attribute.Options.Select(o => o.ToResponse()).ToArray(),
            CreatedAt: attribute.CreatedAt,
            ModifiedAt: attribute.ModifiedAt);

    public static AttributeAssignmentResponse ToResponse(this AttributeAssignment assignment) =>
        new(
            Id: assignment.Id,
            MembershipId: assignment.MembershipId,
            AttributeId: assignment.AttributeId,
            Value: assignment.Value,
            AttributeOptionId: assignment.AttributeOptionId,
            Weight: assignment.Weight);
}
