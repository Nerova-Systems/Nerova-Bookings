using JetBrains.Annotations;

namespace Main.Features.Apps.Domain;

/// <summary>
///     Describes a single OAuth scope / permission a connector requests from (or relies on at)
///     the third-party provider. Surfaced through the Apps API so the frontend can render a
///     truthful, cal.com-style per-app permissions screen.
///     <para>
///         <paramref name="Scope" /> is the exact scope string the connector requests or
///         depends on (sourced from the connector code — never fabricated).
///         <paramref name="Title" /> is a short human-readable label and
///         <paramref name="Description" /> is an accurate one-line explanation of what granting
///         that scope allows the connector to do.
///     </para>
/// </summary>
[PublicAPI]
public sealed record AppPermission(string Scope, string Title, string Description);
