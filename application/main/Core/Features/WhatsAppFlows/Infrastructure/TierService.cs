using JetBrains.Annotations;
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

public interface ITierService
{
    Task<TenantTier> GetTierAsync(TenantId tenantId, CancellationToken cancellationToken);
}

/// <summary>
///     Placeholder tier service — returns <see cref="TenantTier.Professional" /> for every tenant.
///     A future increment will resolve the tier from the subscription record (in the account SCS).
/// </summary>
public sealed class DefaultTierService : ITierService
{
    public Task<TenantTier> GetTierAsync(TenantId tenantId, CancellationToken cancellationToken)
    {
        return Task.FromResult(TenantTier.Professional);
    }
}

public static class TierLimits
{
    public static int MaxCustomQuestions(TenantTier tier)
    {
        return tier switch
        {
            TenantTier.Starter => 0,
            TenantTier.Professional => 1,
            TenantTier.Business => 3,
            TenantTier.Enterprise => int.MaxValue,
            _ => 0
        };
    }
}
