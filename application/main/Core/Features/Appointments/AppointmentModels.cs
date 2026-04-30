using SharedKernel.Domain;

namespace Main.Features.Appointments;

public enum AppointmentStatus
{
    Pending,
    Confirmed,
    Cancelled,
    Rescheduled,
    Completed,
    NoShow
}

public enum AppointmentPaymentStatus
{
    NotRequired,
    Pending,
    DepositPaid,
    Paid,
    Failed,
    Refunded
}

public enum AppointmentSource
{
    PublicBookingPage,
    Manual,
    WhatsAppFlow
}

public enum ServicePaymentPolicy
{
    NoPaymentRequired,
    DepositBeforeBooking,
    FullPaymentBeforeBooking,
    CollectAfterAppointment
}

public enum AppointmentPaymentChannel
{
    HostedCheckout,
    VirtualTerminal
}

public sealed class BusinessProfile : ITenantScopedEntity
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public TenantId TenantId { get; set; } = null!;
    public string Name { get; set; } = "Nerova Studio";
    public string Slug { get; set; } = "nerova-studio";
    public string TimeZone { get; set; } = "Africa/Johannesburg";
    public string Currency { get; set; } = "ZAR";
    public string Address { get; set; } = "14 Main Rd, Sea Point, Cape Town";
    public string? LogoUrl { get; set; }
    public bool PublicBookingEnabled { get; set; } = true;
}

public sealed class ServiceCategory : ITenantScopedEntity
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public TenantId TenantId { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}

public sealed class BookableService : ITenantScopedEntity
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public TenantId TenantId { get; set; } = null!;
    public string CategoryId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Mode { get; set; } = "physical";
    public int DurationMinutes { get; set; }
    public int PriceCents { get; set; }
    public int DepositCents { get; set; }
    public ServicePaymentPolicy PaymentPolicy { get; set; }
    public int BufferBeforeMinutes { get; set; }
    public int BufferAfterMinutes { get; set; }
    public string Location { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
}

public sealed class BookableServiceVersion : ITenantScopedEntity
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public TenantId TenantId { get; set; } = null!;
    public string ServiceId { get; set; } = string.Empty;
    public int VersionNumber { get; set; }
    public string CategoryId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Mode { get; set; } = "physical";
    public int DurationMinutes { get; set; }
    public int PriceCents { get; set; }
    public int DepositCents { get; set; }
    public ServicePaymentPolicy PaymentPolicy { get; set; }
    public int BufferBeforeMinutes { get; set; }
    public int BufferAfterMinutes { get; set; }
    public string Location { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class StaffMember : ITenantScopedEntity
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public TenantId TenantId { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}

public sealed class AvailabilityRule : ITenantScopedEntity
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public TenantId TenantId { get; set; } = null!;
    public string? StaffMemberId { get; set; }
    public DayOfWeek DayOfWeek { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
}

public sealed class ExternalBusyBlock : ITenantScopedEntity
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public TenantId TenantId { get; set; } = null!;
    public string Provider { get; set; } = string.Empty;
    public DateTimeOffset StartAt { get; set; }
    public DateTimeOffset EndAt { get; set; }
    public string Label { get; set; } = string.Empty;
}

public sealed class Client : ITenantScopedEntity
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public TenantId TenantId { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Status { get; set; } = "Active";
    public string? Alert { get; set; }
    public string? InternalNote { get; set; }
}

public sealed class PublicPhoneVerification : ITenantScopedEntity
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public TenantId TenantId { get; set; } = null!;
    public string Phone { get; set; } = string.Empty;
    public string MaskedPhone { get; set; } = string.Empty;
    public string Provider { get; set; } = "TwilioVerify";
    public string? ProviderSid { get; set; }
    public string Status { get; set; } = "Pending";
    public string? VerificationTokenHash { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? VerifiedAt { get; set; }
    public DateTimeOffset? ConsumedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class Appointment : ITenantScopedEntity
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public TenantId TenantId { get; set; } = null!;
    public string PublicReference { get; set; } = Guid.NewGuid().ToString("N")[..10];
    public string ClientId { get; set; } = string.Empty;
    public string ServiceId { get; set; } = string.Empty;
    public string ServiceVersionId { get; set; } = string.Empty;
    public string StaffMemberId { get; set; } = string.Empty;
    public DateTimeOffset StartAt { get; set; }
    public DateTimeOffset EndAt { get; set; }
    public AppointmentStatus Status { get; set; }
    public AppointmentPaymentStatus PaymentStatus { get; set; }
    public AppointmentSource Source { get; set; }
    public string AnswersJson { get; set; } = "[]";
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class AppointmentPaymentIntent : ITenantScopedEntity
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public TenantId TenantId { get; set; } = null!;
    public string AppointmentId { get; set; } = string.Empty;
    public string Provider { get; set; } = "Paystack";
    public AppointmentPaymentChannel Channel { get; set; } = AppointmentPaymentChannel.HostedCheckout;
    public string Reference { get; set; } = string.Empty;
    public int AmountCents { get; set; }
    public string Status { get; set; } = "Pending";
    public string? AuthorizationUrl { get; set; }
    public string? VirtualTerminalCode { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ConfirmedAt { get; set; }
}

public sealed class PaystackSubaccount : ITenantScopedEntity
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public TenantId TenantId { get; set; } = null!;
    public string SubaccountCode { get; set; } = string.Empty;
    public int? SubaccountId { get; set; }
    public string? SplitCode { get; set; }
    public string? VirtualTerminalCode { get; set; }
    public string BusinessName { get; set; } = string.Empty;
    public string SettlementBankName { get; set; } = string.Empty;
    public string SettlementBankCode { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public string MaskedAccountNumber { get; set; } = string.Empty;
    public string Currency { get; set; } = "ZAR";
    public string? PrimaryContactName { get; set; }
    public string? PrimaryContactEmail { get; set; }
    public string? PrimaryContactPhone { get; set; }
    public bool IsActive { get; set; }
    public bool IsVerified { get; set; }
    public string SettlementSchedule { get; set; } = "auto";
    public DateTimeOffset LastSyncedAt { get; set; }
}

public sealed class AppointmentFlowEvent : ITenantScopedEntity
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public TenantId TenantId { get; set; } = null!;
    public string AppointmentId { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";
    public DateTimeOffset ScheduledFor { get; set; }
    public string PayloadJson { get; set; } = "{}";
}

public sealed class IntegrationConnection : ITenantScopedEntity
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public TenantId TenantId { get; set; } = null!;
    public string Provider { get; set; } = string.Empty;
    public string Capability { get; set; } = string.Empty;
    public string Status { get; set; } = "NotConnected";
    public DateTimeOffset? LastSyncedAt { get; set; }
}
