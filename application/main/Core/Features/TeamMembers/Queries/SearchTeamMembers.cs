using FluentValidation;
using JetBrains.Annotations;
using Main.Features.Permissions.Domain;
using Main.Features.Permissions.Pipeline;
using Main.Features.TeamMembers.Domain;
using Main.Features.TeamMembers.Shared;
using SharedKernel.Cqrs;
using SharedKernel.ExecutionContext;

namespace Main.Features.TeamMembers.Queries;

/// <summary>
///     Searches for users within the caller's tenant by partial name or email. The directory
///     reads the account SCS via a single read-only SELECT; when the connection string is missing
///     (e.g., in unit tests that don't reference the account database) an empty list is returned.
///     Tenant-scoped membership filtering is currently a tenant-id match — team-level filtering
///     is deferred until cross-SCS membership is exposed to main.
/// </summary>
[PublicAPI]
[RequirePermission(PermissionResource.EventType, PermissionAction.Read)]
public sealed record SearchTeamMembersQuery(string? Query, int Limit = 20)
    : IRequest<Result<SearchTeamMembersResponse>>;

public sealed class SearchTeamMembersValidator : AbstractValidator<SearchTeamMembersQuery>
{
    public SearchTeamMembersValidator()
    {
        RuleFor(query => query.Query).MaximumLength(200);
        RuleFor(query => query.Limit).InclusiveBetween(1, 100);
    }
}

public sealed class SearchTeamMembersHandler(ITeamMemberDirectory directory, IExecutionContext executionContext)
    : IRequestHandler<SearchTeamMembersQuery, Result<SearchTeamMembersResponse>>
{
    public async Task<Result<SearchTeamMembersResponse>> Handle(SearchTeamMembersQuery query, CancellationToken cancellationToken)
    {
        var tenantId = executionContext.TenantId;
        if (tenantId is null)
        {
            return Result<SearchTeamMembersResponse>.Unauthorized("Authentication is required.");
        }

        var members = await directory.SearchAsync(tenantId, query.Query, query.Limit, cancellationToken);
        var response = members
            .Select(member => new TeamMemberResponse(member.UserId.Value, member.DisplayName, member.Email))
            .ToArray();

        return new SearchTeamMembersResponse(response);
    }
}
