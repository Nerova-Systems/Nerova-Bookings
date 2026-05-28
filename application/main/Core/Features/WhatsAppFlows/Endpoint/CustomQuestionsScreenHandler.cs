using System.Text.Json.Nodes;
using Main.Features.WhatsAppFlows.Domain;

namespace Main.Features.WhatsAppFlows.Endpoint;

/// <summary>
///     Custom questions screen. INIT is a no-op because the question definitions live in the
///     published flow JSON. data_exchange forwards all collected answers under
///     <c>custom_answers</c> and routes to payment / confirmation.
/// </summary>
public sealed class CustomQuestionsScreenHandler : IFlowScreenHandler
{
    public string ScreenId => FlowScreens.CustomQuestions;

    public Task<FlowScreenResponse> Handle(FlowScreenRequest request, TenantFlowConfig config, CancellationToken cancellationToken)
    {
        if (string.Equals(request.Action, "INIT", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(new FlowScreenResponse(FlowScreens.CustomQuestions, new JsonObject()));
        }

        var data = new JsonObject();
        if (request.Data.TryGetProperty("custom_answers", out var answers))
        {
            data["custom_answers"] = JsonNode.Parse(answers.GetRawText());
        }

        var next = config.PaymentTiming is PaymentTiming.BeforeBooking or PaymentTiming.Deposit
            ? FlowScreens.PaymentNotice
            : FlowScreens.ConfirmBooking;
        return Task.FromResult(new FlowScreenResponse(next, data));
    }
}
