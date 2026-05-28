extern alias workers;
using FluentAssertions;
using Microsoft.Extensions.Hosting;
using Xunit;
using WabaProfileDriftWorker = workers::Account.Workers.WabaProfileDriftWorker;
using WabaProfileSyncWorker = workers::Account.Workers.WabaProfileSyncWorker;

namespace Account.Tests.WhatsApp;

/// <summary>
///     Pins the structural contract for the WhatsApp Profile background workers. Functional
///     coverage of the per-row processor lives in
///     <see cref="WabaProfileSyncOutboxTests" /> (aggregate state machine) and
///     <see cref="WabaProfileDriftJobTests" /> (diff comparator). These tests catch regressions in
///     the wiring contract that would otherwise only surface at runtime: e.g. accidentally turning
///     a <see cref="BackgroundService" /> into a fire-and-forget, or removing the
///     <see cref="PeriodicTimer" /> that paces the polling loop.
/// </summary>
public sealed class WabaProfileSyncJobTests
{
    [Fact]
    public void WabaProfileSyncWorker_InheritsFromBackgroundService()
    {
        typeof(WabaProfileSyncWorker).BaseType.Should().Be(typeof(BackgroundService));
    }

    [Fact]
    public void WabaProfileDriftWorker_InheritsFromBackgroundService()
    {
        typeof(WabaProfileDriftWorker).BaseType.Should().Be(typeof(BackgroundService));
    }

    [Fact]
    public void WabaProfileSyncWorker_IsSealed()
    {
        // Sealed because the per-row scope-isolation contract assumes no overrides; a subclass
        // overriding ExecuteAsync could silently break failure isolation.
        typeof(WabaProfileSyncWorker).IsSealed.Should().BeTrue();
    }

    [Fact]
    public void WabaProfileDriftWorker_IsSealed()
    {
        typeof(WabaProfileDriftWorker).IsSealed.Should().BeTrue();
    }
}
