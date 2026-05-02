using Account.Database;
using Account.Features.FeatureFlags;
using Account.Features.FeatureFlags.Domain;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Domain;
using SharedKernel.Endpoints;
using SharedKernel.ExecutionContext;
using SharedKernel.OpenApi;

namespace Account.Api.Endpoints;

public sealed class FeatureFlagEndpoints : IEndpoints
{
    private const string RoutesPrefix = "/api/account/feature-flags";

    public void MapEndpoints(IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup(RoutesPrefix).WithTags("Feature flags").WithGroupName(OpenApiDocumentNames.Account).RequireAuthorization();

        group.MapGet("/", GetFeatureFlags);
        group.MapPut("/{key}/tenant-override", SetTenantOverride);
        group.MapPut("/{key}/user-override", SetUserOverride);
    }

    private static async Task<IResult> GetFeatureFlags(FeatureFlagEvaluator evaluator, IExecutionContext executionContext, CancellationToken cancellationToken)
    {
        var tenantId = RequireTenant(executionContext);
        return Results.Ok(await evaluator.GetEnabledFlagsAsync(tenantId, executionContext.UserInfo.Id, cancellationToken));
    }

    private static async Task<IResult> SetTenantOverride(string key, FeatureFlagOverrideRequest request, AccountDbContext db, IExecutionContext executionContext, TimeProvider timeProvider, CancellationToken cancellationToken)
    {
        var tenantId = RequireTenant(executionContext);
        var normalizedKey = NormalizeKey(key);
        if (normalizedKey is null) return Results.NotFound();

        var flag = await db.FeatureFlags.AsTracking().FirstOrDefaultAsync(item =>
            item.TenantId == tenantId &&
            item.Key == normalizedKey &&
            item.UserId == null,
            cancellationToken
        );
        if (flag is null)
        {
            flag = new FeatureFlag { TenantId = tenantId, Key = normalizedKey };
            db.FeatureFlags.Add(flag);
        }

        flag.Enabled = request.Enabled;
        flag.UpdatedAt = timeProvider.GetUtcNow();
        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok();
    }

    private static async Task<IResult> SetUserOverride(string key, FeatureFlagUserOverrideRequest request, AccountDbContext db, IExecutionContext executionContext, TimeProvider timeProvider, CancellationToken cancellationToken)
    {
        var tenantId = RequireTenant(executionContext);
        var normalizedKey = NormalizeKey(key);
        if (normalizedKey is null) return Results.NotFound();
        if (string.IsNullOrWhiteSpace(request.UserId)) return Results.BadRequest("User id is required.");

        var userId = request.UserId.Trim();
        var flag = await db.FeatureFlags.AsTracking().FirstOrDefaultAsync(item =>
            item.TenantId == tenantId &&
            item.Key == normalizedKey &&
            item.UserId == userId,
            cancellationToken
        );
        if (flag is null)
        {
            flag = new FeatureFlag { TenantId = tenantId, Key = normalizedKey, UserId = userId };
            db.FeatureFlags.Add(flag);
        }

        flag.Enabled = request.Enabled;
        flag.UpdatedAt = timeProvider.GetUtcNow();
        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok();
    }

    private static TenantId RequireTenant(IExecutionContext executionContext)
    {
        return executionContext.TenantId ?? throw new InvalidOperationException("Tenant context is required.");
    }

    private static string? NormalizeKey(string key)
    {
        return FeatureFlagKeys.All.FirstOrDefault(item => item.Equals(key, StringComparison.OrdinalIgnoreCase));
    }
}

public sealed record FeatureFlagOverrideRequest(bool Enabled);
public sealed record FeatureFlagUserOverrideRequest(string UserId, bool Enabled);
