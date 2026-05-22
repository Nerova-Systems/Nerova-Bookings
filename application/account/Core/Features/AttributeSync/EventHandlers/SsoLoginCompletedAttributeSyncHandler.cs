using Account.Features.AttributeSync.Infrastructure;
using Account.Features.Sso.Events;
using MediatR;

namespace Account.Features.AttributeSync.EventHandlers;

/// <summary>
///     Reacts to a successful SSO login by applying all enabled attribute sync rules for the org.
///     Runs in-process within the same HTTP request scope as the SSO complete handler.
/// </summary>
public sealed class SsoLoginCompletedAttributeSyncHandler(
    AttributeSyncService syncService
) : INotificationHandler<SsoLoginCompletedEvent>
{
    public async Task Handle(SsoLoginCompletedEvent notification, CancellationToken cancellationToken)
    {
        await syncService.ApplyAsync(
            notification.MembershipId,
            notification.OrgTenantId,
            notification.Source,
            notification.Claims,
            cancellationToken
        );
    }
}
