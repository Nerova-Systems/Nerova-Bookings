using TickerQ.Utilities.Base;
using TickerQ.Utilities.Interfaces;

namespace Main.Features.Autonomy.Jobs;

/// <summary>
///     Cron entry point for the autonomy runner (design §1): dispatches one detection/execution tick
///     through the command pipeline so unit-of-work, telemetry, and error semantics match every other
///     command in the system.
/// </summary>
public sealed class AutonomyTickerJob(IMediator mediator) : ITickerFunction
{
    public async Task ExecuteAsync(TickerFunctionContext context, CancellationToken ct)
    {
        _ = context;
        await mediator.Send(new Commands.RunAutonomyTickCommand(), ct);
    }
}
