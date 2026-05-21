using Account.Features.AuditLog.Domain;
using Account.Features.Permissions.Domain;
using Account.Features.Permissions.Pipeline;
using FluentValidation;
using JetBrains.Annotations;
using SharedKernel.Cqrs;
using SharedKernel.Domain;

namespace Account.Features.AuditLog.Queries;

/// <summary>
///     Returns a paginated, filtered page of <see cref="AuditLogEntry" /> records for the current tenant.
///     Requires <c>AuditLog.Read</c> permission — Owner and Admin roles only.
/// </summary>
[PublicAPI]
[RequirePermission(PermissionResource.AuditLog, PermissionAction.Read)]
public sealed record GetAuditLogQuery(
    int PageOffset = 0,
    int PageSize = 25,
    string? ActorUserId = null,
    string? Resource = null,
    string? Action = null,
    DateTimeOffset? FromDate = null,
    DateTimeOffset? ToDate = null
) : IRequest<Result<AuditLogResponse>>;

[PublicAPI]
public sealed record AuditLogResponse(
    int TotalCount,
    int PageSize,
    int TotalPages,
    int CurrentPageOffset,
    AuditLogEntryResponse[] Entries);

[PublicAPI]
public sealed record AuditLogEntryResponse(
    AuditLogEntryId Id,
    DateTimeOffset CreatedAt,
    string? ActorUserId,
    string ActorEmail,
    string Resource,
    string Action,
    string? ResourceId,
    string? Metadata,
    string? IpAddress,
    string? UserAgent);

public sealed class GetAuditLogQueryValidator : AbstractValidator<GetAuditLogQuery>
{
    public GetAuditLogQueryValidator()
    {
        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100)
            .WithMessage("Page size must be between 1 and 100.");
        RuleFor(x => x.PageOffset)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Page offset must be 0 or greater.");
    }
}

public sealed class GetAuditLogHandler(IAuditLogRepository repository)
    : IRequestHandler<GetAuditLogQuery, Result<AuditLogResponse>>
{
    public async Task<Result<AuditLogResponse>> Handle(GetAuditLogQuery query, CancellationToken cancellationToken)
    {
        UserId? actorUserId = null;
        if (query.ActorUserId is not null)
        {
            if (!UserId.TryParse(query.ActorUserId, out actorUserId))
                return Result<AuditLogResponse>.BadRequest("Invalid actor user ID format.");
        }

        var filter = new AuditLogFilter(
            ActorUserId: actorUserId,
            Resource: query.Resource,
            Action: query.Action,
            FromDate: query.FromDate,
            ToDate: query.ToDate);

        var (entries, totalCount) = await repository.GetPagedAsync(filter, query.PageOffset, query.PageSize, cancellationToken);

        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling((double)totalCount / query.PageSize);

        if (query.PageOffset > 0 && query.PageOffset >= totalPages)
            return Result<AuditLogResponse>.BadRequest($"The page offset {query.PageOffset} is greater than the total number of pages.");

        var entryResponses = entries.Select(e => new AuditLogEntryResponse(
            e.Id,
            e.CreatedAt,
            e.ActorUserId?.ToString(),
            e.ActorEmail,
            e.Resource,
            e.Action,
            e.ResourceId,
            e.Metadata,
            e.IpAddress,
            e.UserAgent
        )).ToArray();

        return new AuditLogResponse(totalCount, query.PageSize, totalPages, query.PageOffset, entryResponses);
    }
}
