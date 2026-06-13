using System.Globalization;
using System.Text.Json;
using Main.Features.Receptionist.Commands;
using Main.Features.Receptionist.Queries;
using Main.Features.Scheduling.Domain;
using Main.Features.Scheduling.Queries;
using Microsoft.Extensions.AI;
using SharedKernel.Cqrs;

namespace Main.Features.Receptionist.Agent;

/// <summary>
///     Builds the receptionist's tool catalog (spec R2). Every tool wraps a MediatR command or query —
///     never repositories or DbContext (architecture-tested). Parameters are primitives only; tenant and
///     customer identity are injected from <see cref="ReceptionistTurnContext" /> (spec §6.5). Write tools
///     are not registered while the conversation is unidentified (spec R3) — absent, not refused.
///     <see cref="Result" /> failures return as one-line tool error strings the model repairs
///     conversationally; tools never throw.
/// </summary>
public sealed class ReceptionistToolCatalog(IMediator mediator)
{
    private const string ToolBudgetExceededMessage = "ERROR: Tool budget for this turn is exhausted. Apologize briefly and let the customer know a team member will follow up.";

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

    public IList<AITool> Build(ReceptionistTurnContext context)
    {
        var tools = new List<AITool>
        {
            AIFunctionFactory.Create(
                () => GetBusinessInfo(context),
                "GetBusinessInfo",
                "Get the business name, languages, and notes from the owner (opening info, policies, FAQ)."
            ),
            AIFunctionFactory.Create(
                (CancellationToken cancellationToken) => GetServices(context, cancellationToken),
                "GetEventTypes",
                "List the services that can be booked: name, slug, duration in minutes, price and deposit if set."
            ),
            AIFunctionFactory.Create(
                (string serviceSlug, string? fromDate, int? days, CancellationToken cancellationToken) => GetAvailableSlots(context, serviceSlug, fromDate, days, cancellationToken),
                "GetAvailableSlots",
                "Get open appointment slots for a service. fromDate is yyyy-MM-dd (defaults to today); days is how many days ahead to search (default 7, max 14). Times are returned in the business timezone."
            ),
            AIFunctionFactory.Create(
                (string reason, string summary, CancellationToken cancellationToken) => EscalateToHuman(context, reason, summary, cancellationToken),
                "EscalateToHuman",
                "Hand the conversation to a human team member. Use for complaints, special requests, anything you cannot do, or when the customer asks for a person. Provide a short reason and a 1-3 sentence summary of the conversation so far."
            )
        };

        if (!context.IsIdentified)
        {
            tools.Add(AIFunctionFactory.Create(
                    (CancellationToken cancellationToken) => SendLoginFlow(context, cancellationToken),
                    "SendLoginFlow",
                    "Send the customer the quick sign-in form. Required before they can book, view, change, or cancel appointments."
                )
            );
            return tools;
        }

        tools.Add(AIFunctionFactory.Create(
                (string serviceSlug, string startTime, CancellationToken cancellationToken) => CreateBooking(context, serviceSlug, startTime, cancellationToken),
                "CreateBooking",
                "Book an appointment for the customer after they explicitly confirmed the summary. startTime must be an exact slot start returned by GetAvailableSlots, in ISO 8601 format."
            )
        );
        tools.Add(AIFunctionFactory.Create(
                (CancellationToken cancellationToken) => GetMyBookings(context, cancellationToken),
                "GetMyBookings",
                "List the customer's upcoming appointments with their booking codes."
            )
        );
        tools.Add(AIFunctionFactory.Create(
                (string bookingCode, string newStartTime, string? reason, CancellationToken cancellationToken) => RescheduleBooking(context, bookingCode, newStartTime, reason, cancellationToken),
                "RescheduleBooking",
                "Move one of the customer's appointments to a new time. bookingCode comes from GetMyBookings; newStartTime must be an open slot from GetAvailableSlots (ISO 8601)."
            )
        );
        tools.Add(AIFunctionFactory.Create(
                (string bookingCode, string? reason, CancellationToken cancellationToken) => CancelBooking(context, bookingCode, reason, cancellationToken),
                "CancelBooking",
                "Cancel one of the customer's appointments. bookingCode comes from GetMyBookings."
            )
        );
        tools.Add(AIFunctionFactory.Create(
                (string bookingCode, CancellationToken cancellationToken) => RequestDeposit(context, bookingCode, cancellationToken),
                "RequestDeposit",
                "Create the secure deposit payment link for a booking that requires one and share it with the customer."
            )
        );
        tools.Add(AIFunctionFactory.Create(
                (CancellationToken cancellationToken) => GetClientDetails(context, cancellationToken),
                "GetClientDetails",
                "Get what the business knows about this customer (preferences, important service notes). Use it to personalize service and avoid asking for things already on file."
            )
        );
        tools.Add(AIFunctionFactory.Create(
                (string fieldKey, string? value, CancellationToken cancellationToken) => UpdateClientDetail(context, fieldKey, value, cancellationToken),
                "UpdateClientDetail",
                "Save one customer detail the customer just told you (e.g. an allergy, a goal, a preference). fieldKey must be a writable key from GetClientDetails or the known field list; value null clears it. Only save what the customer explicitly stated."
            )
        );

        return tools;
    }

    private static string GetBusinessInfo(ReceptionistTurnContext context)
    {
        if (!context.TryConsumeToolBudget()) return ToolBudgetExceededMessage;

        return Serialize(new
            {
                business_name = context.Profile.DisplayName,
                languages = context.Settings.Languages,
                timezone = context.TimeZone,
                notes = context.Settings.FaqNotes ?? "No additional notes."
            }
        );
    }

    private async Task<string> GetServices(ReceptionistTurnContext context, CancellationToken cancellationToken)
    {
        if (!context.TryConsumeToolBudget()) return ToolBudgetExceededMessage;

        var result = await mediator.Send(new GetPublicServicesQuery(context.TenantId), cancellationToken);
        if (!result.IsSuccess) return ToToolError(context, result.GetErrorSummary());

        return Serialize(result.Value!.Services.Select(service => new
                {
                    service.Slug,
                    service.Title,
                    service.Description,
                    duration_minutes = service.DurationMinutes,
                    price = FormatAmount(service.Price, service.Currency),
                    deposit = FormatAmount(service.DepositAmount, service.Currency)
                }
            )
        );
    }

    private async Task<string> GetAvailableSlots(ReceptionistTurnContext context, string serviceSlug, string? fromDate, int? days, CancellationToken cancellationToken)
    {
        if (!context.TryConsumeToolBudget()) return ToolBudgetExceededMessage;

        var searchDays = Math.Clamp(days ?? 7, 1, 14);
        var startDate = ParseDate(fromDate) ?? DateOnly.FromDateTime(context.Now.UtcDateTime);
        var startTime = startDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var rangeStart = new DateTimeOffset(startTime) < context.Now ? context.Now : new DateTimeOffset(startTime);
        var rangeEnd = new DateTimeOffset(startDate.AddDays(searchDays).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));

        var result = await mediator.Send(new GetPublicSlotsQuery(context.Profile.Handle, serviceSlug.Trim(), rangeStart, rangeEnd, context.TimeZone), cancellationToken);
        if (!result.IsSuccess) return ToToolError(context, result.GetErrorSummary());

        // Cap the payload to bound tokens: at most the next 20 slots across the range (spec §6.4).
        var slots = result.Value!.Slots
            .SelectMany(day => day.Value)
            .OrderBy(slot => slot.Time)
            .Take(20)
            .Select(slot => new
                {
                    start_time = slot.Time.ToString("o"),
                    local_label = FormatLocal(slot.Time, context.TimeZone)
                }
            )
            .ToArray();

        return slots.Length == 0
            ? "No open slots in that period. Suggest the customer tries different dates."
            : Serialize(slots);
    }

    private async Task<string> SendLoginFlow(ReceptionistTurnContext context, CancellationToken cancellationToken)
    {
        if (!context.TryConsumeToolBudget()) return ToolBudgetExceededMessage;

        var result = await mediator.Send(new SendReceptionistLoginFlowCommand(context.TenantId, context.Conversation.Id), cancellationToken);
        if (!result.IsSuccess) return ToToolError(context, result.GetErrorSummary());

        return "Sign-in form sent. Tell the customer to tap it and confirm their details; afterwards you can manage their bookings.";
    }

    private async Task<string> CreateBooking(ReceptionistTurnContext context, string serviceSlug, string startTime, CancellationToken cancellationToken)
    {
        if (!context.TryConsumeToolBudget()) return ToolBudgetExceededMessage;
        if (!DateTimeOffset.TryParse(startTime, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedStartTime))
        {
            return ToToolError(context, "startTime must be a valid ISO 8601 timestamp from GetAvailableSlots.");
        }

        var client = context.Client!;
        var command = new CreateCustomerBookingCommand(
            context.TenantId,
            context.CustomerPhoneNumber,
            $"{client.FirstName} {client.LastName}".Trim(),
            client.Email ?? string.Empty,
            serviceSlug.Trim(),
            parsedStartTime
        );

        var result = await mediator.Send(command, cancellationToken);
        if (!result.IsSuccess || result.Value is null) return ToToolError(context, result.GetErrorSummary());

        var servicesResult = await mediator.Send(new GetPublicServicesQuery(context.TenantId), cancellationToken);
        var service = servicesResult.IsSuccess ? servicesResult.Value!.Services.FirstOrDefault(s => s.Slug == serviceSlug.Trim()) : null;
        var requiresDeposit = service?.DepositAmount is > 0;

        return Serialize(new
            {
                booking_code = GetCustomerBookingsHandler.ToBookingCode(result.Value.Id),
                start_time = result.Value.StartTime.ToString("o"),
                local_label = FormatLocal(result.Value.StartTime, context.TimeZone),
                status = result.Value.Status,
                requires_deposit = requiresDeposit,
                next_step = requiresDeposit
                    ? "A deposit is required. Call RequestDeposit with this booking_code and send the customer the payment link. The booking is only confirmed once paid."
                    : "The booking is in place. Confirm it to the customer."
            }
        );
    }

    private async Task<string> GetMyBookings(ReceptionistTurnContext context, CancellationToken cancellationToken)
    {
        if (!context.TryConsumeToolBudget()) return ToolBudgetExceededMessage;

        var result = await mediator.Send(new GetCustomerBookingsQuery(context.TenantId, context.CustomerPhoneNumber), cancellationToken);
        if (!result.IsSuccess) return ToToolError(context, result.GetErrorSummary());

        if (result.Value!.Bookings.Length == 0) return "The customer has no upcoming appointments.";

        return Serialize(result.Value.Bookings.Select(booking => new
                {
                    booking_code = booking.BookingCode,
                    service = booking.ServiceTitle,
                    start_time = booking.StartTime.ToString("o"),
                    local_label = FormatLocal(booking.StartTime, context.TimeZone),
                    status = booking.Status,
                    payment_status = booking.PaymentStatus
                }
            )
        );
    }

    private async Task<string> RescheduleBooking(ReceptionistTurnContext context, string bookingCode, string newStartTime, string? reason, CancellationToken cancellationToken)
    {
        if (!context.TryConsumeToolBudget()) return ToolBudgetExceededMessage;
        if (!DateTimeOffset.TryParse(newStartTime, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedStartTime))
        {
            return ToToolError(context, "newStartTime must be a valid ISO 8601 timestamp from GetAvailableSlots.");
        }

        var bookingId = await ResolveBookingId(context, bookingCode, cancellationToken);
        if (bookingId is null) return ToToolError(context, $"No upcoming booking matches code '{bookingCode}'. Use GetMyBookings first.");

        var result = await mediator.Send(new RescheduleCustomerBookingCommand(context.TenantId, context.CustomerPhoneNumber, bookingId, parsedStartTime, reason), cancellationToken);
        if (!result.IsSuccess || result.Value is null) return ToToolError(context, result.GetErrorSummary());

        return Serialize(new
            {
                booking_code = GetCustomerBookingsHandler.ToBookingCode(result.Value.Id),
                start_time = result.Value.StartTime.ToString("o"),
                local_label = FormatLocal(result.Value.StartTime, context.TimeZone),
                status = result.Value.Status
            }
        );
    }

    private async Task<string> CancelBooking(ReceptionistTurnContext context, string bookingCode, string? reason, CancellationToken cancellationToken)
    {
        if (!context.TryConsumeToolBudget()) return ToolBudgetExceededMessage;

        var bookingId = await ResolveBookingId(context, bookingCode, cancellationToken);
        if (bookingId is null) return ToToolError(context, $"No upcoming booking matches code '{bookingCode}'. Use GetMyBookings first.");

        var result = await mediator.Send(new CancelCustomerBookingCommand(context.TenantId, context.CustomerPhoneNumber, bookingId, reason), cancellationToken);
        if (!result.IsSuccess) return ToToolError(context, result.GetErrorSummary());

        return "The appointment is cancelled. Let the customer know and offer to rebook.";
    }

    private async Task<string> RequestDeposit(ReceptionistTurnContext context, string bookingCode, CancellationToken cancellationToken)
    {
        if (!context.TryConsumeToolBudget()) return ToolBudgetExceededMessage;

        var bookingId = await ResolveBookingId(context, bookingCode, cancellationToken);
        if (bookingId is null) return ToToolError(context, $"No upcoming booking matches code '{bookingCode}'. Use GetMyBookings first.");

        var result = await mediator.Send(new RequestBookingDepositCommand(context.TenantId, bookingId, context.CustomerPhoneNumber), cancellationToken);
        if (!result.IsSuccess || result.Value is null) return ToToolError(context, result.GetErrorSummary());

        return Serialize(new
            {
                payment_url = result.Value.PaymentUrl,
                amount = FormatAmount(result.Value.Amount, result.Value.Currency),
                instruction = "Share this exact link with the customer and explain the booking is confirmed once the deposit is paid."
            }
        );
    }

    private async Task<string> GetClientDetails(ReceptionistTurnContext context, CancellationToken cancellationToken)
    {
        if (!context.TryConsumeToolBudget()) return ToolBudgetExceededMessage;

        var result = await mediator.Send(new GetClientAgentDetailsQuery(context.TenantId, context.Client!.Id), cancellationToken);
        if (!result.IsSuccess) return ToToolError(context, result.GetErrorSummary());

        if (result.Value!.Details.Length == 0) return "Nothing is on file for this customer yet.";

        return Serialize(result.Value.Details.Select(detail => new
                {
                    field_key = detail.Key,
                    label = detail.Label,
                    value = detail.Value,
                    affects_service = detail.IsConstraint,
                    writable = detail.IsWritable
                }
            )
        );
    }

    private async Task<string> UpdateClientDetail(ReceptionistTurnContext context, string fieldKey, string? value, CancellationToken cancellationToken)
    {
        if (!context.TryConsumeToolBudget()) return ToolBudgetExceededMessage;

        var result = await mediator.Send(new UpdateClientDetailFromAgentCommand(context.TenantId, context.Client!.Id, fieldKey.Trim(), value), cancellationToken);
        if (!result.IsSuccess || result.Value is null) return ToToolError(context, result.GetErrorSummary());

        return $"Saved. Receipt for the owner: {result.Value}";
    }

    private async Task<string> EscalateToHuman(ReceptionistTurnContext context, string reason, string summary, CancellationToken cancellationToken)
    {
        if (!context.TryConsumeToolBudget()) return ToolBudgetExceededMessage;

        var result = await mediator.Send(new EscalateConversationCommand(context.TenantId, context.Conversation.Id, context.Client?.Id, reason, summary), cancellationToken);
        if (!result.IsSuccess) return ToToolError(context, result.GetErrorSummary());

        context.RequestEscalation(reason);
        return "Escalated. Tell the customer a team member will get back to them shortly, then stop offering further help.";
    }

    private async Task<BookingId?> ResolveBookingId(ReceptionistTurnContext context, string bookingCode, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetCustomerBookingsQuery(context.TenantId, context.CustomerPhoneNumber), cancellationToken);
        if (!result.IsSuccess) return null;

        var normalizedCode = bookingCode.Trim().ToUpperInvariant();
        return result.Value!.Bookings.FirstOrDefault(booking => booking.BookingCode == normalizedCode)?.Id;
    }

    private static string ToToolError(ReceptionistTurnContext context, string message)
    {
        context.RecordFailedToolCall();
        return $"ERROR: {message}";
    }

    private static string Serialize(object payload)
    {
        return JsonSerializer.Serialize(payload, JsonOptions);
    }

    private static string? FormatAmount(decimal? amount, string currency)
    {
        return amount is null ? null : $"{currency} {amount.Value:0.##}";
    }

    private static DateOnly? ParseDate(string? date)
    {
        if (string.IsNullOrWhiteSpace(date)) return null;
        return DateOnly.TryParseExact(date.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed) ? parsed : null;
    }

    private static string FormatLocal(DateTimeOffset utcTime, string timeZoneId)
    {
        try
        {
            var local = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(utcTime, timeZoneId);
            return local.ToString("ddd d MMM, HH:mm", CultureInfo.InvariantCulture);
        }
        catch (Exception exception) when (exception is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return utcTime.ToString("ddd d MMM, HH:mm 'UTC'", CultureInfo.InvariantCulture);
        }
    }
}
