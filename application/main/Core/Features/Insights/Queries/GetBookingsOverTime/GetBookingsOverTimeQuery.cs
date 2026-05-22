using FluentValidation;
using JetBrains.Annotations;
using Main.Features.Insights.Shared;
using Main.Features.Scheduling.Domain;
using SharedKernel.Cqrs;

namespace Main.Features.Insights.Queries.GetBookingsOverTime;

[PublicAPI]
public sealed record GetBookingsOverTimeQuery(
    DateTimeOffset From,
    DateTimeOffset To,
    TimeView TimeView = TimeView.Day
) : IRequest<Result<BookingsOverTimeResponse>>;

public sealed class GetBookingsOverTimeQueryValidator : AbstractValidator<GetBookingsOverTimeQuery>
{
    public GetBookingsOverTimeQueryValidator()
    {
        RuleFor(q => q.To).GreaterThan(q => q.From).WithMessage("'To' must be after 'From'.");
        RuleFor(q => q.To).Must((q, to) => (to - q.From).TotalDays <= 366).WithMessage("The date range must not exceed 366 days.");
    }
}

[PublicAPI]
public sealed record BookingsOverTimeResponse(BookingDataPoint[] DataPoints);

[PublicAPI]
public sealed record BookingDataPoint(DateTimeOffset Date, int Count);

public sealed class GetBookingsOverTimeHandler(
    IBookingRepository bookingRepository,
    InsightsScopeResolver scopeResolver
) : IRequestHandler<GetBookingsOverTimeQuery, Result<BookingsOverTimeResponse>>
{
    public async Task<Result<BookingsOverTimeResponse>> Handle(GetBookingsOverTimeQuery query, CancellationToken cancellationToken)
    {
        var scopeResult = scopeResolver.ResolveWithAccess();
        if (!scopeResult.IsSuccess) return Result<BookingsOverTimeResponse>.From(scopeResult);
        var scope = scopeResult.Value!;

        // Load into memory — SQLite cannot group-by DateTimeOffset columns directly
        // and cannot translate DateTimeOffset range comparisons to SQL on SQLite.
        var all = await bookingRepository.GetForScopeAsync(scope.TenantId, scope.UserId, scope.TeamId, cancellationToken);
        var bookings = all.Where(b => b.StartTime >= query.From && b.StartTime < query.To).Select(b => b.StartTime);

        var grouped = bookings
            .GroupBy(startTime => ToBucket(startTime, query.TimeView))
            .Select(g => new BookingDataPoint(g.Key, g.Count()))
            .OrderBy(dp => dp.Date)
            .ToArray();

        return new BookingsOverTimeResponse(grouped);
    }

    private static DateTimeOffset ToBucket(DateTimeOffset startTime, TimeView view)
    {
        var utc = startTime.ToUniversalTime();
        return view switch
        {
            TimeView.Day => new DateTimeOffset(utc.Year, utc.Month, utc.Day, 0, 0, 0, TimeSpan.Zero),
            TimeView.Week => StartOfWeek(utc),
            TimeView.Month => new DateTimeOffset(utc.Year, utc.Month, 1, 0, 0, 0, TimeSpan.Zero),
            TimeView.Year => new DateTimeOffset(utc.Year, 1, 1, 0, 0, 0, TimeSpan.Zero),
            _ => new DateTimeOffset(utc.Year, utc.Month, utc.Day, 0, 0, 0, TimeSpan.Zero)
        };
    }

    private static DateTimeOffset StartOfWeek(DateTimeOffset dt)
    {
        var diff = (7 + (dt.DayOfWeek - DayOfWeek.Monday)) % 7;
        return new DateTimeOffset(dt.Year, dt.Month, dt.Day, 0, 0, 0, TimeSpan.Zero).AddDays(-diff);
    }
}
