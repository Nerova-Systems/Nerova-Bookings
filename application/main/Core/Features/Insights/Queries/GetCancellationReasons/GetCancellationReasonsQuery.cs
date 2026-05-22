using System.Text.Json;
using FluentValidation;
using JetBrains.Annotations;
using Main.Features.Insights.Shared;
using Main.Features.Scheduling.Domain;
using SharedKernel.Cqrs;

namespace Main.Features.Insights.Queries.GetCancellationReasons;

[PublicAPI]
public sealed record GetCancellationReasonsQuery(
    DateTimeOffset From,
    DateTimeOffset To
) : IRequest<Result<CancellationReasonsResponse>>;

public sealed class GetCancellationReasonsQueryValidator : AbstractValidator<GetCancellationReasonsQuery>
{
    public GetCancellationReasonsQueryValidator()
    {
        RuleFor(q => q.To).GreaterThan(q => q.From).WithMessage("'To' must be after 'From'.");
        RuleFor(q => q.To).Must((q, to) => (to - q.From).TotalDays <= 366).WithMessage("The date range must not exceed 366 days.");
    }
}

[PublicAPI]
public sealed record CancellationReasonsResponse(CancellationReasonItem[] Reasons);

[PublicAPI]
public sealed record CancellationReasonItem(string Reason, int Count);

public sealed class GetCancellationReasonsHandler(
    IBookingRepository bookingRepository,
    InsightsScopeResolver scopeResolver
) : IRequestHandler<GetCancellationReasonsQuery, Result<CancellationReasonsResponse>>
{
    private const string FallbackReason = "No reason provided";

    public async Task<Result<CancellationReasonsResponse>> Handle(GetCancellationReasonsQuery query, CancellationToken cancellationToken)
    {
        var scopeResult = scopeResolver.ResolveWithAccess();
        if (!scopeResult.IsSuccess) return Result<CancellationReasonsResponse>.From(scopeResult);
        var scope = scopeResult.Value!;

        // DateTimeOffset comparisons cannot be translated to SQL on SQLite; filter in memory.
        var all = await bookingRepository.GetForScopeAsync(scope.TenantId, scope.UserId, scope.TeamId, cancellationToken);
        var cancellations = all
            .Where(b => b.StartTime >= query.From && b.StartTime < query.To)
            .Where(b => b.Status.Equals(BookingStatuses.Cancelled, StringComparison.OrdinalIgnoreCase)
                        || b.Status.Equals(BookingStatuses.Rejected, StringComparison.OrdinalIgnoreCase)
            )
            .Select(b => b.ResponsesJson);

        var reasons = cancellations
            .Select(ExtractReason)
            .GroupBy(r => r)
            .Select(g => new CancellationReasonItem(g.Key, g.Count()))
            .OrderByDescending(r => r.Count)
            .ToArray();

        return new CancellationReasonsResponse(reasons);
    }

    private static string ExtractReason(string responsesJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(responsesJson);
            if (doc.RootElement.TryGetProperty("cancellationReason", out var prop))
            {
                var value = prop.GetString();
                if (!string.IsNullOrWhiteSpace(value)) return value.Trim();
            }
        }
        catch
        {
            // Malformed JSON — fall through to default
        }

        return FallbackReason;
    }
}
