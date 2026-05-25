using FluentValidation;
using JetBrains.Annotations;
using Main.Features.Permissions.Domain;
using Main.Features.Permissions.Pipeline;
using Main.Features.Scheduling.Domain;
using SharedKernel.Cqrs;
using SharedKernel.Domain;
using SharedKernel.ExecutionContext;

namespace Main.Features.Scheduling.Queries;

/// <summary>
///     Returns the most recent trust-and-safety reports filed against bookings within the caller's
///     tenant. Admin/Owner only — guarded by <c>bookings.manage</c>; ordinary members lack this
///     permission and are denied by the <see cref="PermissionCheckBehavior{TRequest,TResponse}" />.
/// </summary>
[PublicAPI]
[RequirePermission(PermissionResource.Booking, PermissionAction.Manage)]
public sealed record GetReportsForTenantQuery(int PageOffset = 0, int PageSize = 50) : IRequest<Result<BookingReportsResponse>>;

public sealed class GetReportsForTenantQueryValidator : AbstractValidator<GetReportsForTenantQuery>
{
    public GetReportsForTenantQueryValidator()
    {
        RuleFor(query => query.PageOffset).GreaterThanOrEqualTo(0);
        RuleFor(query => query.PageSize).InclusiveBetween(1, 200);
    }
}

[PublicAPI]
public sealed record BookingReportsResponse(int TotalCount, int PageOffset, int PageSize, BookingReportResponse[] Reports);

[PublicAPI]
public sealed record BookingReportResponse(BookingReportId Id, BookingId BookingId, UserId ReportedByUserId, BookingReportReasonCode ReasonCode, string? Notes, DateTimeOffset CreatedAt);

public sealed class GetReportsForTenantHandler(IBookingReportRepository bookingReportRepository, IExecutionContext executionContext)
    : IRequestHandler<GetReportsForTenantQuery, Result<BookingReportsResponse>>
{
    public async Task<Result<BookingReportsResponse>> Handle(GetReportsForTenantQuery query, CancellationToken cancellationToken)
    {
        var tenantId = executionContext.UserInfo.TenantId;
        if (tenantId is null)
        {
            return Result<BookingReportsResponse>.Unauthorized("Authentication is required.");
        }

        var total = await bookingReportRepository.CountForTenantAsync(tenantId, cancellationToken);
        var reports = await bookingReportRepository.GetForTenantAsync(tenantId, query.PageOffset, query.PageSize, cancellationToken);

        var items = reports
            .Select(report => new BookingReportResponse(report.Id, report.BookingId, report.ReportedByUserId, report.ReasonCode, report.Notes, report.CreatedAt))
            .ToArray();

        return new BookingReportsResponse(total, query.PageOffset, query.PageSize, items);
    }
}
