using Account.Features.Memberships.Domain;
using Account.Features.Permissions.Domain;
using Account.Features.Tenants.Domain;
using JetBrains.Annotations;
using SharedKernel.Domain;

namespace Account.Features.Teams;

/// <summary>
///     Serialisable representation of a Team — a <see cref="Tenant" /> with
///     <see cref="TenantKind.Team" /> and a non-null <see cref="Tenant.ParentTenantId" />.
///     Mirrors the team payload returned by cal.com's <c>viewer.teams.get</c> tRPC procedure.
/// </summary>
[PublicAPI]
public sealed record TeamResponse(
    TenantId Id,
    TenantId? ParentOrgId,
    string Name,
    string? Slug,
    string? Bio,
    string? LogoUrl,
    string? Theme,
    string? BrandColor,
    string? DarkBrandColor,
    bool HideBranding,
    bool HideTeamProfileLink,
    bool IsPrivate,
    bool HideBookATeamMember,
    int? TimeFormat,
    string? TimeZone,
    string? WeekStart,
    int MemberCount,
    DateTimeOffset CreatedAt);

/// <summary>Serialisable representation of a single member of a team.</summary>
[PublicAPI]
public sealed record TeamMemberResponse(
    MembershipId MembershipId,
    UserId UserId,
    string Email,
    string? FirstName,
    string? LastName,
    string? AvatarUrl,
    MembershipRole Role,
    RoleId? CustomRoleId,
    bool Accepted,
    DateTimeOffset? AcceptedAt,
    DateTimeOffset InvitedAt);

/// <summary>Shared mapping helpers for team responses.</summary>
public static class TeamMappings
{
    public static TeamResponse ToResponse(this Tenant team, int memberCount) =>
        new(
            Id: team.Id,
            ParentOrgId: team.ParentTenantId,
            Name: team.Name,
            Slug: team.Slug,
            Bio: team.Bio,
            LogoUrl: team.Logo.Url,
            Theme: team.Theme,
            BrandColor: team.BrandColor,
            DarkBrandColor: team.DarkBrandColor,
            HideBranding: team.HideBranding,
            HideTeamProfileLink: team.HideTeamProfileLink,
            IsPrivate: team.IsPrivate,
            HideBookATeamMember: team.HideBookATeamMember,
            TimeFormat: team.TimeFormat,
            TimeZone: team.TimeZone,
            WeekStart: team.WeekStart,
            MemberCount: memberCount,
            CreatedAt: team.CreatedAt);
}
