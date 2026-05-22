using System.Text.Json.Serialization;
using JetBrains.Annotations;

namespace Account.Features.Attributes.Domain;

/// <summary>
///     The data type of an <see cref="Attribute" />, which determines how its value is stored and
///     validated on assignments. Mirrors <c>AttributeType</c> in the cal.com Prisma schema.
/// </summary>
[PublicAPI]
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AttributeType
{
    /// <summary>Free-text string value.</summary>
    Text,

    /// <summary>Numeric value (stored as decimal-parseable string).</summary>
    Number,

    /// <summary>Single option chosen from <see cref="AttributeOption" /> values.</summary>
    SingleSelect,

    /// <summary>Multiple options chosen from <see cref="AttributeOption" /> values.</summary>
    MultiSelect
}
