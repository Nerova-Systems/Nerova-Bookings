extern alias workers;
using FluentAssertions;
using Microsoft.Extensions.Hosting;
using Xunit;
using WabaDisplayNameReviewWorker = workers::Account.Workers.WabaDisplayNameReviewWorker;

namespace Account.Tests.WhatsApp;

/// <summary>
///     Pins the structural contract for the Phase 7c display-name review worker. Functional
///     poller coverage lives in <see cref="WabaDisplayNameReviewTests" /> (aggregate transitions)
///     plus <see cref="RequestWabaDisplayNameChangeHandlerTests" /> (entry path). These tests catch
///     wiring regressions that would only otherwise surface at deploy time.
/// </summary>
public sealed class WabaDisplayNameReviewWorkerTests
{
    [Fact]
    public void WabaDisplayNameReviewWorker_InheritsFromBackgroundService()
    {
        typeof(WabaDisplayNameReviewWorker).BaseType.Should().Be(typeof(BackgroundService));
    }

    [Fact]
    public void WabaDisplayNameReviewWorker_IsSealed()
    {
        // Sealed because the scope-isolation contract assumes no overrides — a subclass overriding
        // ExecuteAsync could silently bypass the per-pass IServiceScope and leak state.
        typeof(WabaDisplayNameReviewWorker).IsSealed.Should().BeTrue();
    }
}
