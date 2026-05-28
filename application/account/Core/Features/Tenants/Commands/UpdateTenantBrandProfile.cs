using System.Text.Json;
using Account.Features.Subscriptions.Domain;
using Account.Features.Tenants.Domain;
using Account.Features.Users.Domain;
using Account.Features.WhatsApp.Domain;
using Account.Features.WhatsApp.Infrastructure;
using FluentValidation;
using JetBrains.Annotations;
using SharedKernel.Authentication;
using SharedKernel.Cqrs;
using SharedKernel.ExecutionContext;
using SharedKernel.Telemetry;

namespace Account.Features.Tenants.Commands;

/// <summary>
///     Updates the current tenant's <see cref="BrandProfile" /> and enqueues a row in the
///     <see cref="WabaProfileSyncOutbox" /> so the Phase 7b sync job can propagate the change to
///     Meta. The command is the only entry point that mutates BrandProfile — there is no separate
///     "set logo" command at this layer (logo URL is one of the BrandProfile fields).
/// </summary>
[PublicAPI]
public sealed record UpdateTenantBrandProfileCommand : ICommand, IRequest<Result>
{
    public string? BusinessDisplayName { get; init; }
    public string? BrandLogoUrl { get; init; }
    public string? BrandAboutText { get; init; }
    public string? BrandDescription { get; init; }
    public string? BrandAddress { get; init; }
    public string? BrandEmail { get; init; }
    public IReadOnlyList<string>? BrandWebsites { get; init; }
    public MetaBusinessVertical BrandVertical { get; init; } = MetaBusinessVertical.Other;
}

public sealed class UpdateTenantBrandProfileValidator : AbstractValidator<UpdateTenantBrandProfileCommand>
{
    public UpdateTenantBrandProfileValidator()
    {
        // Light surface validation; deep invariants live on BrandProfile.Create. FluentValidation
        // gives a friendlier 400 with the property name when the caller obviously over-stuffs a
        // field; BrandProfile.Create is the source of truth.
        RuleFor(x => x.BusinessDisplayName)
            .MaximumLength(BrandProfile.BusinessDisplayNameMaxLength);
        RuleFor(x => x.BrandAboutText)
            .MaximumLength(BrandProfile.BrandAboutTextMaxLength);
        RuleFor(x => x.BrandDescription)
            .MaximumLength(BrandProfile.BrandDescriptionMaxLength);
        RuleFor(x => x.BrandAddress)
            .MaximumLength(BrandProfile.BrandAddressMaxLength);
        RuleFor(x => x.BrandEmail)
            .MaximumLength(BrandProfile.BrandEmailMaxLength)
            .EmailAddress()
            .When(x => !string.IsNullOrWhiteSpace(x.BrandEmail));
        RuleFor(x => x.BrandWebsites!.Count)
            .LessThanOrEqualTo(BrandProfile.MaxBrandWebsites)
            .When(x => x.BrandWebsites is not null)
            .WithMessage($"At most {BrandProfile.MaxBrandWebsites} websites are allowed.");
    }
}

public sealed class UpdateTenantBrandProfileHandler(
    ITenantRepository tenantRepository,
    ISubscriptionRepository subscriptionRepository,
    IWabaConfigurationRepository wabaConfigurationRepository,
    IWabaProfileSyncOutboxRepository outboxRepository,
    IExecutionContext executionContext,
    TimeProvider timeProvider,
    ITelemetryEventsCollector events
) : IRequestHandler<UpdateTenantBrandProfileCommand, Result>
{
    public async Task<Result> Handle(UpdateTenantBrandProfileCommand command, CancellationToken cancellationToken)
    {
        if (executionContext.UserInfo.Role != nameof(UserRole.Owner))
        {
            return Result.Forbidden("Only owners are allowed to update the brand profile.");
        }

        var tenant = await tenantRepository.GetCurrentTenantAsync(cancellationToken);
        if (tenant is null)
        {
            return Result.Unauthorized("Tenant has been deleted.", responseHeaders: new Dictionary<string, string>
                {
                    { AuthenticationTokenHttpKeys.UnauthorizedReasonHeaderKey, nameof(UnauthorizedReason.TenantDeleted) }
                }
            );
        }

        // ─── Tier enforcement ────────────────────────────────────────────
        // Free / null-plan tenants get a read-only profile (display name + vertical only); paid
        // tiers unlock logo, custom about/description, and address fields; Standard+ unlocks the
        // second website slot. Limits are kept inline (no centralised FeatureFlags.cs in account
        // SCS) to match the existing `LinkWabaAccountHandler.PhoneNumberLimitForPlan` pattern.
        var subscription = await subscriptionRepository.GetByTenantIdUnfilteredAsync(tenant.Id, cancellationToken);
        var limits = LimitsFor(subscription?.Plan);

        if (!limits.AllowsLogo && !string.IsNullOrWhiteSpace(command.BrandLogoUrl))
        {
            return Result.Forbidden("Uploading a brand logo requires the Basis plan or higher.");
        }

        if (!limits.AllowsAbout && !string.IsNullOrWhiteSpace(command.BrandAboutText))
        {
            return Result.Forbidden("Customising the brand 'about' text requires the Basis plan or higher.");
        }

        if (!limits.AllowsAddress && !string.IsNullOrWhiteSpace(command.BrandAddress))
        {
            return Result.Forbidden("Setting a brand address requires the Basis plan or higher.");
        }

        if (!limits.AllowsDescription && !string.IsNullOrWhiteSpace(command.BrandDescription))
        {
            return Result.Forbidden("Setting a brand description requires the Basis plan or higher.");
        }

        var websiteCount = command.BrandWebsites?.Count ?? 0;
        if (websiteCount > limits.MaxWebsites)
        {
            return Result.Forbidden(
                $"Your current plan allows at most {limits.MaxWebsites} brand website(s)."
            );
        }

        // ─── Build BrandProfile (deep validation) ────────────────────────
        BrandProfile profile;
        try
        {
            profile = BrandProfile.Create(
                command.BusinessDisplayName,
                command.BrandLogoUrl,
                command.BrandAboutText,
                command.BrandDescription,
                command.BrandAddress,
                command.BrandEmail,
                command.BrandWebsites,
                command.BrandVertical
            );
        }
        catch (ArgumentException ex)
        {
            return Result.BadRequest(ex.ParamName ?? "brandProfile", ex.Message);
        }

        tenant.UpdateBrandProfile(profile);
        tenantRepository.Update(tenant);

        // ─── Enqueue sync (only if the tenant has linked a WABA) ─────────
        // The sync job needs a phone number id to call Meta. When the tenant has not finished
        // WABA onboarding we still persist BrandProfile (so the UI can render it) but skip the
        // outbox row — the LinkWabaAccount flow will trigger a sync when the phone is registered.
        var waba = await wabaConfigurationRepository.GetByTenantIdAsync(tenant.Id, cancellationToken);
        if (waba?.PhoneNumberId is not null)
        {
            var dto = WabaProfileMapper.Map(profile, profilePictureHandle: null);
            var serialized = JsonSerializer.Serialize(dto);
            var now = timeProvider.GetUtcNow();
            var outbox = WabaProfileSyncOutbox.Enqueue(
                tenant.Id,
                waba.PhoneNumberId,
                serialized,
                profile.BrandLogoUrl,
                now
            );
            await outboxRepository.AddAsync(outbox, cancellationToken);
        }

        events.CollectEvent(new TenantBrandProfileUpdated(profile.BrandVertical.ToString()));

        return Result.Success();
    }

    private static TierLimits LimitsFor(SubscriptionPlan? plan)
    {
        // Free (null plan) is read-only beyond display name + vertical; Basis+ unlocks the rest.
        // Standard+ unlocks the second website slot. The codebase has no "Free" enum value — the
        // absence of a Subscription row models the free tier, matching how `Subscriptions/Domain`
        // treats `null` plans throughout the account SCS.
        return plan switch
        {
            null => new TierLimits(false, false, false, false, MaxWebsites: 1),
            SubscriptionPlan.Basis => new TierLimits(true, true, true, true, MaxWebsites: 1),
            SubscriptionPlan.Standard => new TierLimits(true, true, true, true, MaxWebsites: 2),
            SubscriptionPlan.Premium => new TierLimits(true, true, true, true, MaxWebsites: 2),
            _ => new TierLimits(false, false, false, false, MaxWebsites: 1)
        };
    }

    private readonly record struct TierLimits(
        bool AllowsLogo,
        bool AllowsAbout,
        bool AllowsAddress,
        bool AllowsDescription,
        int MaxWebsites
    );
}
