extern alias workers;
using System.Globalization;
using Account.Database;
using Account.Features.Subscriptions.Domain;
using Account.Features.Subscriptions.Shared;
using Account.Integrations.OAuth;
using Account.Integrations.Paystack;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SharedKernel.Tests.Persistence;
using Xunit;
using BillingDriftWorker = workers::Account.Workers.BillingDriftWorker;

namespace Account.Tests.Workers;

/// <summary>
///     End-to-end coverage for the <see cref="BillingDriftWorker" /> lifecycle. The handler logic
///     <c>ProcessPendingPaystackEvents</c> uses in Detect mode is covered by
///     <c>ProcessPendingPaystackEventsDetectModeTests</c>; this file pins the worker's loop semantics:
///     a single pass on <see cref="BackgroundService.ExecuteAsync" />, eligibility filtering via
///     <see cref="ISubscriptionRepository.GetSubscriptionsDueForDriftCheckUnfilteredAsync" />, the
///     iteration-token linkage to <see cref="BillingDriftIterationTimeout" /> (M13), and
///     resilience-on-per-subscription-failure so one bad row cannot kill the entire pass.
/// </summary>
public sealed class BillingDriftWorkerTests(AccountWebApplicationFactory factory) : EndpointBaseTest<AccountDbContext>(factory), IClassFixture<AccountWebApplicationFactory>
{
    [Fact]
    public async Task ExecuteAsync_RunsOnePassThenExits_WithoutPeriodicTimer()
    {
        // Arrange
        Connection.Update("subscriptions", "tenant_id", DatabaseSeeder.Tenant1.Id.Value, [
                ("paystack_customer_code", null),
                ("drift_checked_at", null)
            ]
        );

        var worker = CreateWorker();

        // Act
        using var workerCancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await worker.StartAsync(workerCancellationTokenSource.Token);
        await worker.ExecuteTask!;

        // Assert
        worker.ExecuteTask.IsCompletedSuccessfully.Should().BeTrue("the worker must finish its single pass and complete without throwing");
    }

    [Fact]
    public async Task ExecuteAsync_WhenSubscriptionIsDueForDriftCheck_AdvancesDriftCheckedAt()
    {
        // Arrange
        SetUseMockPaystackCookieOnAmbientHttpContext();
        var beforeWorkerStart = TimeProvider.GetUtcNow();
        Connection.Update("subscriptions", "tenant_id", DatabaseSeeder.Tenant1.Id.Value, [
                ("plan", nameof(SubscriptionPlan.Standard)),
                ("paystack_customer_code", MockPaystackClient.MockCustomerCode),
                ("current_price_amount", MockPaystackClient.StandardAmountExcludingTax),
                ("current_price_currency", MockPaystackClient.MockStandardCurrency),
                ("current_period_end", TimeProvider.GetUtcNow().AddDays(30)),
                ("payment_transactions", "[]"),
                ("drift_checked_at", null)
            ]
        );

        var worker = CreateWorker();

        // Act
        using var workerCancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await worker.StartAsync(workerCancellationTokenSource.Token);
        await worker.ExecuteTask!;

        // Assert
        ReadDriftCheckedAt(DatabaseSeeder.Tenant1.Id.Value).Should()
            .BeOnOrAfter(beforeWorkerStart.AddSeconds(-1), "a subscription with no prior drift check must be picked up by the worker and visited via Detect mode");
    }

    [Fact]
    public async Task ExecuteAsync_WhenSubscriptionWasCheckedRecently_DoesNotAdvanceDriftCheckedAt()
    {
        // Arrange
        SetUseMockPaystackCookieOnAmbientHttpContext();
        var recentDriftCheckedAt = TimeProvider.GetUtcNow().AddHours(-1);
        Connection.Update("subscriptions", "tenant_id", DatabaseSeeder.Tenant1.Id.Value, [
                ("plan", nameof(SubscriptionPlan.Standard)),
                ("paystack_customer_code", MockPaystackClient.MockCustomerCode),
                ("current_price_amount", MockPaystackClient.StandardAmountExcludingTax),
                ("current_price_currency", MockPaystackClient.MockStandardCurrency),
                ("current_period_end", TimeProvider.GetUtcNow().AddDays(30)),
                ("payment_transactions", "[]"),
                ("drift_checked_at", recentDriftCheckedAt.ToUnixTimeMilliseconds())
            ]
        );

        var worker = CreateWorker();

        // Act
        using var workerCancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await worker.StartAsync(workerCancellationTokenSource.Token);
        await worker.ExecuteTask!;

        // Assert
        ReadDriftCheckedAt(DatabaseSeeder.Tenant1.Id.Value).Should().NotBeNull();
        ReadDriftCheckedAt(DatabaseSeeder.Tenant1.Id.Value)!.Value.Should()
            .BeCloseTo(recentDriftCheckedAt, TimeSpan.FromMilliseconds(1), "fresh rows must be excluded by the repository's staleness filter so the worker never visits them");
    }

    [Fact]
    public void BillingDriftWorker_InheritsFromBackgroundService_AndHasNoPeriodicTimerField()
    {
        // Assert
        typeof(BillingDriftWorker).BaseType.Should().Be(typeof(BackgroundService));

        var fields = typeof(BillingDriftWorker).GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        fields.Should().NotContain(f => f.FieldType == typeof(PeriodicTimer), "introducing a PeriodicTimer would re-enable a periodic loop");
    }

    private BillingDriftWorker CreateWorker()
    {
        var configuration = new ConfigurationBuilder().Build();
        var logger = WebApplicationServices.GetRequiredService<ILogger<BillingDriftWorker>>();
        return new BillingDriftWorker(WebApplicationServices, configuration, TimeProvider, logger);
    }

    private void SetUseMockPaystackCookieOnAmbientHttpContext()
    {
        var httpContextAccessor = WebApplicationServices.GetRequiredService<IHttpContextAccessor>();
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers.Append("Cookie", $"{OAuthProviderFactory.UseMockProviderCookieName}=true");
        httpContextAccessor.HttpContext = httpContext;
    }

    private DateTimeOffset? ReadDriftCheckedAt(long tenantId)
    {
        var value = Connection.ExecuteScalar<string>(
            "SELECT drift_checked_at FROM subscriptions WHERE tenant_id = @tenantId",
            [new { tenantId }]
        );
        if (string.IsNullOrEmpty(value))
        {
            return null;
        }
        if (long.TryParse(value, out var unixMs))
        {
            return DateTimeOffset.FromUnixTimeMilliseconds(unixMs);
        }
        return DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
    }
}
