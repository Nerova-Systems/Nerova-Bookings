using JetBrains.Annotations;
using Main.Features.ManagedEventTypes.Shared;
using Main.Features.Schedules.Domain;
using SharedKernel.Cqrs;
using SharedKernel.Domain;
using SharedKernel.StronglyTypedIds;

namespace Main.Features.EventTypes.Domain;

[IdPrefix("etype")]
[JsonConverter(typeof(StronglyTypedIdJsonConverter<string, EventTypeId>))]
public sealed record EventTypeId(string Value) : StronglyTypedUlid<EventTypeId>(Value)
{
    public override string ToString()
    {
        return Value;
    }
}

public sealed class EventType : SoftDeletableAggregateRoot<EventTypeId>, ITenantScopedEntity
{
    [UsedImplicitly]
    private EventType() : base(EventTypeId.NewId())
    {
        OwnerUserId = new UserId(string.Empty);
        Title = string.Empty;
        Slug = string.Empty;
        ScheduleId = new ScheduleId(string.Empty);
    }

    private EventType(
        TenantId tenantId,
        UserId ownerUserId,
        string title,
        string slug,
        string? description,
        int durationMinutes,
        bool hidden,
        ScheduleId scheduleId,
        int beforeEventBufferMinutes,
        int afterEventBufferMinutes,
        int slotIntervalMinutes,
        int minimumBookingNoticeMinutes,
        string? locationType,
        string? locationValue,
        EventTypeSettings? settings
    ) : base(EventTypeId.NewId())
    {
        TenantId = tenantId;
        OwnerUserId = ownerUserId;
        Update(title, slug, description, durationMinutes, hidden, scheduleId, beforeEventBufferMinutes, afterEventBufferMinutes, slotIntervalMinutes, minimumBookingNoticeMinutes, locationType, locationValue, settings);
    }

    private EventType(
        EventTypeId parentId,
        TenantId tenantId,
        UserId ownerUserId,
        string title,
        string slug,
        string? description,
        int durationMinutes,
        bool hidden,
        ScheduleId scheduleId,
        int beforeEventBufferMinutes,
        int afterEventBufferMinutes,
        int slotIntervalMinutes,
        int minimumBookingNoticeMinutes,
        string? locationType,
        string? locationValue,
        EventTypeSettings? settings,
        TenantId? teamId,
        string[] unlockedFields
    ) : base(EventTypeId.NewId())
    {
        TenantId = tenantId;
        OwnerUserId = ownerUserId;
        ParentEventTypeId = parentId;
        TeamId = teamId;
        UnlockedFields = unlockedFields.ToArray();
        Update(title, slug, description, durationMinutes, hidden, scheduleId, beforeEventBufferMinutes, afterEventBufferMinutes, slotIntervalMinutes, minimumBookingNoticeMinutes, locationType, locationValue, settings);
    }

    public UserId OwnerUserId { get; private set; }

    public string Title { get; private set; } = string.Empty;

    public string Slug { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public int DurationMinutes { get; private set; }

    public bool Hidden { get; private set; }

    public ScheduleId ScheduleId { get; private set; } = null!;

    public int BeforeEventBufferMinutes { get; private set; }

    public int AfterEventBufferMinutes { get; private set; }

    public int SlotIntervalMinutes { get; private set; }

    public int MinimumBookingNoticeMinutes { get; private set; }

    public string? LocationType { get; private set; }

    public string? LocationValue { get; private set; }

    public EventTypeSettings Settings { get; private set; } = new();

    public bool IsInstantEvent { get; private set; }

    public bool AssignAllTeamMembers { get; private set; }

    public bool HideOrganizerEmail { get; private set; }

    public bool BookingRequiresAuthentication { get; private set; }

    /// <summary>
    ///     Nullable reference to a secondary email's owning user. No FK constraint enforced; relationship is logical.
    /// </summary>
    public UserId? SecondaryEmailUserId { get; private set; }

    public int[] DurationOptions => Settings.DurationOptions.Length == 0 ? [DurationMinutes] : Settings.DurationOptions;

    public SchedulingType SchedulingType { get; private set; } = SchedulingType.Default;

    /// <summary>
    ///     When non-null, references a Tenant of TenantKind.Team. When null, the aggregate is owned by the existing
    ///     user/solo scope.
    /// </summary>
    public TenantId? TeamId { get; private set; }

    /// <summary>
    ///     When non-null, this event type is a child replica managed by a parent template.
    /// </summary>
    public EventTypeId? ParentEventTypeId { get; }

    /// <summary>
    ///     Field names that the assigned member is allowed to override on their child replica.
    ///     Fields NOT in this list are locked and propagated from the parent template.
    /// </summary>
    public string[] UnlockedFields { get; private set; } = [];

    public TenantId TenantId { get; } = new(0);

    /// <summary>
    ///     Changes the scheduling type of this event type.
    ///     Only valid for team-scoped event types (TeamId must be set).
    /// </summary>
    public Result SetSchedulingType(SchedulingType schedulingType)
    {
        if (schedulingType != SchedulingType.Default && TeamId is null)
        {
            return Result.BadRequest("Scheduling type can only be changed on team-scoped event types.");
        }

        SchedulingType = schedulingType;
        return Result.Success();
    }

    /// <summary>
    ///     Assigns this event type to a team.
    /// </summary>
    /// <remarks>
    ///     The command layer is responsible for verifying that <paramref name="teamId" /> references a Tenant of
    ///     TenantKind.Team. This aggregate cannot verify TenantKind itself.
    /// </remarks>
    public void AssignToTeam(TenantId teamId)
    {
        // Command layer must ensure teamId refers to a TenantKind.Team tenant.
        TeamId = teamId;
    }

    /// <summary>
    ///     Removes the team association, reverting the event type to user/solo scope.
    /// </summary>
    public void RemoveFromTeam()
    {
        TeamId = null;
    }

    /// <summary>
    ///     Validates that this event type can serve as a managed template.
    ///     Returns a bad-request result if validation fails.
    /// </summary>
    public Result EnsureCanBeManagedTemplate()
    {
        if (TeamId is null)
        {
            return Result.BadRequest("Managed templates must be team-scoped.");
        }

        if (ParentEventTypeId is not null)
        {
            return Result.BadRequest("A child event type cannot become a managed template.");
        }

        return Result.Success();
    }

    /// <summary>
    ///     Creates a child replica of this template for the specified team member.
    ///     Copies all field values; the member may later override the <see cref="UnlockedFields" />.
    /// </summary>
    public EventType CreateChildReplica(UserId memberUserId)
    {
        return new EventType(
            Id, TenantId, memberUserId, Title, Slug, Description, DurationMinutes, Hidden, ScheduleId,
            BeforeEventBufferMinutes, AfterEventBufferMinutes, SlotIntervalMinutes, MinimumBookingNoticeMinutes,
            LocationType, LocationValue, Settings, TeamId, UnlockedFields
        );
    }

    /// <summary>
    ///     Propagates the parent template's locked field values into this child replica.
    ///     Fields listed in <see cref="UnlockedFields" /> retain the child's own values.
    /// </summary>
    public void PropagateFromParent(EventType parent)
    {
        var unlocked = UnlockedFields;
        Update(
            IsLocked(unlocked, ManagedEventTypeFields.Title) ? parent.Title : Title,
            IsLocked(unlocked, ManagedEventTypeFields.Slug) ? parent.Slug : Slug,
            IsLocked(unlocked, ManagedEventTypeFields.Description) ? parent.Description : Description,
            IsLocked(unlocked, ManagedEventTypeFields.DurationMinutes) ? parent.DurationMinutes : DurationMinutes,
            IsLocked(unlocked, ManagedEventTypeFields.Hidden) ? parent.Hidden : Hidden,
            IsLocked(unlocked, ManagedEventTypeFields.ScheduleId) ? parent.ScheduleId : ScheduleId,
            IsLocked(unlocked, ManagedEventTypeFields.BeforeEventBufferMinutes) ? parent.BeforeEventBufferMinutes : BeforeEventBufferMinutes,
            IsLocked(unlocked, ManagedEventTypeFields.AfterEventBufferMinutes) ? parent.AfterEventBufferMinutes : AfterEventBufferMinutes,
            IsLocked(unlocked, ManagedEventTypeFields.SlotIntervalMinutes) ? parent.SlotIntervalMinutes : SlotIntervalMinutes,
            IsLocked(unlocked, ManagedEventTypeFields.MinimumBookingNoticeMinutes) ? parent.MinimumBookingNoticeMinutes : MinimumBookingNoticeMinutes,
            IsLocked(unlocked, ManagedEventTypeFields.LocationType) ? parent.LocationType : LocationType,
            IsLocked(unlocked, ManagedEventTypeFields.LocationValue) ? parent.LocationValue : LocationValue,
            IsLocked(unlocked, ManagedEventTypeFields.Settings) ? parent.Settings : Settings
        );
        UnlockedFields = parent.UnlockedFields.ToArray();
    }

    /// <summary>
    ///     Checks that all requested field names are unlocked on this child replica.
    ///     Returns <see cref="Result.Forbidden" /> listing any locked fields.
    /// </summary>
    public Result CheckCanUpdateFields(IEnumerable<string> requestedFields)
    {
        var locked = requestedFields
            .Where(f => !UnlockedFields.Contains(f, StringComparer.OrdinalIgnoreCase))
            .ToArray();
        return locked.Length > 0
            ? Result.Forbidden($"Fields {string.Join(", ", locked)} are locked by the managed template.")
            : Result.Success();
    }

    /// <summary>
    ///     Updates the unlocked fields list on a parent template.
    ///     Should be followed by propagation to all children.
    /// </summary>
    public void UpdateUnlockedFields(string[] unlockedFields)
    {
        UnlockedFields = unlockedFields.Select(f => f.Trim()).Where(f => f.Length > 0).ToArray();
    }

    private static bool IsLocked(string[] unlocked, string fieldName)
    {
        return !unlocked.Contains(fieldName, StringComparer.OrdinalIgnoreCase);
    }

    public static EventType Create(
        TenantId tenantId,
        UserId ownerUserId,
        string title,
        string slug,
        string? description,
        int durationMinutes,
        bool hidden,
        ScheduleId scheduleId,
        int beforeEventBufferMinutes,
        int afterEventBufferMinutes,
        int slotIntervalMinutes,
        int minimumBookingNoticeMinutes,
        string? locationType,
        string? locationValue,
        EventTypeSettings? settings,
        TenantId? teamId = null
    )
    {
        var eventType = new EventType(tenantId, ownerUserId, title, slug, description, durationMinutes, hidden, scheduleId, beforeEventBufferMinutes, afterEventBufferMinutes, slotIntervalMinutes, minimumBookingNoticeMinutes, locationType, locationValue, settings);
        if (teamId is not null) eventType.AssignToTeam(teamId);
        return eventType;
    }

    public void Update(
        string title,
        string slug,
        string? description,
        int durationMinutes,
        bool hidden,
        ScheduleId scheduleId,
        int beforeEventBufferMinutes,
        int afterEventBufferMinutes,
        int slotIntervalMinutes,
        int minimumBookingNoticeMinutes,
        string? locationType,
        string? locationValue,
        EventTypeSettings? settings
    )
    {
        Title = title.Trim();
        Slug = slug.Trim().ToLowerInvariant();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        DurationMinutes = durationMinutes;
        Hidden = hidden;
        ScheduleId = scheduleId;
        BeforeEventBufferMinutes = beforeEventBufferMinutes;
        AfterEventBufferMinutes = afterEventBufferMinutes;
        SlotIntervalMinutes = slotIntervalMinutes;
        MinimumBookingNoticeMinutes = minimumBookingNoticeMinutes;
        LocationType = string.IsNullOrWhiteSpace(locationType) ? null : locationType.Trim();
        LocationValue = string.IsNullOrWhiteSpace(locationValue) ? null : locationValue.Trim();
        Settings = EventTypeSettings.Normalize(settings, DurationMinutes, LocationType, LocationValue);
    }

    public bool ConsumePrivateLink(string? privateLink)
    {
        if (string.IsNullOrWhiteSpace(privateLink))
        {
            return false;
        }

        var linkIndex = Array.FindIndex(
            Settings.PrivateLinks,
            link => string.Equals(link.Link, privateLink.Trim(), StringComparison.OrdinalIgnoreCase)
        );
        if (linkIndex < 0 || Settings.PrivateLinks[linkIndex].MaxUsageCount is null)
        {
            return false;
        }

        var privateLinks = Settings.PrivateLinks.ToArray();
        privateLinks[linkIndex] = privateLinks[linkIndex] with { UsageCount = privateLinks[linkIndex].UsageCount + 1 };
        Settings = Settings with { PrivateLinks = privateLinks };
        return true;
    }

    public void UpdateSettings(EventTypeSettings settings)
    {
        Settings = EventTypeSettings.Normalize(settings, DurationMinutes, LocationType, LocationValue);
    }
    /// <summary>
    ///     Replaces only the location fields on this event type, leaving all other state untouched.
    ///     Used by bulk-apply commands so the caller does not need to re-send the full Update payload.
    /// </summary>
    public void SetLocation(string? locationType, string? locationValue)
    {
        LocationType = string.IsNullOrWhiteSpace(locationType) ? null : locationType.Trim();
        LocationValue = string.IsNullOrWhiteSpace(locationValue) ? null : locationValue.Trim();
        Settings = EventTypeSettings.Normalize(Settings, DurationMinutes, LocationType, LocationValue);
    }

    public void SetSettings(EventTypeSettings settings)
    {
        Settings = EventTypeSettings.Normalize(settings, DurationMinutes, LocationType, LocationValue);
    }

    public void SetIsInstantEvent(bool isInstantEvent)
    {
        IsInstantEvent = isInstantEvent;
    }

    public void SetAssignAllTeamMembers(bool assignAllTeamMembers)
    {
        AssignAllTeamMembers = assignAllTeamMembers;
    }

    public void SetHideOrganizerEmail(bool hideOrganizerEmail)
    {
        HideOrganizerEmail = hideOrganizerEmail;
    }

    public void SetBookingRequiresAuthentication(bool bookingRequiresAuthentication)
    {
        BookingRequiresAuthentication = bookingRequiresAuthentication;
    }

    public void SetSecondaryEmailUserId(UserId? secondaryEmailUserId)
    {
        SecondaryEmailUserId = secondaryEmailUserId;
    }

    public HashedLink AddHashedLink(string hash, int? expiresAfterUses, DateTimeOffset? expiresAt)
    {
        return HashedLink.Create(TenantId, Id, hash, expiresAfterUses, expiresAt);
    }
}
