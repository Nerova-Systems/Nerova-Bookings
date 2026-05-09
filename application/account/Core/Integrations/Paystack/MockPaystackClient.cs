using System.Collections.Concurrent;
using Account.Features.Subscriptions.Domain;
using Microsoft.Extensions.Configuration;

namespace Account.Integrations.Paystack;

public sealed class MockPaystackState
{
    public ConcurrentDictionary<string, VerifiedPaystackTransactionResult> VerifiedTransactions { get; } = new();

    public string? OverrideSubscriptionStatus { get; set; }

    public bool SimulateSubscriptionDeleted { get; set; }

    public bool SimulateCustomerDeleted { get; set; }

    public bool SimulateOpenInvoice { get; set; }

    public bool SimulateRetryAuthenticationRequired { get; set; }

    public bool SimulateAuthorizationChargeFailure { get; set; }
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
        var reference = $"{MockReference}_{Guid.NewGuid():N}";
        state.VerifiedTransactions[reference] = CreateVerifiedTransaction(reference, amount, "USD", purpose);
        return Task.FromResult<CheckoutSessionResult?>(new CheckoutSessionResult(reference, MockAccessCode, amount, "USD", purpose));
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

    public Task<UpgradeSubscriptionResult?> UpgradeSubscriptionAsync(PaystackCustomerId paystackCustomerId, PaystackSubscriptionId authorizationCode, string email, SubscriptionPlan newPlan, CancellationToken cancellationToken)
    {
        EnsureEnabled();
        var amount = newPlan == SubscriptionPlan.Premium ? 15.50m : 0m;
        var reference = $"{MockReference}_upgrade_{Guid.NewGuid():N}";
        if (!state.SimulateAuthorizationChargeFailure)
        {
            state.VerifiedTransactions[reference] = CreateVerifiedTransaction(reference, amount, "USD", PaystackPaymentPurpose.Upgrade);
        }

        return Task.FromResult<UpgradeSubscriptionResult?>(new UpgradeSubscriptionResult(
                ErrorMessage: state.SimulateAuthorizationChargeFailure ? "Paystack could not charge the saved payment method." : null,
                Reference: reference,
                Amount: amount,
                Currency: "USD"
            )
        );
    }

    public Task<AuthorizationChargeResult?> ChargeAuthorizationAsync(
        PaystackCustomerId paystackCustomerId,
        PaystackSubscriptionId authorizationCode,
        string email,
        PaystackPaymentPurpose purpose,
        SubscriptionPlan plan,
        decimal amount,
        string currency,
        CancellationToken cancellationToken
    )
    {
        EnsureEnabled();
        var reference = $"{MockReference}_{purpose.ToString().ToLowerInvariant()}_{Guid.NewGuid():N}";
        var normalizedCurrency = currency.ToUpperInvariant();
        if (state.SimulateAuthorizationChargeFailure)
        {
            return Task.FromResult<AuthorizationChargeResult?>(new AuthorizationChargeResult(false, reference, amount, normalizedCurrency, ErrorMessage: "Paystack could not charge the saved payment method."));
        }

        state.VerifiedTransactions[reference] = CreateVerifiedTransaction(reference, amount, normalizedCurrency, purpose);
        return Task.FromResult<AuthorizationChargeResult?>(new AuthorizationChargeResult(true, reference, amount, normalizedCurrency, new PaymentMethod("visa", "4242", 12, 2026)));
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
        if (state.VerifiedTransactions.TryGetValue(reference, out var verifiedTransaction))
        {
            return Task.FromResult<VerifiedPaystackTransactionResult?>(verifiedTransaction);
        }

        var amount = purpose switch
        {
            PaystackPaymentPurpose.Upgrade => 15.50m,
            PaystackPaymentPurpose.PaymentMethodAuthorization => 1.00m,
            _ => 29.00m
        };
        return Task.FromResult<VerifiedPaystackTransactionResult?>(CreateVerifiedTransaction(reference, amount, "USD", purpose));
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

    public Task<CheckoutSessionResult?> CreatePaymentMethodAuthorizationAsync(PaystackCustomerId paystackCustomerId, string email, CancellationToken cancellationToken)
    {
        EnsureEnabled();
        var reference = $"{MockReference}_auth_{Guid.NewGuid():N}";
        state.VerifiedTransactions[reference] = CreateVerifiedTransaction(reference, 1.00m, "USD", PaystackPaymentPurpose.PaymentMethodAuthorization);
        return Task.FromResult<CheckoutSessionResult?>(new CheckoutSessionResult(reference, MockAccessCode, 1.00m, "USD", PaystackPaymentPurpose.PaymentMethodAuthorization));
    }

    public Task<VerifiedPaystackTransactionResult?> VerifyPaymentMethodAuthorizationAsync(string reference, CancellationToken cancellationToken)
    {
        EnsureEnabled();
        return VerifyTransactionAsync(reference, PaystackPaymentPurpose.PaymentMethodAuthorization, cancellationToken);
    }

    public Task<RefundResult?> CreateRefundAsync(string transactionReference, decimal amount, string currency, CancellationToken cancellationToken)
    {
        EnsureEnabled();
        return Task.FromResult<RefundResult?>(new RefundResult($"refund_{Guid.NewGuid():N}", amount, currency.ToUpperInvariant(), "processed"));
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
        if (!state.SimulateOpenInvoice)
        {
            return Task.FromResult<InvoiceRetryResult?>(null);
        }

        var reference = $"{MockReference}_retry_{Guid.NewGuid():N}";
        state.VerifiedTransactions[reference] = CreateVerifiedTransaction(reference, 29.99m, "USD", PaystackPaymentPurpose.Retry);
        var result = state.SimulateRetryAuthenticationRequired
            ? new InvoiceRetryResult(false, AccessCode: MockAccessCode, Reference: reference, Amount: 29.99m, Currency: "USD")
            : new InvoiceRetryResult(true, Reference: reference, Amount: 29.99m, Currency: "USD");

        return Task.FromResult<InvoiceRetryResult?>(result);
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
        var reference = $"{MockReference}_saved_{Guid.NewGuid():N}";
        state.VerifiedTransactions[reference] = CreateVerifiedTransaction(reference, amount, "USD", PaystackPaymentPurpose.Subscribe);
        return Task.FromResult<SubscribeResult?>(new SubscribeResult(reference, amount, "USD", true, new PaymentMethod("visa", "4242", 12, 2026)));
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

    private static VerifiedPaystackTransactionResult CreateVerifiedTransaction(string reference, decimal amount, string currency, PaystackPaymentPurpose purpose)
    {
        return new VerifiedPaystackTransactionResult(
            reference,
            amount,
            currency,
            true,
            purpose,
            PaystackCustomerId.NewId(MockCustomerId),
            new PaystackAuthorization(PaystackSubscriptionId.NewId(MockAuthorizationCode), "billing@example.com", "SIG_mock_12345"),
            new PaymentMethod("visa", "4242", 12, 2026)
        );
    }
}
