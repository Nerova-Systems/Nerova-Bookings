using System.Collections.Immutable;
using JetBrains.Annotations;
using SharedKernel.Domain;
using SharedKernel.StronglyTypedIds;

namespace Account.Features.SupportTickets.Domain;

public sealed class SupportTicket : AggregateRoot<SupportTicketId>, ITenantScopedEntity
{
    public const int SubjectMinLength = 4;
    public const int SubjectMaxLength = 200;
    public const int MessageBodyMinLength = 1;
    public const int MessageBodyMaxLength = 10_000;
    public const int CsatCommentMaxLength = 2_000;
    public const int ShortDisplayIdLength = 6;

    private SupportTicket(TenantId tenantId, string shortDisplayId, UserId reporterId, string reporterRoleSnapshot, string reporterEmailSnapshot, string subject, SupportTicketCategory category, DateTimeOffset lastActivityAt)
        : base(SupportTicketId.NewId())
    {
        TenantId = tenantId;
        ShortDisplayId = shortDisplayId;
        ReporterId = reporterId;
        ReporterRoleSnapshot = reporterRoleSnapshot;
        ReporterEmailSnapshot = reporterEmailSnapshot;
        Subject = subject;
        Category = category;
        Status = SupportTicketStatus.New;
        LastActivityAt = lastActivityAt;
        Messages = [];
        HistoryEvents = [];
    }

    public string ShortDisplayId { get; private set; }

    public UserId ReporterId { get; private set; }

    public string ReporterRoleSnapshot { get; private set; }

    [UsedImplicitly]
    public string ReporterEmailSnapshot { get; private set; }

    public string Subject { get; private set; }

    public SupportTicketCategory Category { get; private set; }

    public SupportTicketStatus Status { get; private set; }

    public BackOfficeStaffRef? Assignee { get; private set; }

    public DateTimeOffset LastActivityAt { get; private set; }

    public DateTimeOffset? ResolvedAt { get; private set; }

    public DateTimeOffset? ClosedAt { get; private set; }

    public SupportTicketCsat? Csat { get; private set; }

    public ImmutableArray<SupportMessage> Messages { get; private set; }

    public ImmutableArray<SupportTicketHistoryEvent> HistoryEvents { get; private set; }

    public TenantId TenantId { get; }

    public static SupportTicket Create(TenantId tenantId, string shortDisplayId, UserId reporterId, string reporterRoleSnapshot, string reporterEmailSnapshot, string subject, SupportTicketCategory category, DateTimeOffset now)
    {
        var ticket = new SupportTicket(tenantId, shortDisplayId, reporterId, reporterRoleSnapshot, reporterEmailSnapshot, subject, category, now);
        ticket.HistoryEvents = ticket.HistoryEvents.Add(
            SupportTicketHistoryEvent.Create(SupportTicketHistoryEventType.Created, SupportMessageAuthorKind.User, reporterEmailSnapshot, now)
        );
        return ticket;
    }

    public SupportMessage PostUserMessage(UserId authorUserId, string body, ImmutableArray<SupportMessageAttachment> attachments, DateTimeOffset now)
    {
        var message = SupportMessage.Create(authorUserId.Value, SupportMessageAuthorKind.User, ReporterEmailSnapshot, body, attachments, now);
        Messages = Messages.Add(message);
        LastActivityAt = now;
        HistoryEvents = HistoryEvents.Add(
            SupportTicketHistoryEvent.Create(SupportTicketHistoryEventType.MessagePosted, SupportMessageAuthorKind.User, ReporterEmailSnapshot, now, attachments.Length > 0)
        );

        // A user message always transitions to AwaitingAgent. From Closed it acts as a reopen.
        ApplyStatusTransition(SupportTicketStatus.AwaitingAgent, SupportMessageAuthorKind.User, ReporterEmailSnapshot, now, false);
        return message;
    }

    public SupportMessage PostStaffPublicMessage(BackOfficeStaffRef staff, string body, ImmutableArray<SupportMessageAttachment> attachments, DateTimeOffset now)
    {
        var message = SupportMessage.Create(staff.ObjectId, SupportMessageAuthorKind.Staff, staff.DisplayName, body, attachments, now);
        Messages = Messages.Add(message);
        LastActivityAt = now;
        HistoryEvents = HistoryEvents.Add(
            SupportTicketHistoryEvent.Create(SupportTicketHistoryEventType.MessagePosted, SupportMessageAuthorKind.Staff, staff.DisplayName, now, attachments.Length > 0)
        );

        ApplyStatusTransition(SupportTicketStatus.AwaitingUser, SupportMessageAuthorKind.Staff, staff.DisplayName, now, false);
        return message;
    }

    public SupportMessage PostStaffInternalNote(BackOfficeStaffRef staff, string body, ImmutableArray<SupportMessageAttachment> attachments, DateTimeOffset now)
    {
        var message = SupportMessage.Create(staff.ObjectId, SupportMessageAuthorKind.Internal, staff.DisplayName, body, attachments, now);
        Messages = Messages.Add(message);
        LastActivityAt = now;
        HistoryEvents = HistoryEvents.Add(
            SupportTicketHistoryEvent.Create(SupportTicketHistoryEventType.MessagePosted, SupportMessageAuthorKind.Internal, staff.DisplayName, now, attachments.Length > 0)
        );
        return message;
    }

    public bool ChangeStatusByStaff(SupportTicketStatus newStatus, BackOfficeStaffRef staff, DateTimeOffset now)
    {
        if (Status == newStatus) return false;
        if (newStatus is SupportTicketStatus.Closed)
        {
            // Staff cannot directly close; closing is end-user only (explicit close or CSAT submission).
            return false;
        }

        ApplyStatusTransition(newStatus, SupportMessageAuthorKind.Staff, staff.DisplayName, now, true);
        return true;
    }

    public bool MarkResolvedByUser(DateTimeOffset now)
    {
        if (Status is SupportTicketStatus.Resolved or SupportTicketStatus.Closed) return false;
        ApplyStatusTransition(SupportTicketStatus.Resolved, SupportMessageAuthorKind.User, ReporterEmailSnapshot, now, true);
        return true;
    }

    public bool MarkResolvedByStaff(BackOfficeStaffRef staff, DateTimeOffset now)
    {
        if (Status is SupportTicketStatus.Resolved or SupportTicketStatus.Closed) return false;
        ApplyStatusTransition(SupportTicketStatus.Resolved, SupportMessageAuthorKind.Staff, staff.DisplayName, now, true);
        return true;
    }

    public bool CloseByUser(DateTimeOffset now)
    {
        if (Status is SupportTicketStatus.Closed) return false;
        // The end-user may close from any non-closed state; the design only exposes Close from Resolved.
        Status = SupportTicketStatus.Closed;
        ClosedAt = now;
        LastActivityAt = now;
        HistoryEvents = HistoryEvents.Add(
            SupportTicketHistoryEvent.Create(SupportTicketHistoryEventType.Closed, SupportMessageAuthorKind.User, ReporterEmailSnapshot, now)
        );
        return true;
    }

    public bool ReopenByUser(DateTimeOffset now)
    {
        if (Status is not SupportTicketStatus.Closed) return false;
        Status = SupportTicketStatus.AwaitingAgent;
        ClosedAt = null;
        LastActivityAt = now;
        HistoryEvents = HistoryEvents.Add(
            SupportTicketHistoryEvent.Create(SupportTicketHistoryEventType.Reopened, SupportMessageAuthorKind.User, ReporterEmailSnapshot, now)
        );
        return true;
    }

    public bool Assign(BackOfficeStaffRef? assignee, BackOfficeStaffRef actor, DateTimeOffset now)
    {
        if (Equals(Assignee, assignee)) return false;
        Assignee = assignee;
        LastActivityAt = now;
        HistoryEvents = HistoryEvents.Add(
            SupportTicketHistoryEvent.Create(SupportTicketHistoryEventType.AssigneeChanged, SupportMessageAuthorKind.Staff, actor.DisplayName, now, payload: assignee?.DisplayName)
        );
        return true;
    }

    public void SubmitCsat(SupportTicketCsatScore score, string? comment, DateTimeOffset now)
    {
        Csat = new SupportTicketCsat(score, comment, now);
        // Submitting CSAT closes the ticket (PRD).
        if (Status is not SupportTicketStatus.Closed)
        {
            Status = SupportTicketStatus.Closed;
            ClosedAt = now;
        }

        LastActivityAt = now;
        HistoryEvents = HistoryEvents.Add(
            SupportTicketHistoryEvent.Create(SupportTicketHistoryEventType.CsatSubmitted, SupportMessageAuthorKind.User, ReporterEmailSnapshot, now, payload: score.ToString())
        );
    }

    private void ApplyStatusTransition(SupportTicketStatus newStatus, SupportMessageAuthorKind actorKind, string actorDisplayName, DateTimeOffset now, bool recordHistory)
    {
        if (Status == newStatus) return;

        // Leaving Closed always clears ClosedAt; the CSAT record is preserved (see SubmitCsat).
        if (Status is SupportTicketStatus.Closed) ClosedAt = null;
        // Leaving Resolved clears ResolvedAt; entering Resolved sets it.
        if (Status is SupportTicketStatus.Resolved) ResolvedAt = null;

        Status = newStatus;
        if (newStatus is SupportTicketStatus.Resolved) ResolvedAt = now;

        LastActivityAt = now;

        if (recordHistory)
        {
            HistoryEvents = HistoryEvents.Add(
                SupportTicketHistoryEvent.Create(SupportTicketHistoryEventType.StatusChanged, actorKind, actorDisplayName, now, payload: newStatus.ToString())
            );
        }
    }
}

[PublicAPI]
[IdPrefix("tkt")]
[JsonConverter(typeof(StronglyTypedIdJsonConverter<string, SupportTicketId>))]
public sealed record SupportTicketId(string Value) : StronglyTypedUlid<SupportTicketId>(Value)
{
    public override string ToString()
    {
        return Value;
    }
}

[PublicAPI]
[IdPrefix("tmsg")]
[JsonConverter(typeof(StronglyTypedIdJsonConverter<string, SupportMessageId>))]
public sealed record SupportMessageId(string Value) : StronglyTypedUlid<SupportMessageId>(Value)
{
    public override string ToString()
    {
        return Value;
    }
}

[PublicAPI]
public sealed record SupportTicketCsat(SupportTicketCsatScore Score, string? Comment, DateTimeOffset SubmittedAt);

// BackOfficeStaffRef captures the Entra ID identity of a back-office staff user at the time they
// touched the ticket. ObjectId is the Entra oid claim. DisplayName is the friendly name claim.
[PublicAPI]
public sealed record BackOfficeStaffRef(string ObjectId, string DisplayName);

[PublicAPI]
public sealed record SupportMessage(
    SupportMessageId Id,
    string AuthorIdentityValue,
    SupportMessageAuthorKind AuthorKind,
    string AuthorDisplayName,
    string Body,
    ImmutableArray<SupportMessageAttachment> Attachments,
    DateTimeOffset PostedAt
)
{
    internal static SupportMessage Create(string authorIdentityValue, SupportMessageAuthorKind authorKind, string authorDisplayName, string body, ImmutableArray<SupportMessageAttachment> attachments, DateTimeOffset postedAt)
    {
        return new SupportMessage(SupportMessageId.NewId(), authorIdentityValue, authorKind, authorDisplayName, body, attachments, postedAt);
    }
}

[PublicAPI]
public sealed record SupportMessageAttachment(string FileName, string ContentType, long SizeInBytes, string BlobUrl);

[PublicAPI]
public sealed record SupportTicketHistoryEvent(
    SupportTicketHistoryEventType Type,
    SupportMessageAuthorKind ActorKind,
    string ActorDisplayName,
    DateTimeOffset OccurredAt,
    bool HasAttachment,
    string? Payload
)
{
    public static SupportTicketHistoryEvent Create(
        SupportTicketHistoryEventType type,
        SupportMessageAuthorKind actorKind,
        string actorDisplayName,
        DateTimeOffset now,
        bool hasAttachment = false,
        string? payload = null
    )
    {
        return new SupportTicketHistoryEvent(type, actorKind, actorDisplayName, now, hasAttachment, payload);
    }
}
