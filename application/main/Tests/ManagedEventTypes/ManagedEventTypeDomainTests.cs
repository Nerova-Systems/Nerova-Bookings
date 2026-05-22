using FluentAssertions;
using Main.Features.EventTypes.Domain;
using Main.Features.ManagedEventTypes.Shared;
using Main.Features.Schedules.Domain;
using SharedKernel.Domain;
using Xunit;

namespace Main.Tests.ManagedEventTypes;

public sealed class ManagedEventTypeDomainTests
{
    private static EventType MakeTeamEventType(TenantId? teamId = null)
    {
        var tenantId = TenantId.NewId();
        var team = teamId ?? TenantId.NewId();
        return EventType.Create(
            tenantId,
            UserId.NewId(),
            "Intro call",
            "intro-call",
            null,
            30,
            false,
            ScheduleId.NewId(),
            0, 0, 30, 60,
            null, null, null,
            team
        );
    }

    [Fact]
    public void EnsureCanBeManagedTemplate_WhenTeamScoped_ShouldSucceed()
    {
        var template = MakeTeamEventType();

        var result = template.EnsureCanBeManagedTemplate();

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void EnsureCanBeManagedTemplate_WhenNotTeamScoped_ShouldReturnBadRequest()
    {
        var eventType = EventType.Create(
            TenantId.NewId(), UserId.NewId(), "Solo", "solo", null, 30, false,
            ScheduleId.NewId(), 0, 0, 30, 60, null, null, null
        );

        var result = eventType.EnsureCanBeManagedTemplate();

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public void CreateChildReplica_ShouldCopyFieldsAndSetParentId()
    {
        var parent = MakeTeamEventType();
        var memberId = UserId.NewId();

        var child = parent.CreateChildReplica(memberId);

        child.ParentEventTypeId.Should().Be(parent.Id);
        child.OwnerUserId.Should().Be(memberId);
        child.TeamId.Should().Be(parent.TeamId);
        child.Title.Should().Be(parent.Title);
        child.Slug.Should().Be(parent.Slug);
        child.DurationMinutes.Should().Be(parent.DurationMinutes);
    }

    [Fact]
    public void PropagateFromParent_WhenFieldIsLocked_ShouldOverrideChildValue()
    {
        var parent = MakeTeamEventType();
        var child = parent.CreateChildReplica(UserId.NewId());

        // Update parent title; child has same (unlocked = empty → all locked)
        parent.Update("New title", "new-slug", null, 45, false,
            parent.ScheduleId, 0, 0, 30, 60, null, null, null
        );

        child.PropagateFromParent(parent);

        child.Title.Should().Be("New title");
        child.DurationMinutes.Should().Be(45);
    }

    [Fact]
    public void PropagateFromParent_WhenFieldIsUnlocked_ShouldRetainChildValue()
    {
        var parent = MakeTeamEventType();
        parent.UpdateUnlockedFields([ManagedEventTypeFields.Title]);
        var child = parent.CreateChildReplica(UserId.NewId());

        // Give child a different title
        child.Update("My custom title", "intro-call", null, 30, false,
            child.ScheduleId, 0, 0, 30, 60, null, null, null
        );

        // Parent changes title
        parent.Update("Parent new title", "intro-call", null, 30, false,
            parent.ScheduleId, 0, 0, 30, 60, null, null, null
        );

        child.PropagateFromParent(parent);

        child.Title.Should().Be("My custom title");
    }

    [Fact]
    public void CheckCanUpdateFields_WhenFieldUnlocked_ShouldSucceed()
    {
        var parent = MakeTeamEventType();
        parent.UpdateUnlockedFields([ManagedEventTypeFields.Title, ManagedEventTypeFields.Description]);
        var child = parent.CreateChildReplica(UserId.NewId());

        var result = child.CheckCanUpdateFields([ManagedEventTypeFields.Title]);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void CheckCanUpdateFields_WhenFieldLocked_ShouldReturnForbidden()
    {
        var parent = MakeTeamEventType();
        var child = parent.CreateChildReplica(UserId.NewId());

        var result = child.CheckCanUpdateFields([ManagedEventTypeFields.DurationMinutes]);

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public void UpdateUnlockedFields_ShouldTrimAndFilterEmpties()
    {
        var template = MakeTeamEventType();

        template.UpdateUnlockedFields(["  title  ", "", "   ", "description"]);

        template.UnlockedFields.Should().BeEquivalentTo("title", "description");
    }

    [Fact]
    public void EnsureCanBeManagedTemplate_WhenAlreadyChild_ShouldReturnBadRequest()
    {
        var parent = MakeTeamEventType();
        var child = parent.CreateChildReplica(UserId.NewId());

        var result = child.EnsureCanBeManagedTemplate();

        result.IsSuccess.Should().BeFalse();
    }
}
