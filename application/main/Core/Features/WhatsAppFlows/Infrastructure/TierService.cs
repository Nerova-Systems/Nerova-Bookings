using JetBrains.Annotations;
using Microsoft.Extensions.Caching.Memory;
using SharedKernel.Domain;

namespace Main.Features.WhatsAppFlows.Infrastructure;

[PublicAPI]
public enum TenantTier
{
    Starter,
    Professional,
    Business,
    Enterprise
}

/// <summary>
///     Choice of payment-timing options surfaced in the questionnaire / Flow JSON.
///     <c>AfterOnly</c> = only <see cref="PaymentTiming.AfterSession" />; <c>Both</c> = any.
/// </summary>
[PublicAPI]
public enum PaymentTimingChoice
{
    AfterOnly,
    Both
}

/// <summary>Granularity of analytics surfaced for a tier.</summary>
[PublicAPI]
public enum FlowAnalyticsTier
{
    Basic,
    Standard,
    Advanced,
    Full
}

public interface ITierService
{
    Task<TenantTier> GetTierAsync(TenantId tenantId, CancellationToken cancellationToken);
}

/// <summary>
///     Resolves the current tenant's WhatsApp Flows tier by looking up the active subscription
///     in the account SCS via the internal subscription endpoint. Results are memoized per
///     tenant for <see cref="CacheTtlSeconds" /> seconds so that questionnaire/Flow validation
///     and template rendering inside a single request batch don't fan-out N HTTP calls.
///     <para>
///         Subscription plan → tier mapping (this codebase's <c>SubscriptionPlan</c> enum maps
///         to the idealized tier names in the WhatsApp Flows plan):
///         <list type="bullet">
///             <item><c>null</c> (no subscription) → <see cref="TenantTier.Starter" /></item>
///             <item><c>Basis</c> → <see cref="TenantTier.Professional" /></item>
///             <item><c>Standard</c> → <see cref="TenantTier.Business" /></item>
///             <item><c>Premium</c> → <see cref="TenantTier.Enterprise" /></item>
///         </list>
///     </para>
/// </summary>
public sealed class DefaultTierService(
    IWhatsAppSubscriptionLookup subscriptionLookup,
    IMemoryCache cache
) : ITierService
{
    private const int CacheTtlSeconds = 60;

    public async Task<TenantTier> GetTierAsync(TenantId tenantId, CancellationToken cancellationToken)
    {
        var key = $"whatsapp-tier:{tenantId.Value}";
        if (cache.TryGetValue(key, out TenantTier cached)) return cached;

        var plan = await subscriptionLookup.GetSubscriptionPlanAsync(tenantId, cancellationToken);
        var tier = MapPlanToTier(plan);
        cache.Set(key, tier, TimeSpan.FromSeconds(CacheTtlSeconds));
        return tier;
    }

    internal static TenantTier MapPlanToTier(string? plan)
    {
        return plan switch
        {
            null => TenantTier.Starter,
            "Basis" => TenantTier.Professional,
            "Standard" => TenantTier.Business,
            "Premium" => TenantTier.Enterprise,
            _ => TenantTier.Starter
        };
    }
}

/// <summary>
///     Per-tier value lookups for WhatsApp Flows. Boolean tier flags use feature flags (see
///     <c>whatsapp-flows-enabled</c>); the methods below carry the non-boolean per-tier limits
///     that don't fit the existing <c>FeatureFlagDefinition</c> shape. A <c>-1</c> return from
///     any int-valued method means "unlimited".
/// </summary>
public static class TierLimits
{
    /// <summary>Maximum custom pre-booking questions a tenant may configure. <c>-1</c> = unlimited.</summary>
    public static int MaxCustomPreBookingQuestions(TenantTier tier)
    {
        return tier switch
        {
            TenantTier.Starter => 0,
            TenantTier.Professional => 1,
            TenantTier.Business => 3,
            TenantTier.Enterprise => -1,
            _ => 0
        };
    }

    /// <summary>Legacy alias — kept so existing call sites that take the "infinity = int.MaxValue" semantics keep working.</summary>
    public static int MaxCustomQuestions(TenantTier tier)
    {
        var limit = MaxCustomPreBookingQuestions(tier);
        return limit == -1 ? int.MaxValue : limit;
    }

    public static bool MultipleServicesInFlow(TenantTier tier)
    {
        return tier != TenantTier.Starter;
    }

    public static bool StaffSelectionInFlow(TenantTier tier)
    {
        return tier != TenantTier.Starter;
    }

    public static PaymentTimingChoice PaymentTimingChoice(TenantTier tier)
    {
        return tier == TenantTier.Starter ? Infrastructure.PaymentTimingChoice.AfterOnly : Infrastructure.PaymentTimingChoice.Both;
    }

    public static bool CustomConfirmationMessage(TenantTier tier)
    {
        return tier != TenantTier.Starter;
    }

    /// <summary>Maximum WABA phone numbers per tenant. <c>-1</c> = unlimited.</summary>
    public static int MultiplePhoneNumbers(TenantTier tier)
    {
        return tier switch
        {
            TenantTier.Starter => 1,
            TenantTier.Professional => 1,
            TenantTier.Business => 3,
            TenantTier.Enterprise => -1,
            _ => 1
        };
    }

    public static FlowAnalyticsTier FlowAnalyticsDashboard(TenantTier tier)
    {
        return tier switch
        {
            TenantTier.Starter => FlowAnalyticsTier.Basic,
            TenantTier.Professional => FlowAnalyticsTier.Standard,
            TenantTier.Business => FlowAnalyticsTier.Advanced,
            TenantTier.Enterprise => FlowAnalyticsTier.Full,
            _ => FlowAnalyticsTier.Basic
        };
    }
}
