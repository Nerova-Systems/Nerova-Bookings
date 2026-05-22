using FluentValidation;
using JetBrains.Annotations;
using Main.Features.EventTypes.Domain;
using Main.Features.Insights.Shared;
using Main.Features.Scheduling.Domain;
using SharedKernel.Cqrs;

namespace Main.Features.Insights.Queries.GetTopEventTypes;

[PublicAPI]
public sealed record GetTopEventTypesQuery(
    DateTimeOffset From,
    DateTimeOffset To,
    int Limit = 5
) : IRequest<Result<TopEventTypesResponse>>;

public sealed class GetTopEventTypesQueryValidator : AbstractValidator<GetTopEventTypesQuery>
{
    public GetTopEventTypesQueryValidator()
    {
        RuleFor(q => q.To).GreaterThan(q => q.From).WithMessage("'To' must be after 'From'.");
        RuleFor(q => q.To).Must((q, to) => (to - q.From).TotalDays <= 366).WithMessage("The date range must not exceed 366 days.");
        RuleFor(q => q.Limit).InclusiveBetween(1, 50);
    }
}

[PublicAPI]
public sealed record TopEventTypesResponse(EventTypeInsights[] EventTypes);

[PublicAPI]
public sealed record EventTypeInsights(
    EventTypeId EventTypeId,
    string Title,
    string Slug,
    int TotalCount,
    int CancelledCount,
    double CancellationRate
);

public sealed class GetTopEventTypesHandler(
    IBookingRepository bookingRepository,
    InsightsScopeResolver scopeResolver
) : IRequestHandler<GetTopEventTypesQuery, Result<TopEventTypesResponse>>
{
    public async Task<Result<TopEventTypesResponse>> Handle(GetTopEventTypesQuery query, CancellationToken cancellationToken)
    {
        var scopeResult = scopeResolver.ResolveWithAccess();
        if (!scopeResult.IsSuccess) return Result<TopEventTypesResponse>.From(scopeResult);
        var scope = scopeResult.Value!;

        // DateTimeOffset comparisons cannot be translated to SQL on SQLite; filter in memory after loading.
        // Soft-delete filter on EventType is disabled so bookings for deleted event types are still included.
        var withEventTypes = await bookingRepository.GetForScopeWithEventTypesUnfilteredAsync(
            scope.TenantId, scope.UserId, scope.TeamId, cancellationToken
        );

        var grouped = withEventTypes
            .Where(x => x.Booking.StartTime >= query.From && x.Booking.StartTime < query.To)
            .GroupBy(x => new { x.EventType.Id, x.EventType.Title, x.EventType.Slug })
            .Select(g =>
                {
                    var total = g.Count();
                    var cancelled = g.Count(x => x.Booking.Status.Equals(BookingStatuses.Cancelled, StringComparison.OrdinalIgnoreCase)
                                                 || x.Booking.Status.Equals(BookingStatuses.Rejected, StringComparison.OrdinalIgnoreCase)
                    );
                    return new EventTypeInsights(
                        g.Key.Id,
                        g.Key.Title,
                        g.Key.Slug,
                        total,
                        cancelled,
                        total > 0 ? Math.Round((double)cancelled / total, 4) : 0.0
                    );
                }
            )
            .OrderByDescending(e => e.TotalCount)
            .Take(query.Limit)
            .ToArray();

        return new TopEventTypesResponse(grouped);
    }
}
