using FluentValidation;
using JetBrains.Annotations;
using Main.Features.Insights.Shared;
using Main.Features.Scheduling.Domain;
using SharedKernel.Cqrs;
using SharedKernel.Domain;

namespace Main.Features.Insights.Queries.GetTopHosts;

[PublicAPI]
public sealed record GetTopHostsQuery(
    DateTimeOffset From,
    DateTimeOffset To,
    int Limit = 5
) : IRequest<Result<TopHostsResponse>>;

public sealed class GetTopHostsQueryValidator : AbstractValidator<GetTopHostsQuery>
{
    public GetTopHostsQueryValidator()
    {
        RuleFor(q => q.To).GreaterThan(q => q.From).WithMessage("'To' must be after 'From'.");
        RuleFor(q => q.To).Must((q, to) => (to - q.From).TotalDays <= 366).WithMessage("The date range must not exceed 366 days.");
        RuleFor(q => q.Limit).InclusiveBetween(1, 50);
    }
}

[PublicAPI]
public sealed record TopHostsResponse(HostInsights[] Hosts);

[PublicAPI]
public sealed record HostInsights(UserId HostUserId, int TotalCount);

public sealed class GetTopHostsHandler(
    IBookingRepository bookingRepository,
    InsightsScopeResolver scopeResolver
) : IRequestHandler<GetTopHostsQuery, Result<TopHostsResponse>>
{
    public async Task<Result<TopHostsResponse>> Handle(GetTopHostsQuery query, CancellationToken cancellationToken)
    {
        var scopeResult = scopeResolver.ResolveWithAccess();
        if (!scopeResult.IsSuccess) return Result<TopHostsResponse>.From(scopeResult);
        var scope = scopeResult.Value!;

        // DateTimeOffset comparisons cannot be translated to SQL on SQLite; filter in memory.
        var all = await bookingRepository.GetForScopeAsync(scope.TenantId, scope.UserId, scope.TeamId, cancellationToken);
        var ownerUserIds = all
            .Where(b => b.StartTime >= query.From && b.StartTime < query.To)
            .Select(b => b.OwnerUserId);

        var hosts = ownerUserIds
            .GroupBy(id => id)
            .Select(g => new HostInsights(g.Key, g.Count()))
            .OrderByDescending(h => h.TotalCount)
            .Take(query.Limit)
            .ToArray();

        return new TopHostsResponse(hosts);
    }
}
