using SharedKernel.Domain;

namespace Main.Features.Insights.Shared;

/// <summary>
///     Resolved execution context for insights queries: who owns the data and whether it is team-scoped.
/// </summary>
public sealed record InsightsScope(TenantId TenantId, TenantId? TeamId, UserId UserId);
