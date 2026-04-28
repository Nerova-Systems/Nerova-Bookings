namespace SharedKernel.Integrations.Email;

public static class TransactionalEmailTemplateKeys
{
    public const string LoginOtp = "login-otp";
    public const string UnknownLoginAttempt = "unknown-login-attempt";
    public const string SignupOtp = "signup-otp";
    public const string UserInvite = "user-invite";
    public const string TrialExpiry = "trial-expiry";
    public const string PaymentFailed = "payment-failed";
    public const string PaymentRecovered = "payment-recovered";
    public const string RefundRecorded = "refund-recorded";
    public const string SubscriptionChanged = "subscription-changed";
    public const string SubscriptionCancelled = "subscription-cancelled";
}

public sealed record TransactionalEmailMessagesResponse(int TotalCount, TransactionalEmailMessageResponse[] Messages);

public sealed record TransactionalEmailMessageResponse(
    Guid Id,
    string Recipient,
    string Subject,
    string TemplateKey,
    TransactionalEmailStatus Status,
    int Attempts,
    DateTimeOffset CreatedAt,
    DateTimeOffset NextAttemptAt,
    DateTimeOffset? SentAt,
    DateTimeOffset? DeadLetteredAt,
    string? LastError,
    string? CorrelationId
);
