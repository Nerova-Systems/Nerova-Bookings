using Account.Features.AttributeSync.Domain;
using Account.Features.Attributes.Domain;
using JetBrains.Annotations;

namespace Account.Features.AttributeSync;

/// <summary>Serialisable representation of an <see cref="AttributeSyncRule" /> as returned by the API.</summary>
[PublicAPI]
public sealed record AttributeSyncRuleResponse(
    AttributeSyncRuleId Id,
    AttributeId AttributeId,
    string ClaimPath,
    ClaimMappingMode Mode,
    bool AutoCreateOptions,
    bool IsEnabled,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ModifiedAt);

/// <summary>Mapping helpers for <see cref="AttributeSyncRule" />.</summary>
public static class AttributeSyncRuleMappings
{
    public static AttributeSyncRuleResponse ToResponse(this AttributeSyncRule rule) =>
        new(
            Id: rule.Id,
            AttributeId: rule.AttributeId,
            ClaimPath: rule.ClaimPath,
            Mode: rule.Mode,
            AutoCreateOptions: rule.AutoCreateOptions,
            IsEnabled: rule.IsEnabled,
            CreatedAt: rule.CreatedAt,
            ModifiedAt: rule.ModifiedAt);
}
