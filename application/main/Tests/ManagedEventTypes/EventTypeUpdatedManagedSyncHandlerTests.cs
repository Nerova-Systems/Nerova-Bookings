using FluentAssertions;
using Main.Features;
using Main.Features.EventTypes.Domain;
using Main.Features.ManagedEventTypes.EventHandlers;
using Main.Features.ManagedEventTypes.Services;
using Main.Features.Schedules.Domain;
using NSubstitute;
using SharedKernel.Domain;
using SharedKernel.Telemetry;
using SharedKernel.Tests.Telemetry;
using Xunit;

namespace Main.Tests.ManagedEventTypes;

public sealed class EventTypeUpdatedManagedSyncHandlerTests
{
    private static EventType MakeParent()
    {
        return EventType.Create(
            TenantId.NewId(), UserId.NewId(), "Parent", "parent", null, 30, false,
            ScheduleId.NewId(), 0, 0, 30, 60, null, null, null,
            TenantId.NewId()
        );
    }

    [Fact]
    public async Task SyncChildrenAsync_WhenParentHasChildren_ShouldReturnChildCount()
    {
        var parent = MakeParent();
        var child = parent.CreateChildReplica(UserId.NewId());
        var repository = Substitute.For<IEventTypeRepository>();
        repository.GetByIdAsync(parent.Id, Arg.Any<CancellationToken>()).Returns(parent);
        repository.GetChildrenAsync(parent.Id, Arg.Any<CancellationToken>()).Returns([child]);

        var spy = new TelemetryEventsCollectorSpy(new TelemetryEventsCollector());
        var handler = new EventTypeUpdatedManagedSyncHandler(repository, new ManagedEventTypePropagator(), spy);

        var count = await handler.SyncChildrenAsync(parent.Id, CancellationToken.None);

        count.Should().Be(1);
        spy.CollectedEvents.OfType<ManagedEventTypeSynced>().Should().ContainSingle();
    }

    [Fact]
    public async Task SyncChildrenAsync_WhenEventTypeNotFound_ShouldReturnZero()
    {
        var repository = Substitute.For<IEventTypeRepository>();
        var parentId = EventTypeId.NewId();
        repository.GetByIdAsync(parentId, Arg.Any<CancellationToken>()).Returns((EventType?)null);

        var spy = new TelemetryEventsCollectorSpy(new TelemetryEventsCollector());
        var handler = new EventTypeUpdatedManagedSyncHandler(repository, new ManagedEventTypePropagator(), spy);

        var count = await handler.SyncChildrenAsync(parentId, CancellationToken.None);

        count.Should().Be(0);
        spy.CollectedEvents.Should().BeEmpty();
    }

    [Fact]
    public async Task SyncChildrenAsync_WhenCalledOnChild_ShouldReturnZeroAndNotSync()
    {
        var grandParent = MakeParent();
        var child = grandParent.CreateChildReplica(UserId.NewId());
        var repository = Substitute.For<IEventTypeRepository>();
        repository.GetByIdAsync(child.Id, Arg.Any<CancellationToken>()).Returns(child);

        var spy = new TelemetryEventsCollectorSpy(new TelemetryEventsCollector());
        var handler = new EventTypeUpdatedManagedSyncHandler(repository, new ManagedEventTypePropagator(), spy);

        var count = await handler.SyncChildrenAsync(child.Id, CancellationToken.None);

        count.Should().Be(0);
        spy.CollectedEvents.Should().BeEmpty();
    }
}
