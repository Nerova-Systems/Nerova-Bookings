using Account.Features.Tenants.Domain;
using JetBrains.Annotations;
using SharedKernel.Cqrs;
using SharedKernel.Domain;

namespace Account.Features.Tenants.BackOffice.Commands;

[PublicAPI]
public sealed record ReplayArchivedTenantStripeEventsCommand : ICommand, IRequest<Result<ReplayArchivedTenantStripeEventsResponse>>
{
    [JsonIgnore]
    public TenantId TenantId { get; init; } = null!;
}

[PublicAPI]
public sealed record ReplayArchivedTenantStripeEventsResponse(int BillingEventsAppended, DateTimeOffset ReplayedAt);

public sealed class ReplayArchivedTenantStripeEventsHandler(ITenantRepository tenantRepository, TimeProvider timeProvider)
    : IRequestHandler<ReplayArchivedTenantStripeEventsCommand, Result<ReplayArchivedTenantStripeEventsResponse>>
{
    public async Task<Result<ReplayArchivedTenantStripeEventsResponse>> Handle(ReplayArchivedTenantStripeEventsCommand command, CancellationToken cancellationToken)
    {
        var tenant = await tenantRepository.GetByIdUnfilteredAsync(command.TenantId, cancellationToken);
        if (tenant is null) return Result<ReplayArchivedTenantStripeEventsResponse>.NotFound($"Tenant with id '{command.TenantId}' not found.");

        return new ReplayArchivedTenantStripeEventsResponse(0, timeProvider.GetUtcNow());
    }
}
