using Account.Database;
using Account.Features.FeatureFlags.Domain;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Domain;

namespace Account.Features.FeatureFlags;

public sealed class FeatureFlagEvaluator(AccountDbContext db)
{
    public async Task<IReadOnlyList<string>> GetEnabledFlagsAsync(TenantId tenantId, UserId? userId, CancellationToken cancellationToken)
    {
        var rows = await db.FeatureFlags
            .Where(flag => flag.TenantId == tenantId)
            .ToListAsync(cancellationToken);

        return FeatureFlagKeys.All
            .Where(key => IsEnabled(key, rows, userId))
            .ToArray();
    }

    public async Task<bool> IsEnabledAsync(string key, TenantId tenantId, UserId? userId, CancellationToken cancellationToken)
    {
        var rows = await db.FeatureFlags
            .Where(flag => flag.TenantId == tenantId && flag.Key == key)
            .ToListAsync(cancellationToken);

        return IsEnabled(key, rows, userId);
    }

    private static bool IsEnabled(string key, IReadOnlyCollection<FeatureFlag> rows, UserId? userId)
    {
        if (!FeatureFlagKeys.All.Contains(key, StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        if (userId is not null)
        {
            var userOverride = rows.FirstOrDefault(flag =>
                flag.Key.Equals(key, StringComparison.OrdinalIgnoreCase) &&
                flag.UserId == userId.ToString()
            );
            if (userOverride is not null)
            {
                return userOverride.Enabled;
            }
        }

        var tenantOverride = rows.FirstOrDefault(flag =>
            flag.Key.Equals(key, StringComparison.OrdinalIgnoreCase) &&
            flag.UserId == null
        );
        return tenantOverride?.Enabled == true;
    }
}
