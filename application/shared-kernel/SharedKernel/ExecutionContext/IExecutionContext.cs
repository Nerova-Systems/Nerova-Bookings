using System.Net;
using SharedKernel.Authentication;
using SharedKernel.Domain;

namespace SharedKernel.ExecutionContext;

/// <summary>
///     Represents the execution context of the current operation, providing information
///     about the tenant and the authenticated user making the request.
/// </summary>
public interface IExecutionContext
{
    /// <summary>
    ///     Gets the current tenant identifier. May be null if the operation is not tenant-scoped.
    ///     Reflects the active tenant after any tenant-switch (Solo / Team / Organization).
    /// </summary>
    TenantId? TenantId { get; }

    /// <summary>
    ///     Gets the currently-active team scope, or <see langword="null" /> when the session is not
    ///     team-scoped. Set when the user switches to a <c>TenantKind.Team</c> tenant.
    /// </summary>
    TenantId? ActiveTeamId { get; }

    /// <summary>
    ///     Gets the currently-active organization, or <see langword="null" /> when the session is not
    ///     org-scoped (e.g., a Solo session). For a Team session this is the team's parent org; for an
    ///     Organization session this equals <see cref="TenantId" />.
    /// </summary>
    TenantId? ActiveOrgId { get; }

    /// <summary>
    ///     Gets the <c>OrgProfile</c> the user is acting as in the current org session, or
    ///     <see langword="null" /> when there is no active org scope or the user has no profile in
    ///     that org.
    /// </summary>
    string? ActiveOrgProfileId { get; }

    /// <summary>
    ///     Gets information about the current user making the request, including authentication status,
    ///     user ID, and other identity-related details.
    ///     If the user is not authenticated, it will be set to <see cref="Authentication.UserInfo.System" />.
    /// </summary>
    UserInfo UserInfo { get; }

    IPAddress ClientIpAddress { get; }
}
