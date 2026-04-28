using System.Net;
using System.Text.Json;
using Account.Database;
using Account.Features.Subscriptions.Domain;
using Account.Integrations.PayFast;
using FluentAssertions;
using SharedKernel.Tests;
using SharedKernel.Tests.Persistence;
using Xunit;

namespace Account.Tests.Subscriptions;

public sealed class HandlePayFastItnTests : EndpointBaseTest<AccountDbContext>
{
    private const string TestPassphrase = "nerovabookings";
    private const string TestToken = "test-payfast-token-abc";
    private const string TestPfPaymentId = "test-pf-payment-id-001";

    [Fact]
    public async Task HandlePayFastItn_WhenCompleteAndTrial_ShouldActivateSubscription()
    {
        // Arrange
        var payload = BuildItnPayload("COMPLETE", SubscriptionPlan.Starter, TestToken);

        // Act
        var response = await AnonymousHttpClient.PostAsync("/api/account/subscriptions/payfast/itn", payload);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var status = Connection.ExecuteScalar<string>("SELECT status FROM subscriptions WHERE tenant_id = @id", [new { id = DatabaseSeeder.Tenant1.Id.Value }]);
        status.Should().Be(nameof(SubscriptionStatus.Active));
        var plan = Connection.ExecuteScalar<string>("SELECT plan FROM subscriptions WHERE tenant_id = @id", [new { id = DatabaseSeeder.Tenant1.Id.Value }]);
        plan.Should().Be(nameof(SubscriptionPlan.Starter));
        var token = Connection.ExecuteScalar<string?>("SELECT pay_fast_token FROM subscriptions WHERE tenant_id = @id", [new { id = DatabaseSeeder.Tenant1.Id.Value }]);
        token.Should().Be(TestToken);
        TelemetryEventsCollectorSpy.CollectedEvents.Count.Should().Be(1);
        TelemetryEventsCollectorSpy.CollectedEvents[0].GetType().Name.Should().Be("SubscriptionCreated");
    }

    [Fact]
    public async Task HandlePayFastItn_WhenCompleteAndCancelled_ShouldReactivateSubscription()
    {
        // Arrange
        Connection.Update("subscriptions", "tenant_id", DatabaseSeeder.Tenant1.Id.Value, [
                ("status", nameof(SubscriptionStatus.Cancelled)),
                ("plan", nameof(SubscriptionPlan.Starter)),
                ("cancelled_at", TimeProvider.System.GetUtcNow().AddDays(-5))
            ]
        );
        TelemetryEventsCollectorSpy.Reset();
        var payload = BuildItnPayload("COMPLETE", SubscriptionPlan.Starter, TestToken);

        // Act
        var response = await AnonymousHttpClient.PostAsync("/api/account/subscriptions/payfast/itn", payload);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var status = Connection.ExecuteScalar<string>("SELECT status FROM subscriptions WHERE tenant_id = @id", [new { id = DatabaseSeeder.Tenant1.Id.Value }]);
        status.Should().Be(nameof(SubscriptionStatus.Active));
        TelemetryEventsCollectorSpy.CollectedEvents.Count.Should().Be(1);
        TelemetryEventsCollectorSpy.CollectedEvents[0].GetType().Name.Should().Be("SubscriptionReactivated");
    }

    [Fact]
    public async Task HandlePayFastItn_WhenCompleteAndAlreadyActive_ShouldRenewBillingPeriod()
    {
        // Arrange
        Connection.Update("subscriptions", "tenant_id", DatabaseSeeder.Tenant1.Id.Value, [
                ("status", nameof(SubscriptionStatus.Active)),
                ("plan", nameof(SubscriptionPlan.Standard)),
                ("pay_fast_token", TestToken),
                ("next_billing_date", TimeProvider.System.GetUtcNow()),
                ("current_period_start", TimeProvider.System.GetUtcNow().AddDays(-30)),
                ("current_period_end", TimeProvider.System.GetUtcNow())
            ]
        );
        TelemetryEventsCollectorSpy.Reset();
        var payload = BuildItnPayload("COMPLETE", SubscriptionPlan.Standard, TestToken);

        // Act
        var response = await AnonymousHttpClient.PostAsync("/api/account/subscriptions/payfast/itn", payload);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        TelemetryEventsCollectorSpy.CollectedEvents.Count.Should().Be(1);
        TelemetryEventsCollectorSpy.CollectedEvents[0].GetType().Name.Should().Be("SubscriptionRenewed");
    }

    [Fact]
    public async Task HandlePayFastItn_WhenCompleteIsReplayed_ShouldNotRenewBillingPeriodOrAddTransactionTwice()
    {
        // Arrange
        var periodEnd = TimeProvider.System.GetUtcNow();
        Connection.Update("subscriptions", "tenant_id", DatabaseSeeder.Tenant1.Id.Value, [
                ("status", nameof(SubscriptionStatus.Active)),
                ("plan", nameof(SubscriptionPlan.Standard)),
                ("pay_fast_token", TestToken),
                ("next_billing_date", periodEnd),
                ("current_period_start", periodEnd.AddDays(-30)),
                ("current_period_end", periodEnd)
            ]
        );
        var payload = BuildItnPayload("COMPLETE", SubscriptionPlan.Standard, TestToken);

        // Act
        var firstResponse = await AnonymousHttpClient.PostAsync("/api/account/subscriptions/payfast/itn", payload);
        var firstPeriodEnd = Connection.ExecuteScalar<string>("SELECT current_period_end FROM subscriptions WHERE tenant_id = @id", [new { id = DatabaseSeeder.Tenant1.Id.Value }]);
        var replayResponse = await AnonymousHttpClient.PostAsync("/api/account/subscriptions/payfast/itn", BuildItnPayload("COMPLETE", SubscriptionPlan.Standard, TestToken));

        // Assert
        firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        replayResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        CountPaymentTransactions().Should().Be(1);
        var replayedPeriodEnd = Connection.ExecuteScalar<string>("SELECT current_period_end FROM subscriptions WHERE tenant_id = @id", [new { id = DatabaseSeeder.Tenant1.Id.Value }]);
        replayedPeriodEnd.Should().Be(firstPeriodEnd);
        CountPayFastItnEvents().Should().Be(1);
    }

    [Fact]
    public async Task HandlePayFastItn_WhenFailed_ShouldSetSubscriptionToPastDue()
    {
        // Arrange
        Connection.Update("subscriptions", "tenant_id", DatabaseSeeder.Tenant1.Id.Value, [
                ("status", nameof(SubscriptionStatus.Active)),
                ("plan", nameof(SubscriptionPlan.Standard)),
                ("pay_fast_token", TestToken)
            ]
        );
        TelemetryEventsCollectorSpy.Reset();
        var payload = BuildItnPayload("FAILED", SubscriptionPlan.Standard, null);

        // Act
        var response = await AnonymousHttpClient.PostAsync("/api/account/subscriptions/payfast/itn", payload);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var status = Connection.ExecuteScalar<string>("SELECT status FROM subscriptions WHERE tenant_id = @id", [new { id = DatabaseSeeder.Tenant1.Id.Value }]);
        status.Should().Be(nameof(SubscriptionStatus.PastDue));
        TelemetryEventsCollectorSpy.CollectedEvents.Count.Should().Be(1);
        TelemetryEventsCollectorSpy.CollectedEvents[0].GetType().Name.Should().Be("PaymentFailed");
    }

    [Fact]
    public async Task HandlePayFastItn_WhenFailedIsReplayed_ShouldNotResetFirstPaymentFailedAtOrAddTransactionTwice()
    {
        // Arrange
        Connection.Update("subscriptions", "tenant_id", DatabaseSeeder.Tenant1.Id.Value, [
                ("status", nameof(SubscriptionStatus.Active)),
                ("plan", nameof(SubscriptionPlan.Standard)),
                ("pay_fast_token", TestToken)
            ]
        );
        var payload = BuildItnPayload("FAILED", SubscriptionPlan.Standard, null);

        // Act
        var firstResponse = await AnonymousHttpClient.PostAsync("/api/account/subscriptions/payfast/itn", payload);
        var firstPaymentFailedAt = Connection.ExecuteScalar<string?>("SELECT first_payment_failed_at FROM subscriptions WHERE tenant_id = @id", [new { id = DatabaseSeeder.Tenant1.Id.Value }]);
        var replayResponse = await AnonymousHttpClient.PostAsync("/api/account/subscriptions/payfast/itn", BuildItnPayload("FAILED", SubscriptionPlan.Standard, null));

        // Assert
        firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        replayResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        CountPaymentTransactions().Should().Be(1);
        var replayedFirstPaymentFailedAt = Connection.ExecuteScalar<string?>("SELECT first_payment_failed_at FROM subscriptions WHERE tenant_id = @id", [new { id = DatabaseSeeder.Tenant1.Id.Value }]);
        replayedFirstPaymentFailedAt.Should().Be(firstPaymentFailedAt);
        CountPayFastItnEvents().Should().Be(1);
    }

    [Fact]
    public async Task HandlePayFastItn_WhenFailedThenComplete_ShouldRecoverSubscriptionAndRecordDistinctEvents()
    {
        // Arrange
        Connection.Update("subscriptions", "tenant_id", DatabaseSeeder.Tenant1.Id.Value, [
                ("status", nameof(SubscriptionStatus.Active)),
                ("plan", nameof(SubscriptionPlan.Standard)),
                ("pay_fast_token", TestToken)
            ]
        );

        // Act
        var failedResponse = await AnonymousHttpClient.PostAsync("/api/account/subscriptions/payfast/itn", BuildItnPayload("FAILED", SubscriptionPlan.Standard, null));
        var completeResponse = await AnonymousHttpClient.PostAsync("/api/account/subscriptions/payfast/itn", BuildItnPayload("COMPLETE", SubscriptionPlan.Standard, TestToken));

        // Assert
        failedResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        completeResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var status = Connection.ExecuteScalar<string>("SELECT status FROM subscriptions WHERE tenant_id = @id", [new { id = DatabaseSeeder.Tenant1.Id.Value }]);
        status.Should().Be(nameof(SubscriptionStatus.Active));
        var firstPaymentFailedAt = Connection.ExecuteScalar<DateTimeOffset?>("SELECT first_payment_failed_at FROM subscriptions WHERE tenant_id = @id", [new { id = DatabaseSeeder.Tenant1.Id.Value }]);
        firstPaymentFailedAt.Should().BeNull();
        CountPaymentTransactions().Should().Be(2);
        CountPayFastItnEvents().Should().Be(2);
    }

    [Fact]
    public async Task HandlePayFastItn_WhenSameCompleteArrivesConcurrently_ShouldOnlyRecordOneTransaction()
    {
        // Arrange
        Connection.Update("subscriptions", "tenant_id", DatabaseSeeder.Tenant1.Id.Value, [
                ("status", nameof(SubscriptionStatus.Active)),
                ("plan", nameof(SubscriptionPlan.Standard)),
                ("pay_fast_token", TestToken),
                ("next_billing_date", TimeProvider.System.GetUtcNow()),
                ("current_period_start", TimeProvider.System.GetUtcNow().AddDays(-30)),
                ("current_period_end", TimeProvider.System.GetUtcNow())
            ]
        );

        // Act
        var responses = await Task.WhenAll(
            AnonymousHttpClient.PostAsync("/api/account/subscriptions/payfast/itn", BuildItnPayload("COMPLETE", SubscriptionPlan.Standard, TestToken)),
            AnonymousHttpClient.PostAsync("/api/account/subscriptions/payfast/itn", BuildItnPayload("COMPLETE", SubscriptionPlan.Standard, TestToken))
        );

        // Assert
        responses.Should().OnlyContain(response => response.StatusCode == HttpStatusCode.OK);
        CountPaymentTransactions().Should().Be(1);
        CountPayFastItnEvents().Should().Be(1);
    }

    [Fact]
    public async Task HandlePayFastItn_WhenInvalidSignature_ShouldReturnBadRequest()
    {
        // Arrange
        var fields = new Dictionary<string, string>
        {
            { "merchant_id", "10043122" },
            { "payment_status", "COMPLETE" },
            { "pf_payment_id", TestPfPaymentId },
            { "custom_str2", DatabaseSeeder.Tenant1.Id.Value.ToString() },
            { "custom_str3", nameof(SubscriptionPlan.Starter) },
            { "signature", "invalidsignaturethatdoesnotmatch" }
        };
        var payload = new FormUrlEncodedContent(fields);

        // Act
        var response = await AnonymousHttpClient.PostAsync("/api/account/subscriptions/payfast/itn", payload);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        CountPayFastItnEvents().Should().Be(0);
    }

    [Fact]
    public async Task HandlePayFastItn_WhenMissingTenantId_ShouldReturnBadRequest()
    {
        // Arrange
        var fields = new SortedDictionary<string, string>
        {
            { "merchant_id", "10043122" },
            { "payment_status", "COMPLETE" },
            { "pf_payment_id", TestPfPaymentId },
            { "custom_str3", nameof(SubscriptionPlan.Starter) }
        };
        fields["signature"] = PayFastSignature.Generate(fields, TestPassphrase);
        var payload = new FormUrlEncodedContent(fields);

        // Act
        var response = await AnonymousHttpClient.PostAsync("/api/account/subscriptions/payfast/itn", payload);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task HandlePayFastItn_WhenUnknownTenantId_ShouldReturnNotFound()
    {
        // Arrange
        var payload = BuildItnPayload("COMPLETE", SubscriptionPlan.Starter, TestToken, tenantId: 999999);

        // Act
        var response = await AnonymousHttpClient.PostAsync("/api/account/subscriptions/payfast/itn", payload);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private FormUrlEncodedContent BuildItnPayload(string status, SubscriptionPlan plan, string? token, long? tenantId = null)
    {
        var fields = new SortedDictionary<string, string>
        {
            { "merchant_id", "10043122" },
            { "payment_status", status },
            { "pf_payment_id", TestPfPaymentId },
            { "custom_str2", (tenantId ?? DatabaseSeeder.Tenant1.Id.Value).ToString() },
            { "custom_str3", plan.ToString() }
        };
        if (token is not null) fields["token"] = token;
        fields["signature"] = PayFastSignature.GenerateForItn(fields, TestPassphrase);
        return new FormUrlEncodedContent(fields);
    }

    private int CountPaymentTransactions()
    {
        var transactionsJson = Connection.ExecuteScalar<string>("SELECT payment_transactions FROM subscriptions WHERE tenant_id = @id", [new { id = DatabaseSeeder.Tenant1.Id.Value }]);
        using var document = JsonDocument.Parse(transactionsJson);
        return document.RootElement.GetArrayLength();
    }

    private long CountPayFastItnEvents()
    {
        return Connection.ExecuteScalar<long>("SELECT COUNT(*) FROM pay_fast_itn_events", []);
    }
}
