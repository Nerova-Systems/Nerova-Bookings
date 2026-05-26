using Account.Features.Attributes.Domain;
using Account.Features.Memberships.Domain;
using JetBrains.Annotations;
using Attribute = Account.Features.Attributes.Domain.Attribute;

namespace Account.Features.Attributes;

/// <summary>Serialisable representation of an <see cref="AttributeOption" /> as returned by the API.</summary>
[PublicAPI]
public sealed record AttributeOptionResponse(
    AttributeOptionId Id,
    string Value,
    string Slug,
    bool IsGroup,
    string[] Contains
);

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
    DateTimeOffset? ModifiedAt
);

/// <summary>Serialisable representation of an <see cref="AttributeAssignment" /> as returned by the API.</summary>
[PublicAPI]
public sealed record AttributeAssignmentResponse(
    AttributeAssignmentId Id,
    MembershipId MembershipId,
    AttributeId AttributeId,
    string? Value,
    AttributeOptionId? AttributeOptionId,
    int? Weight
);

/// <summary>Mapping helpers shared across attribute command and query handlers.</summary>
public static class AttributeMappings
{
    public static AttributeOptionResponse ToResponse(this AttributeOption option)
    {
        return new AttributeOptionResponse(
            option.Id,
            option.Value,
            option.Slug,
            option.IsGroup,
            option.Contains
        );
    }

    public static AttributeResponse ToResponse(this Attribute attribute)
    {
        return new AttributeResponse(
            attribute.Id,
            attribute.Name,
            attribute.Slug,
            attribute.Type,
            attribute.IsWeightsEnabled,
            attribute.Enabled,
            attribute.IsLocked,
            attribute.Options.Select(o => o.ToResponse()).ToArray(),
            attribute.CreatedAt,
            attribute.ModifiedAt
        );
    }

    public static AttributeAssignmentResponse ToResponse(this AttributeAssignment assignment)
    {
        return new AttributeAssignmentResponse(
            assignment.Id,
            assignment.MembershipId,
            assignment.AttributeId,
            assignment.Value,
            assignment.AttributeOptionId,
            assignment.Weight
        );
    }
}
