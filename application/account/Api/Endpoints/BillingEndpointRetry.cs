using Account.Database;
using Microsoft.EntityFrameworkCore;
using SharedKernel.ApiResults;

namespace Account.Api.Endpoints;

internal static class BillingEndpointRetry
{
    public static async Task<ApiResult> ExecuteAsync(
        Func<Task<ApiResult>> operation,
        AccountDbContext dbContext,
        ILogger logger
    )
    {
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            try
            {
                return await operation();
            }
            catch (Exception ex) when (IsRetryableBillingWrite(ex) && attempt < 3)
            {
                logger.LogWarning(ex, "Retrying billing write after concurrency conflict. Attempt {Attempt}.", attempt);
                dbContext.ChangeTracker.Clear();
            }
        }

        return await operation();
    }

    public static async Task<ApiResult<T>> ExecuteAsync<T>(
        Func<Task<ApiResult<T>>> operation,
        AccountDbContext dbContext,
        ILogger logger
    )
    {
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            try
            {
                return await operation();
            }
            catch (Exception ex) when (IsRetryableBillingWrite(ex) && attempt < 3)
            {
                logger.LogWarning(ex, "Retrying billing write after concurrency conflict. Attempt {Attempt}.", attempt);
                dbContext.ChangeTracker.Clear();
            }
        }

        return await operation();
    }

    private static bool IsRetryableBillingWrite(Exception exception)
    {
        return exception is DbUpdateConcurrencyException or DbUpdateException;
    }
}
