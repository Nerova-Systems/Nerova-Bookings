using FluentValidation;
using JetBrains.Annotations;
using Main.Features.Insights.Shared;
using Main.Features.Scheduling.Domain;
using SharedKernel.Cqrs;

namespace Main.Features.Insights.Queries.GetBookingFunnel;

[PublicAPI]
public sealed record GetBookingFunnelQuery(
    DateTimeOffset From,
    DateTimeOffset To
) : IRequest<Result<BookingFunnelResponse>>;

public sealed class GetBookingFunnelQueryValidator : AbstractValidator<GetBookingFunnelQuery>
{
    public GetBookingFunnelQueryValidator()
    {
        RuleFor(q => q.To).GreaterThan(q => q.From).WithMessage("'To' must be after 'From'.");
        RuleFor(q => q.To).Must((q, to) => (to - q.From).TotalDays <= 366).WithMessage("The date range must not exceed 366 days.");
    }
}

/// <summary>
///     Funnel counts for bookings created within the requested date range.
///     Created → Accepted (confirmed) → Completed (accepted + end time in the past).
/// </summary>
[PublicAPI]
public sealed record BookingFunnelResponse(int Created, int Accepted, int Completed);

public sealed class GetBookingFunnelHandler(
    IBookingRepository bookingRepository,
    InsightsScopeResolver scopeResolver,
    TimeProvider timeProvider
) : IRequestHandler<GetBookingFunnelQuery, Result<BookingFunnelResponse>>
{
    public async Task<Result<BookingFunnelResponse>> Handle(GetBookingFunnelQuery query, CancellationToken cancellationToken)
    {
        var scopeResult = scopeResolver.ResolveWithAccess();
        if (!scopeResult.IsSuccess) return Result<BookingFunnelResponse>.From(scopeResult);
        var scope = scopeResult.Value!;

        var now = timeProvider.GetUtcNow();

        // DateTimeOffset comparisons cannot be translated to SQL on SQLite; filter in memory.
        var all = await bookingRepository.GetForScopeAsync(scope.TenantId, scope.UserId, scope.TeamId, cancellationToken);
        var bookings = all.Where(b => b.StartTime >= query.From && b.StartTime < query.To).ToList();

        var created = bookings.Count;
        var accepted = bookings.Count(b => b.Status == BookingStatus.Accepted);
        var completed = bookings.Count(b => b.Status == BookingStatus.Accepted && b.EndTime < now);

        return new BookingFunnelResponse(created, accepted, completed);
    }
}
