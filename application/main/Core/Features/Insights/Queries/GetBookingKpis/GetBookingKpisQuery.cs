using FluentValidation;
using JetBrains.Annotations;
using Main.Features.Insights.Shared;
using Main.Features.Scheduling.Domain;
using SharedKernel.Cqrs;

namespace Main.Features.Insights.Queries.GetBookingKpis;

[PublicAPI]
public sealed record GetBookingKpisQuery(
    DateTimeOffset From,
    DateTimeOffset To
) : IRequest<Result<BookingKpisResponse>>;

public sealed class GetBookingKpisQueryValidator : AbstractValidator<GetBookingKpisQuery>
{
    public GetBookingKpisQueryValidator()
    {
        RuleFor(q => q.To).GreaterThan(q => q.From).WithMessage("'To' must be after 'From'.");
        RuleFor(q => q.To).Must((q, to) => (to - q.From).TotalDays <= 366).WithMessage("The date range must not exceed 366 days.");
    }
}

[PublicAPI]
public sealed record BookingKpisResponse(
    int TotalCount,
    int AcceptedCount,
    int PendingCount,
    int CancelledCount,
    int CompletedCount,
    int PriorPeriodTotalCount,
    int PriorPeriodAcceptedCount,
    int PriorPeriodCancelledCount
);

public sealed class GetBookingKpisHandler(
    IBookingRepository bookingRepository,
    InsightsScopeResolver scopeResolver,
    TimeProvider timeProvider
) : IRequestHandler<GetBookingKpisQuery, Result<BookingKpisResponse>>
{
    public async Task<Result<BookingKpisResponse>> Handle(GetBookingKpisQuery query, CancellationToken cancellationToken)
    {
        var scopeResult = scopeResolver.ResolveWithAccess();
        if (!scopeResult.IsSuccess) return Result<BookingKpisResponse>.From(scopeResult);
        var scope = scopeResult.Value!;

        var range = new InsightsDateRange(query.From, query.To);
        var prior = range.PriorPeriod();
        var now = timeProvider.GetUtcNow();

        // DateTimeOffset comparisons cannot be translated to SQL on SQLite; filter in memory.
        var all = await bookingRepository.GetForScopeAsync(scope.TenantId, scope.UserId, scope.TeamId, cancellationToken);
        var current = all.Where(b => b.StartTime >= range.From && b.StartTime < range.To).ToList();
        var priorData = all.Where(b => b.StartTime >= prior.From && b.StartTime < prior.To).ToList();

        var completed = current.Count(b => b.Status.Equals(BookingStatuses.Accepted, StringComparison.OrdinalIgnoreCase) && b.EndTime < now);
        var cancelled = current.Count(b => b.Status.Equals(BookingStatuses.Cancelled, StringComparison.OrdinalIgnoreCase) || b.Status.Equals(BookingStatuses.Rejected, StringComparison.OrdinalIgnoreCase));
        var accepted = current.Count(b => b.Status.Equals(BookingStatuses.Accepted, StringComparison.OrdinalIgnoreCase));
        var pending = current.Count(b => b.Status.Equals(BookingStatuses.Pending, StringComparison.OrdinalIgnoreCase));

        var priorCancelled = priorData.Count(b => b.Status.Equals(BookingStatuses.Cancelled, StringComparison.OrdinalIgnoreCase) || b.Status.Equals(BookingStatuses.Rejected, StringComparison.OrdinalIgnoreCase));
        var priorAccepted = priorData.Count(b => b.Status.Equals(BookingStatuses.Accepted, StringComparison.OrdinalIgnoreCase));

        return new BookingKpisResponse(
            TotalCount: current.Count,
            AcceptedCount: accepted,
            PendingCount: pending,
            CancelledCount: cancelled,
            CompletedCount: completed,
            PriorPeriodTotalCount: priorData.Count,
            PriorPeriodAcceptedCount: priorAccepted,
            PriorPeriodCancelledCount: priorCancelled
        );
    }
}
