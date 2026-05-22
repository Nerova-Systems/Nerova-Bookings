using FluentValidation;
using JetBrains.Annotations;
using Main.Features.Scheduling.Domain;
using SharedKernel.Cqrs;
using SharedKernel.ExecutionContext;

namespace Main.Features.Scheduling.Queries;

[PublicAPI]
public sealed record GetBookingHistoryQuery(BookingId Id, int PageOffset = 0, int PageSize = 25) : IRequest<Result<BookingHistoryResponse>>;

public sealed class GetBookingHistoryValidator : AbstractValidator<GetBookingHistoryQuery>
{
    public GetBookingHistoryValidator()
    {
        RuleFor(query => query.PageOffset).GreaterThanOrEqualTo(0);
        RuleFor(query => query.PageSize).InclusiveBetween(1, 100);
    }
}

[PublicAPI]
public sealed record BookingHistoryResponse(int TotalCount, int PageOffset, int PageSize, BookingHistoryEntryResponse[] Entries);

public sealed class GetBookingHistoryHandler(
    IBookingRepository bookingRepository,
    IBookingHistoryEntryRepository bookingHistoryEntryRepository,
    IExecutionContext executionContext
) : IRequestHandler<GetBookingHistoryQuery, Result<BookingHistoryResponse>>
{
    public async Task<Result<BookingHistoryResponse>> Handle(GetBookingHistoryQuery query, CancellationToken cancellationToken)
    {
        var tenantId = executionContext.UserInfo.TenantId;
        var ownerUserId = executionContext.UserInfo.Id;
        if (tenantId is null || ownerUserId is null)
        {
            return Result<BookingHistoryResponse>.Unauthorized("Authentication is required.");
        }

        var item = await bookingRepository.GetForOwnerWithEventTypeAsync(tenantId, ownerUserId, executionContext.ActiveTeamId, query.Id, cancellationToken);
        if (item is null)
        {
            return Result<BookingHistoryResponse>.NotFound($"Booking '{query.Id}' was not found.");
        }

        var (entries, total) = await bookingHistoryEntryRepository.GetForBookingPagedAsync(item.Booking.Id, query.PageOffset, query.PageSize, cancellationToken);
        return new BookingHistoryResponse(
            total,
            query.PageOffset,
            query.PageSize,
            entries.Select(h => new BookingHistoryEntryResponse(h.Id, h.EventType, h.OccurredAt, h.ActorUserId, h.PayloadJson)).ToArray()
        );
    }
}
