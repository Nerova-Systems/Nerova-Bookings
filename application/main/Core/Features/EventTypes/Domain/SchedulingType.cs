using System.Text.Json.Serialization;

namespace Main.Features.EventTypes.Domain;

/// <summary>
///     Determines how a team event type distributes bookings across its hosts.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SchedulingType
{
    /// <summary>Standard single-host event type (no team scheduling).</summary>
    Default = 0,

    /// <summary>Bookings are distributed across available hosts one at a time.</summary>
    RoundRobin = 1,

    /// <summary>ALL hosts must be free for a slot to be offered.</summary>
    Collective = 2
}
