using JetBrains.Annotations;
using Main.Features.WhatsAppFlows.Domain;
using SharedKernel.Domain;

namespace Main.Features.WhatsAppFlows.Shared;

[PublicAPI]
public sealed record TenantFlowConfigResponse(
    long Id,
    TenantId TenantId,
    BusinessVertical BusinessVertical,
    StaffAssignment StaffAssignment,
    PaymentTiming PaymentTiming,
    long? DepositAmountCents,
    int BookingWindowDays,
    int DefaultSessionMinutes,
    bool HasMultipleServices,
    bool AllowSameDayBookings,
    string ConfirmationMessageTemplate,
    string CancellationContact,
    IReadOnlyList<CustomQuestion> CustomPreBookingQuestions,
    string ConfigVersionHash
)
{
    public static TenantFlowConfigResponse From(TenantFlowConfig c)
    {
        return new TenantFlowConfigResponse(
            c.Id.Value,
            c.TenantId,
            c.BusinessVertical,
            c.StaffAssignment,
            c.PaymentTiming,
            c.DepositAmountCents,
            c.BookingWindowDays,
            c.DefaultSessionMinutes,
            c.HasMultipleServices,
            c.AllowSameDayBookings,
            c.ConfirmationMessageTemplate,
            c.CancellationContact,
            c.CustomPreBookingQuestions,
            c.ConfigVersionHash
        );
    }
}

[PublicAPI]
public sealed record CustomQuestionResponse(int Order, string QuestionText, bool IsRequired, CustomQuestionType QuestionType, string[]? Choices);

[PublicAPI]
public sealed record PublishFlowResponse(string FlowId, string Status, string PreviewUrl, DateTimeOffset PreviewExpiresAt);
