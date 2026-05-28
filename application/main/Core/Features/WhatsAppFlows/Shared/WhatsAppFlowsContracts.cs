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
    string ConfigVersionHash,
    string? WabaPhoneNumber
)
{
    public static TenantFlowConfigResponse From(TenantFlowConfig c, string? wabaPhoneNumber = null)
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
            c.ConfigVersionHash,
            NormalizeToE164(wabaPhoneNumber)
        );
    }

    /// <summary>
    ///     Account SCS stores the WABA phone number as Meta returns it (e.g. <c>"+27 81 123 4567"</c>).
    ///     We strip spaces and other non-digit characters and re-prepend <c>+</c> so the value is a
    ///     canonical E.164 string suitable for building <c>wa.me</c> URLs.
    /// </summary>
    private static string? NormalizeToE164(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var digits = new string(raw.Where(char.IsDigit).ToArray());
        return digits.Length == 0 ? null : "+" + digits;
    }
}

[PublicAPI]
public sealed record CustomQuestionResponse(int Order, string QuestionText, bool IsRequired, CustomQuestionType QuestionType, string[]? Choices);

[PublicAPI]
public sealed record PublishFlowResponse(string FlowId, string Status, string PreviewUrl, DateTimeOffset PreviewExpiresAt);

[PublicAPI]
public sealed record FlowPreviewLinkResponse(string PreviewUrl, DateTimeOffset ExpiresAt);
