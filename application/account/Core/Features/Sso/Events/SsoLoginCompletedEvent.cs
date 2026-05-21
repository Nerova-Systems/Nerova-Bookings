using System.Text.Json;
using Account.Features.AttributeSync.Domain;
using Account.Features.Memberships.Domain;
using MediatR;
using SharedKernel.Domain;

namespace Account.Features.Sso.Events;

/// <summary>
///     Published (in-process, via MediatR <see cref="INotification" />) after a successful SSO
///     login callback has been validated and a session has been issued.
///     <para>
///         Consumed by <c>SsoLoginCompletedAttributeSyncHandler</c> to apply IdP attribute sync
///         rules for the logged-in org member.
///     </para>
/// </summary>
public sealed record SsoLoginCompletedEvent(
    MembershipId MembershipId,
    TenantId OrgTenantId,
    SyncSource Source,
    IReadOnlyDictionary<string, JsonElement> Claims
) : INotification;
