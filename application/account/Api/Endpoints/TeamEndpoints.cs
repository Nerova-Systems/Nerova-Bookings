using Account.Database;
using Account.Features.FeatureFlags;
using Account.Features.FeatureFlags.Domain;
using Account.Features.Teams.Domain;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Domain;
using SharedKernel.Endpoints;
using SharedKernel.ExecutionContext;

namespace Account.Api.Endpoints;

public sealed class TeamEndpoints : IEndpoints
{
    private const string RoutesPrefix = "/api/account/teams";

    public void MapEndpoints(IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup(RoutesPrefix).WithTags("Teams").RequireAuthorization();

        group.MapGet("/", ListTeams);
        group.MapPost("/", CreateTeam);
        group.MapPut("/{teamId}", UpdateTeam);
        group.MapDelete("/{teamId}", DeleteTeam);
        group.MapGet("/{teamId}/members", ListTeamMembers);
        group.MapPut("/{teamId}/members", ReplaceTeamMembers);
        group.MapPut("/{teamId}/members/{userId}/role", ChangeTeamMemberRole);
    }

    private static async Task<IResult> ListTeams(AccountDbContext db, FeatureFlagEvaluator featureFlags, IExecutionContext executionContext, CancellationToken cancellationToken)
    {
        var tenantId = RequireTenant(executionContext);
        if (!await TeamsEnabled(featureFlags, tenantId, executionContext.UserInfo.Id, cancellationToken)) return TeamsDisabled();

        var teams = await db.Teams
            .OrderBy(team => team.Name)
            .Select(team => new TeamDto(team.Id, team.Name, team.Description))
            .ToListAsync(cancellationToken);
        return Results.Ok(teams);
    }

    private static async Task<IResult> CreateTeam(TeamRequest request, AccountDbContext db, FeatureFlagEvaluator featureFlags, IExecutionContext executionContext, TimeProvider timeProvider, CancellationToken cancellationToken)
    {
        var tenantId = RequireTenant(executionContext);
        if (!await TeamsEnabled(featureFlags, tenantId, executionContext.UserInfo.Id, cancellationToken)) return TeamsDisabled();
        if (string.IsNullOrWhiteSpace(request.Name)) return Results.BadRequest("Team name is required.");

        var team = new Team
        {
            TenantId = tenantId,
            Name = request.Name.Trim(),
            Description = request.Description?.Trim() ?? string.Empty,
            CreatedAt = timeProvider.GetUtcNow()
        };
        db.Teams.Add(team);
        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(new TeamDto(team.Id, team.Name, team.Description));
    }

    private static async Task<IResult> UpdateTeam(string teamId, TeamRequest request, AccountDbContext db, FeatureFlagEvaluator featureFlags, IExecutionContext executionContext, CancellationToken cancellationToken)
    {
        var tenantId = RequireTenant(executionContext);
        if (!await TeamsEnabled(featureFlags, tenantId, executionContext.UserInfo.Id, cancellationToken)) return TeamsDisabled();
        if (string.IsNullOrWhiteSpace(request.Name)) return Results.BadRequest("Team name is required.");

        var team = await db.Teams.AsTracking().FirstOrDefaultAsync(item => item.Id == teamId, cancellationToken);
        if (team is null) return Results.NotFound();

        team.Name = request.Name.Trim();
        team.Description = request.Description?.Trim() ?? string.Empty;
        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(new TeamDto(team.Id, team.Name, team.Description));
    }

    private static async Task<IResult> DeleteTeam(string teamId, AccountDbContext db, FeatureFlagEvaluator featureFlags, IExecutionContext executionContext, CancellationToken cancellationToken)
    {
        var tenantId = RequireTenant(executionContext);
        if (!await TeamsEnabled(featureFlags, tenantId, executionContext.UserInfo.Id, cancellationToken)) return TeamsDisabled();

        var team = await db.Teams.AsTracking().FirstOrDefaultAsync(item => item.Id == teamId, cancellationToken);
        if (team is null) return Results.NotFound();

        var members = await db.TeamMembers.AsTracking().Where(member => member.TeamId == teamId).ToListAsync(cancellationToken);
        db.TeamMembers.RemoveRange(members);
        db.Teams.Remove(team);
        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok();
    }

    private static async Task<IResult> ListTeamMembers(string teamId, AccountDbContext db, FeatureFlagEvaluator featureFlags, IExecutionContext executionContext, CancellationToken cancellationToken)
    {
        var tenantId = RequireTenant(executionContext);
        if (!await TeamsEnabled(featureFlags, tenantId, executionContext.UserInfo.Id, cancellationToken)) return TeamsDisabled();
        if (!await db.Teams.AnyAsync(team => team.Id == teamId, cancellationToken)) return Results.NotFound();

        var members = await db.TeamMembers
            .Where(member => member.TeamId == teamId)
            .OrderBy(member => member.UserId)
            .Select(member => new TeamMemberDto(member.UserId, member.Role.ToString()))
            .ToListAsync(cancellationToken);
        return Results.Ok(members);
    }

    private static async Task<IResult> ReplaceTeamMembers(string teamId, ReplaceTeamMembersRequest request, AccountDbContext db, FeatureFlagEvaluator featureFlags, IExecutionContext executionContext, CancellationToken cancellationToken)
    {
        var tenantId = RequireTenant(executionContext);
        if (!await TeamsEnabled(featureFlags, tenantId, executionContext.UserInfo.Id, cancellationToken)) return TeamsDisabled();
        if (!await db.Teams.AnyAsync(team => team.Id == teamId, cancellationToken)) return Results.NotFound();

        var requested = (request.Members ?? Array.Empty<TeamMemberRequest>())
            .Where(member => !string.IsNullOrWhiteSpace(member.UserId))
            .Select(member => new TeamMemberRequest(member.UserId!.Trim(), member.Role))
            .GroupBy(member => member.UserId)
            .Select(group => group.Last())
            .ToArray();

        var existing = await db.TeamMembers.AsTracking().Where(member => member.TeamId == teamId).ToListAsync(cancellationToken);
        db.TeamMembers.RemoveRange(existing.Where(member => requested.All(requestedMember => requestedMember.UserId != member.UserId)));

        foreach (var requestedMember in requested)
        {
            var userId = requestedMember.UserId!;
            var role = ParseRole(requestedMember.Role);
            var member = existing.FirstOrDefault(item => item.UserId == userId);
            if (member is null)
            {
                db.TeamMembers.Add(new TeamMember { TenantId = tenantId, TeamId = teamId, UserId = userId, Role = role });
            }
            else
            {
                member.Role = role;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok();
    }

    private static async Task<IResult> ChangeTeamMemberRole(string teamId, string userId, ChangeTeamMemberRoleRequest request, AccountDbContext db, FeatureFlagEvaluator featureFlags, IExecutionContext executionContext, CancellationToken cancellationToken)
    {
        var tenantId = RequireTenant(executionContext);
        if (!await TeamsEnabled(featureFlags, tenantId, executionContext.UserInfo.Id, cancellationToken)) return TeamsDisabled();

        var member = await db.TeamMembers.AsTracking().FirstOrDefaultAsync(item => item.TeamId == teamId && item.UserId == userId, cancellationToken);
        if (member is null) return Results.NotFound();

        member.Role = ParseRole(request.Role);
        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok();
    }

    private static async Task<bool> TeamsEnabled(FeatureFlagEvaluator featureFlags, TenantId tenantId, UserId? userId, CancellationToken cancellationToken)
    {
        return await featureFlags.IsEnabledAsync(FeatureFlagKeys.Teams, tenantId, userId, cancellationToken);
    }

    private static IResult TeamsDisabled()
    {
        return Results.Problem("Feature 'teams' is not enabled.", statusCode: StatusCodes.Status404NotFound);
    }

    private static TeamMemberRole ParseRole(string? role)
    {
        return Enum.TryParse<TeamMemberRole>(role, true, out var parsed) ? parsed : TeamMemberRole.Member;
    }

    private static TenantId RequireTenant(IExecutionContext executionContext)
    {
        return executionContext.TenantId ?? throw new InvalidOperationException("Tenant context is required.");
    }
}

public sealed record TeamRequest(string Name, string? Description);
public sealed record TeamDto(string Id, string Name, string Description);
public sealed record TeamMemberDto(string UserId, string Role);
public sealed record ReplaceTeamMembersRequest(IReadOnlyList<TeamMemberRequest> Members);
public sealed record TeamMemberRequest(string? UserId, string? Role);
public sealed record ChangeTeamMemberRoleRequest(string? Role);
