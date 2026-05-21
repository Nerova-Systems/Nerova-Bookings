using System.Net;
using FluentAssertions;
using SharedKernel.Tests;
using Xunit;

namespace Main.Tests.Insights;

/// <summary>
///     Tests auth guard (no JWT → 401) and feature flag gate (no flag → 403) for all insights endpoints.
///     Also verifies the basic shape of valid responses (non-null DTO).
/// </summary>
public sealed class InsightsEndpointTests : InsightsEndpointBaseTest
{
    private static readonly string[] AllEndpoints =
    [
        "/api/insights/kpis?from=2025-01-01T00:00:00Z&to=2026-01-01T00:00:00Z",
        "/api/insights/bookings-over-time?from=2025-01-01T00:00:00Z&to=2026-01-01T00:00:00Z",
        "/api/insights/top-event-types?from=2025-01-01T00:00:00Z&to=2026-01-01T00:00:00Z",
        "/api/insights/top-hosts?from=2025-01-01T00:00:00Z&to=2026-01-01T00:00:00Z",
        "/api/insights/heatmap?from=2025-01-01T00:00:00Z&to=2026-01-01T00:00:00Z",
        "/api/insights/funnel?from=2025-01-01T00:00:00Z&to=2026-01-01T00:00:00Z",
        "/api/insights/cancellation-reasons?from=2025-01-01T00:00:00Z&to=2026-01-01T00:00:00Z"
    ];

    [Fact]
    public async Task AllEndpoints_WhenAnonymous_ShouldReturnUnauthorized()
    {
        foreach (var url in AllEndpoints)
        {
            var response = await AnonymousHttpClient.GetAsync(url);
            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized, $"endpoint {url} should be protected");
        }
    }

    [Fact]
    public async Task AllEndpoints_WhenAuthenticatedButFeatureFlagDisabled_ShouldReturnForbidden()
    {
        foreach (var url in AllEndpoints)
        {
            var response = await AuthenticatedOwnerHttpClient.GetAsync(url);
            response.StatusCode.Should().Be(HttpStatusCode.Forbidden, $"endpoint {url} should require cap-insights flag");
        }
    }

    [Fact]
    public async Task AllEndpoints_WhenFeatureFlagEnabled_ShouldReturnSuccessfulResponse()
    {
        foreach (var url in AllEndpoints)
        {
            var response = await InsightsClient.GetAsync(url);
            response.ShouldBeSuccessfulGetRequest();
        }
    }

    [Fact]
    public async Task KpisEndpoint_WhenToBeforeFrom_ShouldReturnValidationError()
    {
        var response = await InsightsClient.GetAsync("/api/insights/kpis?from=2026-01-01T00:00:00Z&to=2025-01-01T00:00:00Z");
        await response.ShouldHaveErrorStatusCode(HttpStatusCode.BadRequest, (string?)null);
    }
}
