using System.Text.Json.Nodes;
using Main.Features.WhatsAppFlows.Domain;

namespace Main.Features.WhatsAppFlows.Endpoint;

/// <summary>
///     Screen IDs the dispatcher resolves. Centralised so handlers and tests do not drift apart.
/// </summary>
public static class FlowScreens
{
    public const string Welcome = "WELCOME";
    public const string SelectService = "SELECT_SERVICE";
    public const string SelectStaff = "SELECT_STAFF";
    public const string SelectDate = "SELECT_DATE";
    public const string SelectTime = "SELECT_TIME";
    public const string CustomQuestions = "CUSTOM_QUESTIONS";
    public const string PaymentNotice = "PAYMENT_NOTICE";
    public const string ConfirmBooking = "CONFIRM_BOOKING";
    public const string Success = "SUCCESS";
}

/// <summary>
///     Welcome screen. INIT returns the business intro; data_exchange routes forward depending on
///     whether the tenant configured multiple services.
/// </summary>
public sealed class WelcomeScreenHandler : IFlowScreenHandler
{
    public string ScreenId => FlowScreens.Welcome;

    public Task<FlowScreenResponse> Handle(FlowScreenRequest request, TenantFlowConfig config, CancellationToken cancellationToken)
    {
        if (string.Equals(request.Action, "INIT", StringComparison.OrdinalIgnoreCase))
        {
            var data = new JsonObject
            {
                ["intro"] = "Welcome — let's get you booked in.",
                ["has_multiple_services"] = config.HasMultipleServices
            };
            return Task.FromResult(new FlowScreenResponse(FlowScreens.Welcome, data));
        }

        var next = config.HasMultipleServices ? FlowScreens.SelectService : FlowScreens.SelectDate;
        return Task.FromResult(new FlowScreenResponse(next, new JsonObject()));
    }
}
