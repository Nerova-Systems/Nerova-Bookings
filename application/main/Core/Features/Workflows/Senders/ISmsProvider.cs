namespace Main.Features.Workflows.Senders;

/// <summary>
///     Outbound SMS provider used by the WorkflowReminder dispatcher.
/// </summary>
public interface ISmsProvider
{
    Task<SmsResult> SendAsync(string toE164, string body, CancellationToken cancellationToken);
}

/// <summary>
///     Outcome of an SMS send attempt.
///     <para>
///         <see cref="Sent" /> — the provider accepted the message and returned a message id.
///         <see cref="NotConfigured" /> — required credentials are missing; the dispatcher should
///         skip the reminder gracefully without retrying (config has to change first).
///         <see cref="TransientFailure" /> — network / 5xx / rate-limit; safe to retry.
///         <see cref="PermanentFailure" /> — 4xx other than 429; do not retry.
///     </para>
/// </summary>
public sealed record SmsResult(SmsResultStatus Status, string? MessageId, string? ErrorReason)
{
    public static SmsResult Sent(string messageId) => new(SmsResultStatus.Sent, messageId, null);
    public static SmsResult NotConfigured(string reason) => new(SmsResultStatus.NotConfigured, null, reason);
    public static SmsResult Transient(string reason) => new(SmsResultStatus.TransientFailure, null, reason);
    public static SmsResult Permanent(string reason) => new(SmsResultStatus.PermanentFailure, null, reason);
}

public enum SmsResultStatus
{
    Sent,
    NotConfigured,
    TransientFailure,
    PermanentFailure
}
