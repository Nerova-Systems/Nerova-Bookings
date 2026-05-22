using JetBrains.Annotations;

namespace Account.Features.AttributeSync.Domain;

/// <summary>
///     Determines how a claim value from the IdP is mapped to an <c>AttributeAssignment</c>.
/// </summary>
[PublicAPI]
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ClaimMappingMode
{
    /// <summary>
    ///     Writes the raw claim value as a free-text or numeric attribute value.
    ///     Suitable for <see cref="Attributes.Domain.AttributeType.Text" /> /
    ///     <see cref="Attributes.Domain.AttributeType.Number" /> attributes.
    /// </summary>
    Direct,

    /// <summary>
    ///     Maps the claim value to a single attribute option by slug.
    ///     Suitable for <see cref="Attributes.Domain.AttributeType.SingleSelect" /> attributes.
    /// </summary>
    Lookup,

    /// <summary>
    ///     Maps each value of a multi-value claim to one attribute option by slug.
    ///     Suitable for <see cref="Attributes.Domain.AttributeType.MultiSelect" /> attributes.
    ///     The <see cref="AttributeSyncRule.ClaimPath" /> may end with <c>[]</c> to signal
    ///     that the claim carries multiple values.
    /// </summary>
    Group
}
