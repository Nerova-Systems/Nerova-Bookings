using FluentAssertions;
using Main.Features.EventTypes.Domain;
using Main.Features.ManagedEventTypes.Services;
using Main.Features.ManagedEventTypes.Shared;
using Main.Features.Schedules.Domain;
using NSubstitute;
using SharedKernel.Domain;
using Xunit;

namespace Main.Tests.ManagedEventTypes;

public sealed class ManagedEventTypePropagatorTests
{
    private static EventType MakeParent(string[]? unlockedFields = null)
    {
        var parent = EventType.Create(
            TenantId.NewId(), UserId.NewId(), "Parent", "parent", null, 30, false,
            ScheduleId.NewId(), 0, 0, 30, 60, null, null, null,
            teamId: TenantId.NewId()
        );
        parent.UpdateUnlockedFields(unlockedFields ?? []);
        return parent;
    }

    [Fact]
    public async Task PropagateAsync_WhenParentHasChildren_ShouldCallUpdateForEach()
    {
        var parent = MakeParent();
        parent.Update("New title", "parent", null, 45, false, parent.ScheduleId, 0, 0, 30, 60, null, null, null);
        var child1 = parent.CreateChildReplica(UserId.NewId());
        var child2 = parent.CreateChildReplica(UserId.NewId());

        var repository = Substitute.For<IEventTypeRepository>();
        repository.GetChildrenAsync(parent.Id, Arg.Any<CancellationToken>())
            .Returns([child1, child2]);

        var propagator = new ManagedEventTypePropagator();
        var count = await propagator.PropagateAsync(parent, repository, CancellationToken.None);

        count.Should().Be(2);
        repository.Received(1).Update(child1);
        repository.Received(1).Update(child2);
    }

    [Fact]
    public async Task PropagateAsync_WhenNoChildren_ShouldReturnZero()
    {
        var parent = MakeParent();
        var repository = Substitute.For<IEventTypeRepository>();
        repository.GetChildrenAsync(parent.Id, Arg.Any<CancellationToken>())
            .Returns([]);

        var propagator = new ManagedEventTypePropagator();
        var count = await propagator.PropagateAsync(parent, repository, CancellationToken.None);

        count.Should().Be(0);
    }

    [Fact]
    public async Task PropagateAsync_WhenFieldIsLocked_ShouldOverrideChildField()
    {
        var parent = MakeParent(); // no unlocked fields = all locked
        parent.Update("Final title", "parent", null, 60, false, parent.ScheduleId, 0, 0, 30, 60, null, null, null);
        var child = parent.CreateChildReplica(UserId.NewId());

        var repository = Substitute.For<IEventTypeRepository>();
        repository.GetChildrenAsync(parent.Id, Arg.Any<CancellationToken>())
            .Returns([child]);

        var propagator = new ManagedEventTypePropagator();
        await propagator.PropagateAsync(parent, repository, CancellationToken.None);

        child.Title.Should().Be("Final title");
        child.DurationMinutes.Should().Be(60);
    }
}
