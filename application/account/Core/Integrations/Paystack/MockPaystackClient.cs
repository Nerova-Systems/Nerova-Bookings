using Account.Features.Subscriptions.Domain;
using Microsoft.Extensions.Configuration;

namespace Account.Integrations.Paystack;

public sealed class MockPaystackState
{
    public string? OverrideSubscriptionStatus { get; set; }

    public bool SimulateSubscriptionDeleted { get; set; }

    public bool SimulateCustomerDeleted { get; set; }

    public bool SimulateOpenInvoice { get; set; }
}

public sealed class MockPaystackClient(IConfiguration configuration, TimeProvider timeProvider, MockPaystackState state) : IPaystackClient
{
    public const string MockCustomerId = "CUS_mock_12345";
    public const string MockCustomerCode = MockCustomerId;
    public const string MockSubscriptionId = "AUTH_mock_12345";
    public const string MockAuthorizationCode = MockSubscriptionId;
    public const string MockReference = "nerova_mock_reference_12345";
    public const string MockAccessCode = "access_mock_12345";
    public const string MockInvoiceUrl = "https://mock.paystack.local/receipt/12345";
    public const string MockWebhookEventId = "evt_mock_12345";

    private readonly bool _isEnabled = configuration.GetValue<bool>("Paystack:AllowMockProvider");

    public Task<PaystackCustomerId?> CreateCustomerAsync(string tenantName, string email, long tenantId, CancellationToken cancellationToken)
    {
        EnsureEnabled();
        return Task.FromResult<PaystackCustomerId?>(PaystackCustomerId.NewId(MockCustomerId));
    }

    public Task<CheckoutSessionResult?> CreateCheckoutSessionAsync(PaystackCustomerId paystackCustomerId, string email, SubscriptionPlan plan, PaystackPaymentPurpose purpose, CancellationToken cancellationToken)
    {
        EnsureEnabled();
        var amount = plan == SubscriptionPlan.Premium ? 99.00m : 29.00m;
        return Task.FromResult<CheckoutSessionResult?>(new CheckoutSessionResult($"{MockReference}_{Guid.NewGuid():N}", MockAccessCode, amount, "USD", purpose));
    }

    public Task<SubscriptionSyncResult?> SyncSubscriptionStateAsync(PaystackCustomerId paystackCustomerId, CancellationToken cancellationToken)
    {
        EnsureEnabled();

        if (state.SimulateSubscriptionDeleted)
        {
            return Task.FromResult<SubscriptionSyncResult?>(null);
        }

        var now = timeProvider.GetUtcNow();
        var transactions = new[]
        {
            new PaymentTransaction(
                PaymentTransactionId.NewId(),
                29.99m,
                "USD",
                PaymentTransactionStatus.Succeeded,
                now,
                null,
                null,
                null
            )
        };

        var result = new SubscriptionSyncResult(
            SubscriptionPlan.Standard,
            null,
            PaystackSubscriptionId.NewId(MockAuthorizationCode),
            29.99m,
            "USD",
            now.AddDays(30),
            false,
            null,
            null,
            transactions,
            new PaymentMethod("visa", "4242", 12, 2026),
            state.OverrideSubscriptionStatus ?? PaystackSubscriptionStatus.Active
        );

        return Task.FromResult<SubscriptionSyncResult?>(result);
    }

    public Task<PaystackSubscriptionId?> GetCheckoutSessionSubscriptionIdAsync(string sessionId, CancellationToken cancellationToken)
    {
        EnsureEnabled();
        return Task.FromResult<PaystackSubscriptionId?>(PaystackSubscriptionId.NewId(MockAuthorizationCode));
    }

    public Task<UpgradeSubscriptionResult?> UpgradeSubscriptionAsync(PaystackCustomerId paystackCustomerId, PaystackSubscriptionId authorizationCode, string email, SubscriptionPlan newPlan, CancellationToken cancellationToken)
    {
        EnsureEnabled();
        var amount = newPlan == SubscriptionPlan.Premium ? 70.00m : 0m;
        return Task.FromResult<UpgradeSubscriptionResult?>(new UpgradeSubscriptionResult(
                Reference: $"{MockReference}_upgrade_{Guid.NewGuid():N}",
                Amount: amount,
                Currency: "USD"
            )
        );
    }

    public Task<bool> ScheduleDowngradeAsync(PaystackSubscriptionId paystackSubscriptionId, SubscriptionPlan newPlan, CancellationToken cancellationToken)
    {
        EnsureEnabled();
        return Task.FromResult(true);
    }

    public Task<bool> CancelScheduledDowngradeAsync(PaystackSubscriptionId paystackSubscriptionId, CancellationToken cancellationToken)
    {
        EnsureEnabled();
        return Task.FromResult(true);
    }

    public Task<bool> CancelSubscriptionAtPeriodEndAsync(PaystackSubscriptionId paystackSubscriptionId, CancellationReason reason, string? feedback, CancellationToken cancellationToken)
    {
        EnsureEnabled();
        return Task.FromResult(true);
    }

    public Task<bool> ReactivateSubscriptionAsync(PaystackSubscriptionId paystackSubscriptionId, CancellationToken cancellationToken)
    {
        EnsureEnabled();
        return Task.FromResult(true);
    }

    public Task<PriceCatalogItem[]> GetPriceCatalogAsync(CancellationToken cancellationToken)
    {
        EnsureEnabled();
        return Task.FromResult<PriceCatalogItem[]>([
                new PriceCatalogItem(SubscriptionPlan.Standard, 29.00m, "USD", "month", 1, false),
                new PriceCatalogItem(SubscriptionPlan.Premium, 99.00m, "USD", "month", 1, false)
            ]
        );
    }

    public PaystackWebhookEventResult? VerifyWebhookSignature(string payload, string signatureHeader)
    {
        EnsureEnabled();

        if (signatureHeader == "invalid_signature")
        {
            return null;
        }

        var eventType = "charge.success";
        var eventId = $"{MockWebhookEventId}_{Guid.NewGuid():N}";
        foreach (var part in signatureHeader.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            if (part.StartsWith("event_type:", StringComparison.Ordinal)) eventType = part["event_type:".Length..];
            if (part.StartsWith("event_id:", StringComparison.Ordinal)) eventId = part["event_id:".Length..];
        }

        var customerCode = payload.StartsWith("customer:", StringComparison.Ordinal) ? payload.Split(':')[1] : payload == "no_customer" ? null : MockCustomerId;
        PaystackCustomerId.TryParse(customerCode, out var customerId);

        return new PaystackWebhookEventResult(eventId, eventType, customerId, MockReference);
    }

    public Task<VerifiedPaystackTransactionResult?> VerifyTransactionAsync(string reference, PaystackPaymentPurpose purpose, CancellationToken cancellationToken)
    {
        EnsureEnabled();
        var amount = purpose == PaystackPaymentPurpose.Upgrade ? 70.00m : 29.00m;
        return Task.FromResult<VerifiedPaystackTransactionResult?>(new VerifiedPaystackTransactionResult(
                reference,
                amount,
                "USD",
                true,
                purpose,
                PaystackCustomerId.NewId(MockCustomerId),
                new PaystackAuthorization(PaystackSubscriptionId.NewId(MockAuthorizationCode), "billing@example.com", "SIG_mock_12345"),
                new PaymentMethod("visa", "4242", 12, 2026)
            )
        );
    }

    public Task<CustomerBillingResult?> GetCustomerBillingInfoAsync(PaystackCustomerId paystackCustomerId, CancellationToken cancellationToken)
    {
        EnsureEnabled();

        if (state.SimulateCustomerDeleted)
        {
            return Task.FromResult<CustomerBillingResult?>(new CustomerBillingResult(null, true));
        }

        var billingInfo = new BillingInfo("Test Organization", new BillingAddress("Vestergade 12", null, "1456", "København K", null, "DK"), "billing@example.com", null);
        var paymentMethod = new PaymentMethod("visa", "4242", 12, 2026);
        return Task.FromResult<CustomerBillingResult?>(new CustomerBillingResult(billingInfo, false, paymentMethod));
    }

    public Task<bool> UpdateCustomerBillingInfoAsync(PaystackCustomerId paystackCustomerId, BillingInfo billingInfo, string? locale, CancellationToken cancellationToken)
    {
        EnsureEnabled();
        return Task.FromResult(true);
    }

    public Task<bool> SyncCustomerTaxIdAsync(PaystackCustomerId paystackCustomerId, string? taxId, CancellationToken cancellationToken)
    {
        EnsureEnabled();
        return Task.FromResult(taxId != "INVALID");
    }

    public Task<CheckoutSessionResult?> CreateSetupIntentAsync(PaystackCustomerId paystackCustomerId, string email, CancellationToken cancellationToken)
    {
        EnsureEnabled();
        return Task.FromResult<CheckoutSessionResult?>(new CheckoutSessionResult($"{MockReference}_auth_{Guid.NewGuid():N}", MockAccessCode, 1.00m, "USD", PaystackPaymentPurpose.PaymentMethodAuthorization));
    }

    public Task<VerifiedPaystackTransactionResult?> GetSetupIntentPaymentMethodAsync(string setupIntentId, CancellationToken cancellationToken)
    {
        EnsureEnabled();
        return VerifyTransactionAsync(setupIntentId, PaystackPaymentPurpose.PaymentMethodAuthorization, cancellationToken);
    }

    public Task<bool> SetSubscriptionDefaultPaymentMethodAsync(PaystackSubscriptionId paystackSubscriptionId, string paymentMethodId, CancellationToken cancellationToken)
    {
        EnsureEnabled();
        return Task.FromResult(true);
    }

    public Task<bool> SetCustomerDefaultPaymentMethodAsync(PaystackCustomerId paystackCustomerId, string paymentMethodId, CancellationToken cancellationToken)
    {
        EnsureEnabled();
        return Task.FromResult(true);
    }

    public Task<OpenInvoiceResult?> GetOpenInvoiceAsync(PaystackSubscriptionId paystackSubscriptionId, CancellationToken cancellationToken)
    {
        EnsureEnabled();
        return Task.FromResult(state.SimulateOpenInvoice ? new OpenInvoiceResult(29.99m, "USD") : null);
    }

    public Task<InvoiceRetryResult?> RetryOpenInvoicePaymentAsync(PaystackSubscriptionId paystackSubscriptionId, string? paymentMethodId, CancellationToken cancellationToken)
    {
        EnsureEnabled();
        var result = state.SimulateOpenInvoice
            ? new InvoiceRetryResult(
                true,
                Reference: $"{MockReference}_retry_{Guid.NewGuid():N}",
                Amount: 29.99m,
                Currency: "USD"
            )
            : null;

        return Task.FromResult(result);
    }

    public Task<UpgradePreviewResult?> GetUpgradePreviewAsync(PaystackSubscriptionId paystackSubscriptionId, SubscriptionPlan newPlan, CancellationToken cancellationToken)
    {
        EnsureEnabled();
        var now = timeProvider.GetUtcNow();
        var lineItems = new[]
        {
            new UpgradePreviewLineItem("Unused time on Standard after " + now.ToString("d MMM yyyy"), -14.50m, "USD", true, false),
            new UpgradePreviewLineItem("Remaining time on Premium after " + now.ToString("d MMM yyyy"), 30.00m, "USD", true, false),
            new UpgradePreviewLineItem("Tax", 0m, "USD", false, true)
        };
        return Task.FromResult<UpgradePreviewResult?>(new UpgradePreviewResult(15.50m, "USD", lineItems));
    }

    public Task<CheckoutPreviewResult?> GetCheckoutPreviewAsync(PaystackCustomerId paystackCustomerId, SubscriptionPlan plan, CancellationToken cancellationToken)
    {
        EnsureEnabled();
        return Task.FromResult<CheckoutPreviewResult?>(new CheckoutPreviewResult(plan == SubscriptionPlan.Premium ? 99.00m : 29.00m, "USD", 0m));
    }

    public Task<SubscribeResult?> CreateSubscriptionWithSavedPaymentMethodAsync(PaystackCustomerId paystackCustomerId, PaystackSubscriptionId authorizationCode, string email, SubscriptionPlan plan, CancellationToken cancellationToken)
    {
        EnsureEnabled();
        var amount = plan == SubscriptionPlan.Premium ? 99.00m : 29.00m;
        return Task.FromResult<SubscribeResult?>(new SubscribeResult($"{MockReference}_saved_{Guid.NewGuid():N}", amount, "USD", true, new PaymentMethod("visa", "4242", 12, 2026)));
    }

    public Task<PaymentTransaction[]?> SyncPaymentTransactionsAsync(PaystackCustomerId paystackCustomerId, CancellationToken cancellationToken)
    {
        EnsureEnabled();
        var now = timeProvider.GetUtcNow();
        return Task.FromResult<PaymentTransaction[]?>([
                new PaymentTransaction(PaymentTransactionId.NewId(), 29.99m, "USD", PaymentTransactionStatus.Succeeded, now, null, null, null)
            ]
        );
    }

    private void EnsureEnabled()
    {
        if (!_isEnabled)
        {
            throw new InvalidOperationException("Mock Paystack provider is not enabled.");
        }
    }
}
