using FluentValidation;
using JetBrains.Annotations;
using Main.Features.Insights.Shared;
using Main.Features.Scheduling.Domain;
using SharedKernel.Cqrs;

namespace Main.Features.Insights.Queries.GetBookingHeatmap;

[PublicAPI]
public sealed record GetBookingHeatmapQuery(
    DateTimeOffset From,
    DateTimeOffset To,
    string TimeZone = "UTC"
) : IRequest<Result<BookingHeatmapResponse>>;

public sealed class GetBookingHeatmapQueryValidator : AbstractValidator<GetBookingHeatmapQuery>
{
    public GetBookingHeatmapQueryValidator()
    {
        RuleFor(q => q.To).GreaterThan(q => q.From).WithMessage("'To' must be after 'From'.");
        RuleFor(q => q.To).Must((q, to) => (to - q.From).TotalDays <= 366).WithMessage("The date range must not exceed 366 days.");
        RuleFor(q => q.TimeZone)
            .NotEmpty()
            .Must(tz =>
                {
                    try
                    {
                        TimeZoneInfo.FindSystemTimeZoneById(tz);
                        return true;
                    }
                    catch
                    {
                        return false;
                    }
                }
            )
            .WithMessage("'TimeZone' must be a valid IANA or Windows timezone identifier.");
    }
}

[PublicAPI]
public sealed record BookingHeatmapResponse(HeatmapCell[] Cells);

/// <summary>
///     A single cell in the 7×24 heatmap grid.
///     <paramref name="DayOfWeek" /> follows <see cref="System.DayOfWeek" /> (0=Sunday … 6=Saturday).
///     <paramref name="Hour" /> is the local hour in the requested timezone (0–23).
/// </summary>
[PublicAPI]
public sealed record HeatmapCell(int DayOfWeek, int Hour, int Count);

public sealed class GetBookingHeatmapHandler(
    IBookingRepository bookingRepository,
    InsightsScopeResolver scopeResolver
) : IRequestHandler<GetBookingHeatmapQuery, Result<BookingHeatmapResponse>>
{
    public async Task<Result<BookingHeatmapResponse>> Handle(GetBookingHeatmapQuery query, CancellationToken cancellationToken)
    {
        var scopeResult = scopeResolver.ResolveWithAccess();
        if (!scopeResult.IsSuccess) return Result<BookingHeatmapResponse>.From(scopeResult);
        var scope = scopeResult.Value!;

        // Validator already rejects invalid timezone identifiers; FindSystemTimeZoneById will not throw here.
        var tz = TimeZoneInfo.FindSystemTimeZoneById(query.TimeZone);

        // DateTimeOffset comparisons cannot be translated to SQL on SQLite; filter in memory.
        var all = await bookingRepository.GetForScopeAsync(scope.TenantId, scope.UserId, scope.TeamId, cancellationToken);
        var startTimes = all.Where(b => b.StartTime >= query.From && b.StartTime < query.To).Select(b => b.StartTime);

        var cells = startTimes
            .Select(startTime => TimeZoneInfo.ConvertTime(startTime, tz))
            .GroupBy(local => new { DayOfWeek = (int)local.DayOfWeek, local.Hour })
            .Select(g => new HeatmapCell(g.Key.DayOfWeek, g.Key.Hour, g.Count()))
            .OrderBy(c => c.DayOfWeek).ThenBy(c => c.Hour)
            .ToArray();

        return new BookingHeatmapResponse(cells);
    }
}
