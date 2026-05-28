using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using JetBrains.Annotations;
using SharedKernel.Domain;
using SharedKernel.StronglyTypedIds;

namespace Main.Features.WhatsAppFlows.Domain;

[PublicAPI]
[JsonConverter(typeof(StronglyTypedIdJsonConverter<long, TenantFlowConfigId>))]
public sealed record TenantFlowConfigId(long Value) : StronglyTypedLongId<TenantFlowConfigId>(Value)
{
    public override string ToString()
    {
        return Value.ToString();
    }
}

[PublicAPI]
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum BusinessVertical
{
    HairSalon,
    BarberShop,
    PersonalTrainer,
    Tutor,
    Clinic,
    Other
}

[PublicAPI]
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum StaffAssignment
{
    SpecificStaff,
    FirstAvailable,
    AutoAssign
}

[PublicAPI]
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PaymentTiming
{
    AfterSession,
    BeforeBooking,
    Deposit
}

[PublicAPI]
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CustomQuestionType
{
    Text,
    MultipleChoice,
    YesNo
}

/// <summary>
///     Value-object captured as part of the questionnaire. Stored as a json column on
///     <see cref="TenantFlowConfig" /> via the EF Core configuration.
/// </summary>
[PublicAPI]
public sealed record CustomQuestion(
    int Order,
    string QuestionText,
    bool IsRequired,
    CustomQuestionType QuestionType,
    string[]? Choices
);

/// <summary>
///     The questionnaire output for a tenant — the source of truth for what the WhatsApp Flow
///     looks like. One record per tenant.
/// </summary>
public sealed class TenantFlowConfig : AggregateRoot<TenantFlowConfigId>, ITenantScopedEntity
{
    private List<CustomQuestion> _customPreBookingQuestions = [];

    [UsedImplicitly]
    private TenantFlowConfig() : base(TenantFlowConfigId.NewId())
    {
        ConfirmationMessageTemplate = string.Empty;
        CancellationContact = string.Empty;
        ConfigVersionHash = string.Empty;
    }

    private TenantFlowConfig(TenantId tenantId, BusinessVertical vertical) : base(TenantFlowConfigId.NewId())
    {
        TenantId = tenantId;
        BusinessVertical = vertical;
        ConfirmationMessageTemplate = string.Empty;
        CancellationContact = string.Empty;
        ApplyVerticalDefaults(vertical);
        ConfigVersionHash = ComputeHash();
    }

    public TenantId TenantId { get; private set; } = null!;

    public BusinessVertical BusinessVertical { get; private set; }

    public StaffAssignment StaffAssignment { get; private set; }

    public PaymentTiming PaymentTiming { get; private set; }

    public long? DepositAmountCents { get; private set; }

    public int BookingWindowDays { get; private set; }

    public int DefaultSessionMinutes { get; private set; }

    public bool HasMultipleServices { get; private set; }

    public bool AllowSameDayBookings { get; private set; }

    public string ConfirmationMessageTemplate { get; private set; }

    public string CancellationContact { get; private set; }

    public IReadOnlyList<CustomQuestion> CustomPreBookingQuestions => _customPreBookingQuestions.AsReadOnly();

    public string ConfigVersionHash { get; private set; }

    public static TenantFlowConfig Create(TenantId tenantId, BusinessVertical vertical)
    {
        return new TenantFlowConfig(tenantId, vertical);
    }

    public void UpdateBusinessProfile(
        BusinessVertical vertical,
        StaffAssignment staffAssignment,
        PaymentTiming paymentTiming,
        long? depositAmountCents,
        int bookingWindowDays,
        int defaultSessionMinutes,
        bool hasMultipleServices,
        bool allowSameDayBookings,
        string confirmationMessageTemplate,
        string cancellationContact
    )
    {
        BusinessVertical = vertical;
        StaffAssignment = staffAssignment;
        PaymentTiming = paymentTiming;
        DepositAmountCents = paymentTiming == PaymentTiming.Deposit ? depositAmountCents : null;
        BookingWindowDays = bookingWindowDays;
        DefaultSessionMinutes = defaultSessionMinutes;
        HasMultipleServices = hasMultipleServices;
        AllowSameDayBookings = allowSameDayBookings;
        ConfirmationMessageTemplate = confirmationMessageTemplate;
        CancellationContact = cancellationContact;
        RecomputeVersionHash();
    }

    public CustomQuestion AddCustomQuestion(string text, CustomQuestionType type, bool isRequired, string[]? choices)
    {
        var order = _customPreBookingQuestions.Count == 0 ? 1 : _customPreBookingQuestions.Max(q => q.Order) + 1;
        var question = new CustomQuestion(order, text, isRequired, type, choices);
        _customPreBookingQuestions.Add(question);
        RecomputeVersionHash();
        return question;
    }

    public bool RemoveCustomQuestion(int order)
    {
        var existing = _customPreBookingQuestions.FirstOrDefault(q => q.Order == order);
        if (existing is null) return false;
        _customPreBookingQuestions.Remove(existing);
        RecomputeVersionHash();
        return true;
    }

    public void RecomputeVersionHash()
    {
        ConfigVersionHash = ComputeHash();
    }

    private string ComputeHash()
    {
        // Deterministic serialization for stable hashing: ordered properties, ordered questions.
        var canonical = new
        {
            BusinessVertical = BusinessVertical.ToString(),
            StaffAssignment = StaffAssignment.ToString(),
            PaymentTiming = PaymentTiming.ToString(),
            DepositAmountCents,
            BookingWindowDays,
            DefaultSessionMinutes,
            HasMultipleServices,
            AllowSameDayBookings,
            ConfirmationMessageTemplate,
            CancellationContact,
            Questions = _customPreBookingQuestions
                .OrderBy(q => q.Order)
                .Select(q => new
                    {
                        q.Order,
                        q.QuestionText,
                        q.IsRequired,
                        Type = q.QuestionType.ToString(),
                        Choices = q.Choices ?? []
                    }
                )
                .ToArray()
        };

        var json = JsonSerializer.Serialize(canonical);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private void ApplyVerticalDefaults(BusinessVertical vertical)
    {
        BookingWindowDays = 30;
        DefaultSessionMinutes = 60;
        AllowSameDayBookings = true;
        StaffAssignment = StaffAssignment.AutoAssign;
        PaymentTiming = PaymentTiming.AfterSession;
        ConfirmationMessageTemplate = "Hi {name}, your booking for {service} on {time} with {staff} is confirmed.";
        CancellationContact = string.Empty;

        switch (vertical)
        {
            case BusinessVertical.HairSalon:
            case BusinessVertical.BarberShop:
                HasMultipleServices = true;
                StaffAssignment = StaffAssignment.SpecificStaff;
                DefaultSessionMinutes = 45;
                break;
            case BusinessVertical.PersonalTrainer:
            case BusinessVertical.Tutor:
                HasMultipleServices = false;
                StaffAssignment = StaffAssignment.AutoAssign;
                DefaultSessionMinutes = 60;
                break;
            case BusinessVertical.Clinic:
                HasMultipleServices = true;
                StaffAssignment = StaffAssignment.SpecificStaff;
                DefaultSessionMinutes = 30;
                break;
            case BusinessVertical.Other:
            default:
                HasMultipleServices = false;
                break;
        }
    }
}
