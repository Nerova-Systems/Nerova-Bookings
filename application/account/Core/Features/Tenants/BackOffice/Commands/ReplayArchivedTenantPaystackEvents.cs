using Account.Features.Tenants.Domain;
using JetBrains.Annotations;
using SharedKernel.Cqrs;
using SharedKernel.Domain;

namespace Account.Features.Tenants.BackOffice.Commands;

[PublicAPI]
public sealed record ReplayArchivedTenantPaystackEventsCommand : ICommand, IRequest<Result<ReplayArchivedTenantPaystackEventsResponse>>
{
    [JsonIgnore]
    public TenantId TenantId { get; init; } = null!;
}

[PublicAPI]
public sealed record ReplayArchivedTenantPaystackEventsResponse(int BillingEventsAppended, DateTimeOffset ReplayedAt);

public sealed class ReplayArchivedTenantPaystackEventsHandler(ITenantRepository tenantRepository, TimeProvider timeProvider)
    : IRequestHandler<ReplayArchivedTenantPaystackEventsCommand, Result<ReplayArchivedTenantPaystackEventsResponse>>
{
    public async Task<Result<ReplayArchivedTenantPaystackEventsResponse>> Handle(ReplayArchivedTenantPaystackEventsCommand command, CancellationToken cancellationToken)
    {
        var tenant = await tenantRepository.GetByIdUnfilteredAsync(command.TenantId, cancellationToken);
        if (tenant is null) return Result<ReplayArchivedTenantPaystackEventsResponse>.NotFound($"Tenant with id '{command.TenantId}' not found.");

        return new ReplayArchivedTenantPaystackEventsResponse(0, timeProvider.GetUtcNow());
    }
}
