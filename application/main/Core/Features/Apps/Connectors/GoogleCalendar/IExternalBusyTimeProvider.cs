using SharedKernel.Domain;

namespace Main.Features.Apps.Connectors.GoogleCalendar;

/// <summary>
///     A single busy interval reported by an external calendar (e.g. Google Calendar free-busy
///     response). Slot calculators union these with internal <c>Booking</c> rows before producing
///     candidate slots so hosts never get double-booked across systems.
/// </summary>
public sealed record ExternalBusyTime(DateTimeOffset Start, DateTimeOffset End);

/// <summary>
///     Provider abstraction the slot calculators query before computing free slots for a host.
///     Each connector (Google Calendar, Outlook, …) registers one implementation. Implementations
///     return an empty array when the user has no credential for that provider so the slot
///     calculators can call <c>SelectMany</c> across all registered providers without branching.
///     <para>
///         <b>Wiring status.</b> The interface and the Google Calendar implementation ship in this
///         track; threading external busy times through <c>PublicSlotCalculator</c>,
///         <c>CollectiveSlotCalculator</c>, and <c>RoundRobinSlotCalculator</c> is deferred — that
///         change touches every caller and test of those calculators and is sequenced after the
///         connector itself lands cleanly.
///     </para>
/// </summary>
public interface IExternalBusyTimeProvider
{
    Task<IReadOnlyList<ExternalBusyTime>> GetBusyTimesAsync(
        TenantId tenantId,
        UserId userId,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken
    );
}
