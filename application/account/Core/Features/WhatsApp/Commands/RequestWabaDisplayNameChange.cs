using System.Text.RegularExpressions;
using Account.Features.Users.Domain;
using Account.Features.WhatsApp.Domain;
using Account.Features.WhatsApp.Infrastructure;
using FluentValidation;
using JetBrains.Annotations;
using SharedKernel.Cqrs;
using SharedKernel.ExecutionContext;
using SharedKernel.Telemetry;

namespace Account.Features.WhatsApp.Commands;

/// <summary>
///     Submits a Meta WABA display-name change request for the current tenant. Meta reviews the
///     request for 1–3 business days; the result is observed via the Phase 7c poller and surfaced
///     through <see cref="Queries.GetWabaDisplayNameStatusQuery" />.
///     <para>
///         The handler is owner-gated (matching <c>UpdateTenantBrandProfileHandler</c>) and
///         enforces the local invariant that a tenant cannot submit a new request while a
///         previous one is still in review — Meta returns a 4xx in that case and we'd rather fail
///         locally without the round-trip.
///     </para>
/// </summary>
[PublicAPI]
public sealed record RequestWabaDisplayNameChangeCommand(string RequestedDisplayName)
    : ICommand, IRequest<Result>;

public sealed partial class RequestWabaDisplayNameChangeValidator : AbstractValidator<RequestWabaDisplayNameChangeCommand>
{
    /// <summary>
    ///     Meta's display-name field is capped at 75 characters. See
    ///     https://developers.facebook.com/docs/whatsapp/business-management-api/display-name.
    /// </summary>
    public const int RequestedDisplayNameMaxLength = 75;

    public RequestWabaDisplayNameChangeValidator()
    {
        RuleFor(x => x.RequestedDisplayName)
            .NotEmpty()
            .MaximumLength(RequestedDisplayNameMaxLength)
            .Must(IsAllowedCharset)
            .WithMessage("Display name contains characters that Meta does not allow. Allowed punctuation: ' . , & - ( ) /")
            .Must(IsNotAllCapsUnlessBrand)
            .WithMessage("Display name cannot be all caps unless it is a registered brand acronym.");
    }

    // Allowed punctuation per the task brief: '.,&-()/ — plus letters (any script), digits, and
    // whitespace. We compile a single regex covering the whole string.
    private static bool IsAllowedCharset(string value)
    {
        return !string.IsNullOrEmpty(value) && AllowedCharsetRegex().IsMatch(value);
    }

    [GeneratedRegex("""^[\p{L}\p{N}\s'.,&\-()/]+$""", RegexOptions.CultureInvariant)]
    private static partial Regex AllowedCharsetRegex();

    // "All caps unless brand" — we cannot tell brand from non-brand here, so we reject pure-letter
    // strings of length >= 4 where every letter is upper-case. Short acronyms (e.g. "IBM") and
    // mixed-case names ("eBay") are allowed; the registered-brand exemption is enforced upstream
    // when Meta accepts the change.
    private static bool IsNotAllCapsUnlessBrand(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return true;
        var letters = value.Where(char.IsLetter).ToArray();
        if (letters.Length < 4) return true;
        return letters.Any(char.IsLower);
    }
}

public sealed class RequestWabaDisplayNameChangeHandler(
    IWabaConfigurationRepository repository,
    IWhatsAppCloudApiClient cloudApiClient,
    IExecutionContext executionContext,
    TimeProvider timeProvider,
    ITelemetryEventsCollector events
) : IRequestHandler<RequestWabaDisplayNameChangeCommand, Result>
{
    public async Task<Result> Handle(RequestWabaDisplayNameChangeCommand command, CancellationToken cancellationToken)
    {
        if (executionContext.UserInfo.Role != nameof(UserRole.Owner))
        {
            return Result.Forbidden("Only owners are allowed to request a WhatsApp display-name change.");
        }

        var tenantId = executionContext.TenantId;
        if (tenantId is null)
        {
            return Result.Unauthorized("No tenant selected.");
        }

        var config = await repository.GetByTenantIdAsync(tenantId, cancellationToken);
        if (config is null)
        {
            return Result.NotFound("WhatsApp configuration not found for this tenant.");
        }

        if (config.PhoneNumberId is null || config.WabaAccessToken is null)
        {
            return Result.BadRequest("WhatsApp account is not fully linked yet.");
        }

        if (config.DisplayNameStatus == WabaDisplayNameStatus.PendingReview)
        {
            return Result.BadRequest(
                "A display-name change is already pending Meta review. Wait for the current request to complete."
            );
        }

        var apiResult = await cloudApiClient.RequestDisplayNameChangeAsync(
            config.PhoneNumberId,
            config.WabaAccessToken,
            command.RequestedDisplayName,
            cancellationToken
        );
        if (!apiResult.IsSuccess)
        {
            return Result.From(apiResult);
        }

        config.RequestDisplayNameChange(command.RequestedDisplayName, timeProvider.GetUtcNow());
        repository.Update(config);

        events.CollectEvent(new WabaDisplayNameChangeRequested(
            config.TenantId, config.PhoneNumberId, command.RequestedDisplayName
        ));

        return Result.Success();
    }
}
