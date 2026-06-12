using System.Diagnostics;
using System.Text.Json;
using JetBrains.Annotations;
using Main.Features.Clients.Domain;
using Main.Features.Receptionist.Agent;
using Main.Features.Receptionist.Domain;
using Main.Features.Receptionist.Queries;
using Main.Features.Scheduling.Domain;
using Main.Features.WhatsAppBooking.Domain;
using Main.Features.WhatsAppBooking.Infrastructure;
using Main.Features.WhatsAppOnboarding.Domain;
using Microsoft.Extensions.Options;
using Main.Integrations.Ai;
using SharedKernel.Cqrs;
using SharedKernel.Domain;
using SharedKernel.Telemetry;

namespace Main.Features.Receptionist.Commands;

/// <summary>
///     Runs one AI receptionist turn for an inbound WhatsApp message (spec §6.3): serializes turns per
///     conversation, enforces session and tenant token budgets, composes the persona and state-filtered
///     tools, resumes the persisted agent session, runs the model, sends the reply, and persists the
///     updated session — all in one unit of work. This handler is the single owner of the conversation
///     and session aggregates during a turn. Any unhandled failure sends a safe fallback message so the
///     customer is never left unanswered (spec §6.8).
/// </summary>
[PublicAPI]
public sealed record ProcessReceptionistTurnCommand(TenantId TenantId, string CustomerPhoneNumber, string MessageText)
    : ICommand, IRequest<Result>;

public sealed class ProcessReceptionistTurnHandler(
    IWhatsAppConversationRepository conversationRepository,
    IWhatsAppBusinessAccountRepository businessAccountRepository,
    IReceptionistSettingsRepository receptionistSettingsRepository,
    IReceptionistSessionRepository receptionistSessionRepository,
    ISchedulingProfileRepository schedulingProfileRepository,
    IClientRepository clientRepository,
    ReceptionistAgentFactory agentFactory,
    ReceptionistTurnLockRegistry turnLockRegistry,
    IWhatsAppOutboundSender outboundSender,
    IMediator mediator,
    IOptions<AiOptions> aiOptions,
    TimeProvider timeProvider,
    ITelemetryEventsCollector events,
    ILogger<ProcessReceptionistTurnHandler> logger
) : IRequestHandler<ProcessReceptionistTurnCommand, Result>
{
    private const string FallbackMessage = "Sorry, something went wrong on our side. A team member will get back to you shortly.";
    private const string BudgetEscalationMessage = "Let me connect you with the team — someone will get back to you shortly.";
    private const string EscalatedHoldMessage = "A team member has been notified and will reply here as soon as possible. Thank you for your patience!";

    public async Task<Result> Handle(ProcessReceptionistTurnCommand command, CancellationToken cancellationToken)
    {
        using var turnLock = await turnLockRegistry.AcquireAsync($"{command.TenantId.Value}:{command.CustomerPhoneNumber}", cancellationToken);

        var account = await businessAccountRepository.GetByTenantIdUnfilteredAsync(command.TenantId, cancellationToken);
        if (account is null)
        {
            return Result.BadRequest("WhatsApp is not connected for this tenant.");
        }

        var settings = await receptionistSettingsRepository.GetByTenantUnfilteredAsync(command.TenantId, cancellationToken);
        if (settings?.IsEnabled != true)
        {
            return Result.BadRequest("The receptionist is not enabled for this tenant.");
        }

        var now = timeProvider.GetUtcNow();
        var conversation = await conversationRepository.GetByTenantAndPhoneUnfilteredAsync(command.TenantId, command.CustomerPhoneNumber, cancellationToken);
        var conversationIsNew = conversation is null;
        conversation ??= WhatsAppConversation.Start(command.TenantId, command.CustomerPhoneNumber, now);

        // Identification: a known client phone (or a just-completed login Flow) identifies the customer
        // before tools are composed, so write tools appear only for verified identities (spec R3).
        var client = await clientRepository.GetByTenantAndContactUnfilteredAsync(command.TenantId, command.CustomerPhoneNumber, null, cancellationToken);
        if (client is not null && !conversation.IsIdentified)
        {
            conversation.MarkIdentified(now);
        }

        if (!conversation.IsIdentified)
        {
            client = null;
        }

        var session = await receptionistSessionRepository.GetByConversationUnfilteredAsync(conversation.Id, cancellationToken);
        var sessionIsNew = session is null;
        session ??= ReceptionistSession.Start(command.TenantId, conversation.Id, now);

        // Escalated conversations get a single courteous hold message, then silence until resolved (R6).
        if (session.State == ReceptionistSessionState.Escalated)
        {
            if (!session.EscalationHoldNotified)
            {
                await outboundSender.SendTextAsync(account, conversation.CustomerPhoneNumber, EscalatedHoldMessage, cancellationToken);
                session.MarkEscalationHoldNotified();
                await PersistAsync(conversation, conversationIsNew, session, sessionIsNew, cancellationToken);
            }

            return Result.Success();
        }

        // Session expiry follows the conversation timeout semantics: an inactive thread restarts fresh (R7).
        if (session.State == ReceptionistSessionState.Expired || session.LastTurnAt is not null && now - session.LastTurnAt > WhatsAppConversation.SessionTimeout)
        {
            session.RestartThread(now);
        }

        var profile = await schedulingProfileRepository.GetByTenantIdUnfilteredAsync(command.TenantId, cancellationToken);
        if (profile is null)
        {
            await outboundSender.SendTextAsync(account, conversation.CustomerPhoneNumber, FallbackMessage, cancellationToken);
            return Result.BadRequest("No scheduling profile exists for this tenant.");
        }

        // Budget guardrails (R9): breached budgets degrade gracefully — escalate, never silently drop.
        var options = aiOptions.Value;
        if (session.InputTokens + session.OutputTokens >= options.MaxTokensPerSession)
        {
            return await EscalateForBudgetAsync(command, conversation, conversationIsNew, account, session, sessionIsNew, "Session token budget exceeded", cancellationToken);
        }

        var monthStart = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero);
        var tenantTokensThisMonth = await receptionistSessionRepository.GetTenantTokensUsedSinceUnfilteredAsync(command.TenantId, monthStart, cancellationToken);
        if (tenantTokensThisMonth >= options.MaxTokensPerTenantPerMonth)
        {
            return await EscalateForBudgetAsync(command, conversation, conversationIsNew, account, session, sessionIsNew, "Tenant monthly token budget exceeded", cancellationToken);
        }

        var context = new ReceptionistTurnContext(account, conversation, settings, profile, client, now, options.MaxToolCallsPerTurn);

        var servicesResult = await mediator.Send(new GetPublicServicesQuery(command.TenantId), cancellationToken);
        var serviceSummary = servicesResult.IsSuccess
            ? string.Join("\n", servicesResult.Value!.Services.Select(service =>
                    $"- {service.Title} ({service.DurationMinutes} min{FormatPrice(service.Price, service.DepositAmount, service.Currency)})"
                )
            )
            : string.Empty;

        var stopwatch = Stopwatch.StartNew();
        try
        {
            var agent = agentFactory.Create(context, serviceSummary);

            var agentSession = session.AgentThread is null
                ? await agent.CreateSessionAsync(cancellationToken)
                : await agent.DeserializeSessionAsync(JsonDocument.Parse(session.AgentThread).RootElement, null, cancellationToken);

            var response = await agent.RunAsync(command.MessageText, agentSession, null, cancellationToken);

            var replyText = string.IsNullOrWhiteSpace(response.Text) ? FallbackMessage : response.Text.Trim();
            await outboundSender.SendTextAsync(account, conversation.CustomerPhoneNumber, replyText, cancellationToken);

            var serializedSession = (await agent.SerializeSessionAsync(agentSession, null, cancellationToken)).GetRawText();
            var inputTokens = response.Usage?.InputTokenCount ?? 0;
            var outputTokens = response.Usage?.OutputTokenCount ?? 0;
            session.RecordTurn(serializedSession, inputTokens, outputTokens, now);

            if (context.EscalationRequested || context.ToolBudgetExceeded)
            {
                if (context.ToolBudgetExceeded && !context.EscalationRequested)
                {
                    await mediator.Send(new EscalateConversationCommand(command.TenantId, conversation.Id, client?.Id, "Tool budget exceeded in a single turn", $"Last customer message: {Truncate(command.MessageText)}"), cancellationToken);
                }

                session.Escalate();
            }

            conversation.TouchInbound(now);
            await PersistAsync(conversation, conversationIsNew, session, sessionIsNew, cancellationToken);

            events.CollectEvent(new ReceptionistTurnCompleted(conversation.Id, context.ToolCallCount, inputTokens, outputTokens, stopwatch.ElapsedMilliseconds));

            return Result.Success();
        }
        catch (Exception exception)
        {
            // The model or transport failed: degrade to a safe fallback so the customer is never left
            // unanswered, and surface the conversation to a human. Webhook ingestion is unaffected.
            logger.LogError(exception, "Receptionist turn failed for conversation {ConversationId}", conversation.Id.Value);
            await outboundSender.SendTextAsync(account, conversation.CustomerPhoneNumber, FallbackMessage, cancellationToken);
            await mediator.Send(new EscalateConversationCommand(command.TenantId, conversation.Id, client?.Id, "Receptionist turn failed", $"Last customer message: {Truncate(command.MessageText)}"), cancellationToken);
            session.Escalate();
            session.MarkEscalationHoldNotified();
            await PersistAsync(conversation, conversationIsNew, session, sessionIsNew, cancellationToken);

            return Result.Success();
        }
    }

    private async Task<Result> EscalateForBudgetAsync(
        ProcessReceptionistTurnCommand command,
        WhatsAppConversation conversation,
        bool conversationIsNew,
        WhatsAppBusinessAccount account,
        ReceptionistSession session,
        bool sessionIsNew,
        string reason,
        CancellationToken cancellationToken
    )
    {
        await outboundSender.SendTextAsync(account, conversation.CustomerPhoneNumber, BudgetEscalationMessage, cancellationToken);
        await mediator.Send(new EscalateConversationCommand(command.TenantId, conversation.Id, null, reason, $"Last customer message: {Truncate(command.MessageText)}"), cancellationToken);

        session.Escalate();
        session.MarkEscalationHoldNotified();
        await PersistAsync(conversation, conversationIsNew, session, sessionIsNew, cancellationToken);

        return Result.Success();
    }

    private async Task PersistAsync(WhatsAppConversation conversation, bool conversationIsNew, ReceptionistSession session, bool sessionIsNew, CancellationToken cancellationToken)
    {
        if (conversationIsNew)
        {
            await conversationRepository.AddAsync(conversation, cancellationToken);
        }
        else
        {
            conversationRepository.Update(conversation);
        }

        if (sessionIsNew)
        {
            await receptionistSessionRepository.AddAsync(session, cancellationToken);
        }
        else
        {
            receptionistSessionRepository.Update(session);
        }
    }

    private static string FormatPrice(decimal? price, decimal? deposit, string currency)
    {
        if (price is null && deposit is null) return string.Empty;

        var parts = new List<string>();
        if (price is not null) parts.Add($"{currency} {price.Value:0.##}");
        if (deposit is not null) parts.Add($"deposit {currency} {deposit.Value:0.##}");
        return ", " + string.Join(", ", parts);
    }

    private static string Truncate(string text)
    {
        return text.Length <= 300 ? text : text[..300];
    }
}
