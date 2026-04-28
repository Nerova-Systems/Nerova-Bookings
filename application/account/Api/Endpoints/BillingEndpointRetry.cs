using Account.Database;
using Microsoft.EntityFrameworkCore;
using SharedKernel.ApiResults;

namespace Account.Api.Endpoints;

internal static class BillingEndpointRetry
{
    private static readonly SemaphoreSlim SqliteBillingWriteLock = new(1, 1);

    public static async Task<ApiResult> ExecuteAsync(
        Func<Task<ApiResult>> operation,
        AccountDbContext dbContext,
        ILogger logger
    )
    {
        if (!IsSqlite(dbContext)) return await ExecuteWithRetryAsync(operation, dbContext, logger);

        await SqliteBillingWriteLock.WaitAsync();
        try
        {
            return await ExecuteWithRetryAsync(operation, dbContext, logger);
        }
        finally
        {
            SqliteBillingWriteLock.Release();
        }
    }

    public static async Task<ApiResult<T>> ExecuteAsync<T>(
        Func<Task<ApiResult<T>>> operation,
        AccountDbContext dbContext,
        ILogger logger
    )
    {
        if (!IsSqlite(dbContext)) return await ExecuteWithRetryAsync(operation, dbContext, logger);

        await SqliteBillingWriteLock.WaitAsync();
        try
        {
            return await ExecuteWithRetryAsync(operation, dbContext, logger);
        }
        finally
        {
            SqliteBillingWriteLock.Release();
        }
    }

    private static async Task<ApiResult> ExecuteWithRetryAsync(
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
                await Task.Delay(TimeSpan.FromMilliseconds(20));
            }
        }

        return await operation();
    }

    private static async Task<ApiResult<T>> ExecuteWithRetryAsync<T>(
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
                await Task.Delay(TimeSpan.FromMilliseconds(20));
            }
        }

        return await operation();
    }

    private static bool IsSqlite(AccountDbContext dbContext)
    {
        return dbContext.Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite";
    }

    private static bool IsRetryableBillingWrite(Exception exception)
    {
        return exception is DbUpdateConcurrencyException or DbUpdateException ||
               exception is InvalidOperationException { Message: "SqliteConnection does not support nested transactions." };
    }
}
