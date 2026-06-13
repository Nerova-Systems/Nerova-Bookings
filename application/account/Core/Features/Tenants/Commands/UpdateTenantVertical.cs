using Account.Features.Tenants.Domain;
using Account.Features.Users.Domain;
using JetBrains.Annotations;
using SharedKernel.Authentication;
using SharedKernel.Cqrs;
using SharedKernel.Domain;
using SharedKernel.ExecutionContext;
using SharedKernel.Telemetry;

namespace Account.Features.Tenants.Commands;

/// <summary>
///     Sets the tenant's business vertical (docs/vertical-template-fields-spec.md §1) — welcome step 1.
///     The vertical is the schema: it selects the fixed client field catalog in the main SCS. Owner-only;
///     changing it later is a support action, so this command is intended for initial selection.
/// </summary>
[PublicAPI]
public sealed record UpdateTenantVerticalCommand(NerovaVertical Vertical) : ICommand, IRequest<Result>;

public sealed class UpdateTenantVerticalHandler(
    ITenantRepository tenantRepository,
    IExecutionContext executionContext,
    ITelemetryEventsCollector events
) : IRequestHandler<UpdateTenantVerticalCommand, Result>
{
    public async Task<Result> Handle(UpdateTenantVerticalCommand command, CancellationToken cancellationToken)
    {
        if (executionContext.UserInfo.Role != nameof(UserRole.Owner))
        {
            return Result.Forbidden("Only owners are allowed to set the business vertical.");
        }

        var tenant = await tenantRepository.GetCurrentTenantAsync(cancellationToken);
        if (tenant is null)
        {
            return Result.Unauthorized("Tenant has been deleted.", responseHeaders: new Dictionary<string, string>
                {
                    { AuthenticationTokenHttpKeys.UnauthorizedReasonHeaderKey, nameof(UnauthorizedReason.TenantDeleted) }
                }
            );
        }

        tenant.SetVertical(command.Vertical);
        tenantRepository.Update(tenant);

        events.CollectEvent(new TenantVerticalSet(command.Vertical.ToString()));

        return Result.Success();
    }
}
