using SharedKernel.Domain;

namespace Main.Features.TeamMembers.Domain;

/// <summary>
///     Cross-SCS lookup for team members managed by the account SCS. Returns minimal contact
///     info (display name + email) for users matching a name/email query within the given tenant.
///     Implementations are expected to be read-only (no writes across the SCS boundary) and to
///     return an empty list when configured connection strings are missing.
/// </summary>
public interface ITeamMemberDirectory
{
    Task<TeamMember[]> SearchAsync(TenantId tenantId, string? query, int limit, CancellationToken cancellationToken);
}

public sealed record TeamMember(UserId UserId, string DisplayName, string Email);
