using Account.Features.WhatsApp.Domain;
using JetBrains.Annotations;
using SharedKernel.Domain;

namespace Account.Features.WhatsApp.Queries;

[PublicAPI]
public sealed record GetWabaDisplayNameStatusQuery(TenantId TenantId)
    : IRequest<WabaDisplayNameStatusResponse?>;

[PublicAPI]
public sealed record WabaDisplayNameStatusResponse(
    WabaDisplayNameStatus Status,
    string? RequestedDisplayName,
    string? VerifiedName,
    DateTimeOffset? RequestedAt,
    DateTimeOffset? LastCheckedAt
);

/// <summary>
///     Reads the Phase 7c display-name review state for the current tenant. Returns
///     <see langword="null" /> when the tenant has no <c>WabaConfiguration</c>; the endpoint maps
///     that to a 404 so the UI can distinguish "never onboarded" from "no pending request".
/// </summary>
public sealed class GetWabaDisplayNameStatusHandler(IWabaConfigurationRepository repository)
    : IRequestHandler<GetWabaDisplayNameStatusQuery, WabaDisplayNameStatusResponse?>
{
    public async Task<WabaDisplayNameStatusResponse?> Handle(
        GetWabaDisplayNameStatusQuery query,
        CancellationToken cancellationToken)
    {
        var config = await repository.GetByTenantIdAsync(query.TenantId, cancellationToken);
        if (config is null)
        {
            return null;
        }

        return new WabaDisplayNameStatusResponse(
            Status: config.DisplayNameStatus,
            RequestedDisplayName: config.RequestedDisplayName,
            VerifiedName: config.VerifiedName,
            RequestedAt: config.DisplayNameReviewRequestedAt,
            LastCheckedAt: config.DisplayNameLastCheckedAt
        );
    }
}
