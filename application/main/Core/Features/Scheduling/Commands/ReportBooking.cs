using FluentValidation;
using JetBrains.Annotations;
using Main.Features.Permissions.Domain;
using Main.Features.Permissions.Pipeline;
using Main.Features.Scheduling.Domain;
using SharedKernel.Cqrs;
using SharedKernel.ExecutionContext;

namespace Main.Features.Scheduling.Commands;

/// <summary>
///     Files a trust-and-safety report against a <see cref="Booking" />. Any workspace member can
///     file (the <c>bookings.report</c> permission is granted to Member+). Admin/Owner view the
///     resulting feed via <c>GET /api/bookings/reports</c> (guarded by <c>bookings.manage</c>).
/// </summary>
[PublicAPI]
[RequirePermission(PermissionResource.Booking, PermissionAction.Report)]
public sealed record ReportBookingCommand(BookingId Id, BookingReportReasonCode ReasonCode, string? Notes = null) : ICommand, IRequest<Result<BookingReportId>>;

public sealed class ReportBookingValidator : AbstractValidator<ReportBookingCommand>
{
    public ReportBookingValidator()
    {
        RuleFor(command => command.ReasonCode).IsInEnum();
        RuleFor(command => command.Notes).MaximumLength(2000);
    }
}

public sealed class ReportBookingHandler(
    IBookingRepository bookingRepository,
    IBookingReportRepository bookingReportRepository,
    IExecutionContext executionContext
) : IRequestHandler<ReportBookingCommand, Result<BookingReportId>>
{
    public async Task<Result<BookingReportId>> Handle(ReportBookingCommand command, CancellationToken cancellationToken)
    {
        var tenantId = executionContext.UserInfo.TenantId;
        var reporterUserId = executionContext.UserInfo.Id;
        if (tenantId is null || reporterUserId is null)
        {
            return Result<BookingReportId>.Unauthorized("Authentication is required.");
        }

        // Tenant-scoped existence check: any workspace user may report any booking inside their
        // tenant (so an Admin reporting a Member's booking is permitted). The Booking aggregate's
        // ITenantScopedEntity query filter limits the lookup to the caller's tenant.
        var booking = await bookingRepository.GetByIdAsync(command.Id, cancellationToken);
        if (booking is null || booking.TenantId != tenantId)
        {
            return Result<BookingReportId>.NotFound($"Booking '{command.Id}' was not found.");
        }

        var report = BookingReport.Create(tenantId, booking.Id, reporterUserId, command.ReasonCode, command.Notes);
        await bookingReportRepository.AddAsync(report, cancellationToken);

        return report.Id;
    }
}
