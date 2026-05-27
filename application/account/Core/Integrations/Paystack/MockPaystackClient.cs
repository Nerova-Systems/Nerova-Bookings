using System.Collections.Concurrent;
using Account.Features.Subscriptions.Domain;
using Microsoft.Extensions.Configuration;

namespace Account.Integrations.Paystack;

public sealed class MockPaystackState
{
    public ConcurrentDictionary<string, VerifiedPaystackTransactionResult> VerifiedTransactions { get; } = new();

    public bool SimulateCustomerDeleted { get; set; }

    public bool SimulateAuthorizationChargeFailure { get; set; }

    public bool SimulateRefundFailure { get; set; }
}

public sealed class MockPaystackClient(IConfiguration configuration, TimeProvider timeProvider, MockPaystackState state) : IPaystackClient
{
    public const string MockCustomerId = "CUS_mock_12345";
    public const string MockCustomerCode = MockCustomerId;
    public const string MockSubscriptionId = "AUTH_mock_12345";
    public const string MockAuthorizationCode = MockSubscriptionId;
    public const string MockReference = "nerova_mock_reference_12345";
    public const string MockAccessCode = "access_mock_12345";
    public const string MockReceiptUrl = "https://mock.paystack.local/receipt/12345";
    public const string MockWebhookEventId = "evt_mock_12345";
    public const string MockStandardCurrency = "ZAR";
    public const decimal StandardAmountExcludingTax = 29.00m;
    public const decimal PremiumAmountExcludingTax = 99.00m;

    private readonly bool _isEnabled = configuration.GetValue<bool>("Paystack:AllowMockProvider");

    public Task<PaystackCustomerId?> CreateCustomerAsync(string tenantName, string email, long tenantId, CancellationToken cancellationToken)
    {
        EnsureEnabled();
        return Task.FromResult<PaystackCustomerId?>(PaystackCustomerId.NewId(GetMockCustomerCode(tenantId)));
    }

    public Task<CheckoutSessionResult?> CreateCheckoutSessionAsync(PaystackCustomerId paystackCustomerId, string email, SubscriptionPlan plan, PaystackPaymentPurpose purpose, CancellationToken cancellationToken)
    {
        EnsureEnabled();
        var amount = plan == SubscriptionPlan.Premium ? 99.00m : 29.00m;
        var reference = $"{MockReference}_{Guid.NewGuid():N}";
        state.VerifiedTransactions[reference] = CreateVerifiedTransaction(reference, amount, MockStandardCurrency, purpose, paystackCustomerId);
        return Task.FromResult<CheckoutSessionResult?>(new CheckoutSessionResult(reference, MockAccessCode, amount, MockStandardCurrency, purpose));
    }

    public Task<AuthorizationChargeResult?> ChargeAuthorizationAsync(
        PaystackCustomerId paystackCustomerId,
        PaystackAuthorizationCode authorizationCode,
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

        state.VerifiedTransactions[reference] = CreateVerifiedTransaction(reference, amount, normalizedCurrency, purpose, paystackCustomerId);
        return Task.FromResult<AuthorizationChargeResult?>(new AuthorizationChargeResult(true, reference, amount, normalizedCurrency, new PaymentMethod("visa", "4242", 12, 2026)));
    }

    public Task<PriceCatalogItem[]> GetPriceCatalogAsync(CancellationToken cancellationToken)
    {
        EnsureEnabled();
        return Task.FromResult<PriceCatalogItem[]>([
                new PriceCatalogItem(SubscriptionPlan.Standard, 29.00m, MockStandardCurrency, "month", 1, false),
                new PriceCatalogItem(SubscriptionPlan.Premium, 99.00m, MockStandardCurrency, "month", 1, false)
            ]
        );
    }

    public Task<IReadOnlyList<PaystackBankDto>> GetBanksAsync(string country, CancellationToken cancellationToken)
    {
        EnsureEnabled();
        IReadOnlyList<PaystackBankDto> banks =
        [
            new PaystackBankDto("044", "Access Bank"),
            new PaystackBankDto("063", "Access Bank (Diamond)"),
            new PaystackBankDto("023", "Citibank Nigeria"),
            new PaystackBankDto("050", "EcoBank Nigeria"),
            new PaystackBankDto("011", "First Bank of Nigeria"),
            new PaystackBankDto("214", "First City Monument Bank"),
            new PaystackBankDto("070", "Fidelity Bank"),
            new PaystackBankDto("058", "Guaranty Trust Bank"),
            new PaystackBankDto("030", "Heritage Bank"),
            new PaystackBankDto("082", "Keystone Bank"),
            new PaystackBankDto("014", "MainStreet Bank"),
            new PaystackBankDto("076", "Polaris Bank"),
            new PaystackBankDto("039", "Stanbic IBTC Bank"),
            new PaystackBankDto("232", "Sterling Bank"),
            new PaystackBankDto("032", "Union Bank of Nigeria"),
            new PaystackBankDto("033", "United Bank for Africa"),
            new PaystackBankDto("215", "Unity Bank"),
            new PaystackBankDto("035", "Wema Bank"),
            new PaystackBankDto("057", "Zenith Bank")
        ];
        return Task.FromResult(banks);
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
        return Task.FromResult<VerifiedPaystackTransactionResult?>(CreateVerifiedTransaction(reference, amount, MockStandardCurrency, purpose));
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
        state.VerifiedTransactions[reference] = CreateVerifiedTransaction(reference, 1.00m, MockStandardCurrency, PaystackPaymentPurpose.PaymentMethodAuthorization, paystackCustomerId);
        return Task.FromResult<CheckoutSessionResult?>(new CheckoutSessionResult(reference, MockAccessCode, 1.00m, MockStandardCurrency, PaystackPaymentPurpose.PaymentMethodAuthorization));
    }

    public Task<VerifiedPaystackTransactionResult?> VerifyPaymentMethodAuthorizationAsync(string reference, CancellationToken cancellationToken)
    {
        EnsureEnabled();
        return VerifyTransactionAsync(reference, PaystackPaymentPurpose.PaymentMethodAuthorization, cancellationToken);
    }

    public Task<RefundResult?> CreateRefundAsync(string transactionReference, decimal amount, string currency, CancellationToken cancellationToken)
    {
        EnsureEnabled();
        if (state.SimulateRefundFailure)
        {
            return Task.FromResult<RefundResult?>(null);
        }

        return Task.FromResult<RefundResult?>(new RefundResult($"refund_{Guid.NewGuid():N}", amount, currency.ToUpperInvariant(), "processed"));
    }

    public Task<CheckoutPreviewResult?> GetCheckoutPreviewAsync(PaystackCustomerId paystackCustomerId, SubscriptionPlan plan, CancellationToken cancellationToken)
    {
        EnsureEnabled();
        return Task.FromResult<CheckoutPreviewResult?>(new CheckoutPreviewResult(plan == SubscriptionPlan.Premium ? 99.00m : 29.00m, MockStandardCurrency, 0m));
    }

    public Task<PaymentTransaction[]?> SyncPaymentTransactionsAsync(PaystackCustomerId paystackCustomerId, CancellationToken cancellationToken)
    {
        EnsureEnabled();
        var now = timeProvider.GetUtcNow();
        var transactions = state.VerifiedTransactions.Values
            .Where(t => t is { Paid: true, CustomerId: not null } && t.CustomerId == paystackCustomerId)
            .Select(t => new PaymentTransaction(PaymentTransactionId.NewId(), t.Amount, t.Amount, 0m, t.Currency, PaymentTransactionStatus.Succeeded, now, null, null, null, PaystackReference: t.Reference))
            .ToArray();
        return Task.FromResult<PaymentTransaction[]?>(transactions);
    }

    public static string GetMockCustomerCode(long tenantId)
    {
        return $"CUS_mock_{tenantId}";
    }

    private void EnsureEnabled()
    {
        if (!_isEnabled)
        {
            throw new InvalidOperationException("Mock Paystack provider is not enabled.");
        }
    }

    private static VerifiedPaystackTransactionResult CreateVerifiedTransaction(string reference, decimal amount, string currency, PaystackPaymentPurpose purpose, PaystackCustomerId? customerId = null)
    {
        return new VerifiedPaystackTransactionResult(
            reference,
            amount,
            currency,
            true,
            purpose,
            customerId ?? PaystackCustomerId.NewId(MockCustomerId),
            new PaystackAuthorization(PaystackAuthorizationCode.NewId(MockAuthorizationCode), "billing@example.com", "SIG_mock_12345"),
            new PaymentMethod("visa", "4242", 12, 2026)
        );
    }
}
