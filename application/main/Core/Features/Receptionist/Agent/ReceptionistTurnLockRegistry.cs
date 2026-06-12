using System.Collections.Concurrent;

namespace Main.Features.Receptionist.Agent;

/// <summary>
///     Serializes receptionist turns per conversation (spec R8): customers double-text, and two
///     concurrent turns over one thread would corrupt the serialized session and double-reply.
///     In-process locking is sufficient because webhook processing for a given Meta phone number is
///     handled by this application instance; revisit with a distributed lock if webhook ingestion is
///     ever scaled out per tenant.
/// </summary>
public sealed class ReceptionistTurnLockRegistry
{
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new(StringComparer.Ordinal);

    public async Task<IDisposable> AcquireAsync(string conversationId, CancellationToken cancellationToken)
    {
        var semaphore = _locks.GetOrAdd(conversationId, _ => new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync(cancellationToken);
        return new Releaser(semaphore);
    }

    private sealed class Releaser(SemaphoreSlim semaphore) : IDisposable
    {
        public void Dispose()
        {
            semaphore.Release();
        }
    }
}
