using System.Net;
using Account.Database;
using Account.Features.Subscriptions.Domain;
using Account.Integrations.PayFast;
using FluentAssertions;
using SharedKernel.Tests;
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
        var status = Connection.ExecuteScalar<string>("SELECT status FROM subscriptions WHERE tenant_id = @id", new { id = DatabaseSeeder.Tenant1.Id.Value });
        status.Should().Be(nameof(SubscriptionStatus.Active));
        var plan = Connection.ExecuteScalar<string>("SELECT plan FROM subscriptions WHERE tenant_id = @id", new { id = DatabaseSeeder.Tenant1.Id.Value });
        plan.Should().Be(nameof(SubscriptionPlan.Starter));
        var token = Connection.ExecuteScalar<string?>("SELECT pay_fast_token FROM subscriptions WHERE tenant_id = @id", new { id = DatabaseSeeder.Tenant1.Id.Value });
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
        var status = Connection.ExecuteScalar<string>("SELECT status FROM subscriptions WHERE tenant_id = @id", new { id = DatabaseSeeder.Tenant1.Id.Value });
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
        var status = Connection.ExecuteScalar<string>("SELECT status FROM subscriptions WHERE tenant_id = @id", new { id = DatabaseSeeder.Tenant1.Id.Value });
        status.Should().Be(nameof(SubscriptionStatus.PastDue));
        TelemetryEventsCollectorSpy.CollectedEvents.Count.Should().Be(1);
        TelemetryEventsCollectorSpy.CollectedEvents[0].GetType().Name.Should().Be("PaymentFailed");
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

    private static FormUrlEncodedContent BuildItnPayload(string status, SubscriptionPlan plan, string? token, long? tenantId = null)
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
        fields["signature"] = PayFastSignature.Generate(fields, TestPassphrase);
        return new FormUrlEncodedContent(fields);
    }
}
