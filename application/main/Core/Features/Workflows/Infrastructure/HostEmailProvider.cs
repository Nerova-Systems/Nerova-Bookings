using Main.Features.Workflows.Senders;
using SharedKernel.Domain;

namespace Main.Features.Workflows.Infrastructure;

/// <summary>
///     Adapts <see cref="IUserContactLookup" /> to the legacy <see cref="IHostEmailProvider" />
///     surface used by <c>DispatchWorkflowReminderJob</c>. Keeps that consumer unchanged while the
///     real cross-SCS lookup lives in <see cref="AccountDbUserContactLookup" />.
/// </summary>
public sealed class HostEmailProvider(IUserContactLookup contactLookup) : IHostEmailProvider
{
    public async Task<string?> GetEmailAsync(UserId userId, CancellationToken cancellationToken)
    {
        var contact = await contactLookup.GetAsync(userId, cancellationToken);
        return contact?.Email;
    }
}
