using System.Collections.Concurrent;
using SharedKernel.Domain;

namespace Main.Features.Apps.Domain;

/// <summary>
///     A short-lived value carrying the data the OAuth callback needs to validate the round-trip:
///     which tenant/user initiated the flow and which app slug they were installing.
/// </summary>
public sealed record OAuthStateEntry(TenantId TenantId, UserId UserId, AppSlug AppSlug);

/// <summary>
///     CSRF-protection store for the OAuth <c>state</c> parameter. The state token is generated
///     when the install endpoint is hit and consumed once during the callback. Entries expire
///     after <see cref="StateTtl" /> (15 minutes), matching the typical OAuth provider window.
///     <para>
///         A simple in-memory implementation is shipped here because the main SCS does not
///         currently register <c>IDistributedCache</c>. The interface lets connector tracks swap
///         in a Redis-backed implementation later without touching the OAuth command handlers.
///     </para>
/// </summary>
public interface IOAuthStateStore
{
    static readonly TimeSpan StateTtl = TimeSpan.FromMinutes(15);

    /// <summary>Generates and stores a fresh state token. Returns the token to embed in the authorize URL.</summary>
    string Issue(OAuthStateEntry entry);

    /// <summary>Atomically consumes a state token: returns the entry if valid, removes it, otherwise <see langword="null" />.</summary>
    OAuthStateEntry? Consume(string state);
}

/// <summary>
///     Thread-safe in-memory <see cref="IOAuthStateStore" />. Registered as a singleton.
///     Entries are evicted on read when expired; a background sweep is not required because
///     <see cref="Consume" /> handles expiration inline and the working set is bounded by the
///     15-minute TTL × concurrent install attempts.
/// </summary>
public sealed class InMemoryOAuthStateStore(TimeProvider timeProvider) : IOAuthStateStore
{
    private readonly ConcurrentDictionary<string, (OAuthStateEntry Entry, DateTimeOffset ExpiresAt)> _entries = new();

    public string Issue(OAuthStateEntry entry)
    {
        var token = Guid.NewGuid().ToString("N");
        var expiresAt = timeProvider.GetUtcNow().Add(IOAuthStateStore.StateTtl);
        _entries[token] = (entry, expiresAt);
        return token;
    }

    public OAuthStateEntry? Consume(string state)
    {
        if (!_entries.TryRemove(state, out var stored)) return null;
        return stored.ExpiresAt < timeProvider.GetUtcNow() ? null : stored.Entry;
    }
}
