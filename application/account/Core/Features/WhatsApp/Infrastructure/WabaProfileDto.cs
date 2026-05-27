using System.Text.Json.Serialization;
using JetBrains.Annotations;

namespace Account.Features.WhatsApp.Infrastructure;

/// <summary>
///     Wire shape of the Meta <c>POST /{phone_number_id}/whatsapp_business_profile</c> body.
///     <para>
///         All fields except <see cref="MessagingProduct" /> are optional. Meta will only update
///         the fields that are present; omit a field to leave its current value untouched.
///     </para>
/// </summary>
[PublicAPI]
public sealed record WabaProfileDto(
    [property: JsonPropertyName("messaging_product")]
    string MessagingProduct,
    [property: JsonPropertyName("about")] string? About,
    [property: JsonPropertyName("address")] string? Address,
    [property: JsonPropertyName("description")]
    string? Description,
    [property: JsonPropertyName("email")] string? Email,
    [property: JsonPropertyName("vertical")]
    string? Vertical,
    [property: JsonPropertyName("websites")]
    IReadOnlyList<string>? Websites,
    [property: JsonPropertyName("profile_picture_handle")]
    string? ProfilePictureHandle
);
