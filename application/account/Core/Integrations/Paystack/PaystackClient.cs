using System.Globalization;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Account.Features.Subscriptions.Domain;
using Microsoft.Extensions.Configuration;

namespace Account.Integrations.Paystack;

public sealed class PaystackClient(IConfiguration configuration, IHttpClientFactory httpClientFactory, ILogger<PaystackClient> logger) : IPaystackClient
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly long _cardAuthorizationAmountSubunit = configuration.GetValue<long?>("Paystack:CardAuthorizationAmountSubunit") ?? 100;
    private readonly string? _premiumPlanCode = configuration["Paystack:PremiumPlanCode"];
    private readonly string? _secretKey = configuration["Paystack:SecretKey"];
    private readonly string? _standardPlanCode = configuration["Paystack:StandardPlanCode"];

    public async Task<PaystackCustomerId?> CreateCustomerAsync(string tenantName, string email, long tenantId, CancellationToken cancellationToken)
    {
        var payload = new
        {
            email,
            first_name = tenantName,
            metadata = new { tenant_id = tenantId.ToString(CultureInfo.InvariantCulture), tenant_name = tenantName }
        };

        var response = await SendAsync(HttpMethod.Post, "/customer", payload, cancellationToken);
        if (response is null) return null;

        var customerCode = GetString(response.RootElement, "data", "customer_code");
        if (customerCode is null)
        {
            logger.LogWarning("Paystack customer creation response did not include a customer_code");
            return null;
        }

        return PaystackCustomerId.NewId(customerCode);
    }

    public async Task<CheckoutSessionResult?> CreateCheckoutSessionAsync(PaystackCustomerId paystackCustomerId, string email, SubscriptionPlan plan, PaystackPaymentPurpose purpose, CancellationToken cancellationToken)
    {
        var catalogItem = await GetCatalogItemAsync(plan, cancellationToken);
        if (catalogItem is null) return null;

        var reference = CreateReference(purpose);
        var payload = new
        {
            email,
            amount = ToSubunit(catalogItem.UnitAmount),
            currency = catalogItem.Currency.ToUpperInvariant(),
            reference,
            channels = new[] { "card" },
            metadata = new
            {
                purpose = purpose.ToString(),
                plan = plan.ToString(),
                paystack_customer_code = paystackCustomerId.Value,
                reusable_authorization_required = true
            }
        };

        var response = await SendAsync(HttpMethod.Post, "/transaction/initialize", payload, cancellationToken);
        if (response is null) return null;

        var accessCode = GetString(response.RootElement, "data", "access_code");
        var responseReference = GetString(response.RootElement, "data", "reference") ?? reference;
        if (accessCode is null)
        {
            logger.LogWarning("Paystack transaction initialization response for reference '{Reference}' did not include an access_code", reference);
            return null;
        }

        return new CheckoutSessionResult(responseReference, accessCode, catalogItem.UnitAmount, catalogItem.Currency.ToUpperInvariant(), purpose);
    }

    public async Task<AuthorizationChargeResult?> ChargeAuthorizationAsync(
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
        var normalizedCurrency = currency.ToUpperInvariant();
        var reference = CreateReference(purpose);
        var payload = new
        {
            authorization_code = authorizationCode.Value,
            email,
            amount = ToSubunit(amount),
            currency = normalizedCurrency,
            reference,
            metadata = new
            {
                purpose = purpose.ToString(),
                plan = plan.ToString(),
                paystack_customer_code = paystackCustomerId.Value
            }
        };

        var response = await SendAsync(HttpMethod.Post, "/transaction/charge_authorization", payload, cancellationToken);
        if (response is null) return null;

        var paid = string.Equals(GetString(response.RootElement, "data", "status"), "success", StringComparison.OrdinalIgnoreCase);
        var paymentMethod = CreatePaymentMethod(response.RootElement);
        if (!paid)
        {
            var gatewayResponse = GetString(response.RootElement, "data", "gateway_response");
            return new AuthorizationChargeResult(false, reference, amount, normalizedCurrency, paymentMethod, gatewayResponse ?? "Paystack could not charge the saved payment method.");
        }

        return new AuthorizationChargeResult(true, reference, amount, normalizedCurrency, paymentMethod);
    }

    public async Task<PriceCatalogItem[]> GetPriceCatalogAsync(CancellationToken cancellationToken)
    {
        var standard = await GetCatalogItemAsync(SubscriptionPlan.Standard, cancellationToken);
        var premium = await GetCatalogItemAsync(SubscriptionPlan.Premium, cancellationToken);
        return new[] { standard, premium }.OfType<PriceCatalogItem>().ToArray();
    }

    public PaystackWebhookEventResult? VerifyWebhookSignature(string payload, string signatureHeader)
    {
        if (string.IsNullOrWhiteSpace(_secretKey))
        {
            logger.LogWarning("Paystack secret key is not configured. Cannot verify webhook signature");
            return null;
        }

        var expectedSignature = Convert.ToHexString(HMACSHA512.HashData(Encoding.UTF8.GetBytes(_secretKey), Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
        if (!CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expectedSignature), Encoding.UTF8.GetBytes(signatureHeader.ToLowerInvariant())))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(payload);
            var root = document.RootElement;
            var eventType = GetString(root, "event") ?? "unknown";
            var reference = GetString(root, "data", "reference");
            var eventId = GetString(root, "data", "id") ?? reference ?? $"{eventType}_{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant()}";
            var customerCode = GetString(root, "data", "customer", "customer_code") ?? GetString(root, "data", "customer_code");
            PaystackCustomerId.TryParse(customerCode, out var customerId);
            return new PaystackWebhookEventResult(eventId, eventType, customerId, reference);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Invalid Paystack webhook payload");
            return null;
        }
    }

    public async Task<VerifiedPaystackTransactionResult?> VerifyTransactionAsync(string reference, PaystackPaymentPurpose purpose, CancellationToken cancellationToken)
    {
        var response = await SendAsync(HttpMethod.Get, $"/transaction/verify/{Uri.EscapeDataString(reference)}", null, cancellationToken);
        if (response is null) return null;

        var root = response.RootElement;
        var apiStatus = GetBoolean(root, "status") == true;
        var transactionStatus = GetString(root, "data", "status");
        var paid = apiStatus && string.Equals(transactionStatus, "success", StringComparison.OrdinalIgnoreCase);
        var channel = GetString(root, "data", "channel");
        var reusable = GetBoolean(root, "data", "authorization", "reusable") == true;
        var authorizationCode = GetString(root, "data", "authorization", "authorization_code");
        var email = GetString(root, "data", "customer", "email") ?? GetString(root, "data", "authorization", "email");
        var signature = GetString(root, "data", "authorization", "signature");
        var customerCode = GetString(root, "data", "customer", "customer_code");
        var verifiedReference = GetString(root, "data", "reference") ?? reference;
        var verifiedPurpose = GetPaymentPurpose(root) ?? purpose;
        var amountSubunit = GetLong(root, "data", "amount") ?? 0;
        var currency = GetString(root, "data", "currency")?.ToUpperInvariant() ?? "USD";

        if (paid && !string.Equals(channel, "card", StringComparison.OrdinalIgnoreCase))
        {
            return new VerifiedPaystackTransactionResult(verifiedReference, FromSubunit(amountSubunit), currency, false, verifiedPurpose, null, null, null, "Only card payments are accepted.");
        }

        if (paid && !reusable)
        {
            return new VerifiedPaystackTransactionResult(verifiedReference, FromSubunit(amountSubunit), currency, false, verifiedPurpose, null, null, null, "The card authorization is not reusable.");
        }

        PaystackCustomerId.TryParse(customerCode, out var customerId);
        var authorization = authorizationCode is not null && email is not null && signature is not null
            ? new PaystackAuthorization(PaystackAuthorizationCode.NewId(authorizationCode), email, signature)
            : null;
        var paymentMethod = CreatePaymentMethod(root);

        return new VerifiedPaystackTransactionResult(verifiedReference, FromSubunit(amountSubunit), currency, paid, verifiedPurpose, customerId, authorization, paymentMethod);
    }

    public async Task<CustomerBillingResult?> GetCustomerBillingInfoAsync(PaystackCustomerId paystackCustomerId, CancellationToken cancellationToken)
    {
        var response = await SendAsync(HttpMethod.Get, $"/customer/{Uri.EscapeDataString(paystackCustomerId.Value)}", null, cancellationToken);
        if (response is null) return null;

        var data = response.RootElement.GetProperty("data");
        var email = GetString(data, "email");
        var name = GetString(data, "first_name") ?? GetString(data, "last_name");
        return new CustomerBillingResult(new BillingInfo(name, null, email, null), false);
    }

    public async Task<bool> UpdateCustomerBillingInfoAsync(PaystackCustomerId paystackCustomerId, BillingInfo billingInfo, string? locale, CancellationToken cancellationToken)
    {
        var payload = new
        {
            first_name = billingInfo.Name,
            email = billingInfo.Email,
            metadata = new
            {
                address = billingInfo.Address,
                locale
            }
        };
        return await SendAsync(HttpMethod.Put, $"/customer/{Uri.EscapeDataString(paystackCustomerId.Value)}", payload, cancellationToken) is not null;
    }

    public Task<bool> SyncCustomerTaxIdAsync(PaystackCustomerId paystackCustomerId, string? taxId, CancellationToken cancellationToken)
    {
        return Task.FromResult(true);
    }

    public async Task<CheckoutSessionResult?> CreatePaymentMethodAuthorizationAsync(PaystackCustomerId paystackCustomerId, string email, CancellationToken cancellationToken)
    {
        var reference = CreateReference(PaystackPaymentPurpose.PaymentMethodAuthorization);
        var payload = new
        {
            email,
            amount = _cardAuthorizationAmountSubunit,
            currency = configuration["Paystack:CardAuthorizationCurrency"] ?? "USD",
            reference,
            channels = new[] { "card" },
            metadata = new
            {
                purpose = nameof(PaystackPaymentPurpose.PaymentMethodAuthorization),
                paystack_customer_code = paystackCustomerId.Value,
                reusable_authorization_required = true
            }
        };

        var response = await SendAsync(HttpMethod.Post, "/transaction/initialize", payload, cancellationToken);
        if (response is null) return null;

        var accessCode = GetString(response.RootElement, "data", "access_code");
        if (accessCode is null) return null;
        return new CheckoutSessionResult(reference, accessCode, FromSubunit(_cardAuthorizationAmountSubunit), payload.currency, PaystackPaymentPurpose.PaymentMethodAuthorization);
    }

    public Task<VerifiedPaystackTransactionResult?> VerifyPaymentMethodAuthorizationAsync(string reference, CancellationToken cancellationToken)
    {
        return VerifyTransactionAsync(reference, PaystackPaymentPurpose.PaymentMethodAuthorization, cancellationToken);
    }

    public async Task<RefundResult?> CreateRefundAsync(string transactionReference, decimal amount, string currency, CancellationToken cancellationToken)
    {
        var normalizedCurrency = currency.ToUpperInvariant();
        var payload = new
        {
            transaction = transactionReference,
            amount = ToSubunit(amount),
            currency = normalizedCurrency,
            merchant_note = "Refund payment method authorization charge"
        };

        var response = await SendAsync(HttpMethod.Post, "/refund", payload, cancellationToken);
        if (response is null) return null;

        var refundId = GetString(response.RootElement, "data", "id");
        var refundedAmount = FromSubunit(GetLong(response.RootElement, "data", "amount") ?? payload.amount);
        var refundedCurrency = GetString(response.RootElement, "data", "currency")?.ToUpperInvariant() ?? normalizedCurrency;
        var status = GetString(response.RootElement, "data", "status") ?? "pending";
        return new RefundResult(refundId, refundedAmount, refundedCurrency, status);
    }

    public async Task<CheckoutPreviewResult?> GetCheckoutPreviewAsync(PaystackCustomerId paystackCustomerId, SubscriptionPlan plan, CancellationToken cancellationToken)
    {
        var catalogItem = await GetCatalogItemAsync(plan, cancellationToken);
        return catalogItem is null ? null : new CheckoutPreviewResult(catalogItem.UnitAmount, catalogItem.Currency.ToUpperInvariant(), 0m);
    }

    public Task<PaymentTransaction[]?> SyncPaymentTransactionsAsync(PaystackCustomerId paystackCustomerId, CancellationToken cancellationToken)
    {
        return Task.FromResult<PaymentTransaction[]?>([]);
    }

    private async Task<PriceCatalogItem?> GetCatalogItemAsync(SubscriptionPlan plan, CancellationToken cancellationToken)
    {
        var planCode = plan switch
        {
            SubscriptionPlan.Standard => _standardPlanCode,
            SubscriptionPlan.Premium => _premiumPlanCode,
            _ => null
        };

        if (string.IsNullOrWhiteSpace(planCode))
        {
            logger.LogWarning("Paystack plan code is not configured for plan '{Plan}'", plan);
            return null;
        }

        var response = await SendAsync(HttpMethod.Get, $"/plan/{Uri.EscapeDataString(planCode)}", null, cancellationToken);
        if (response is null) return null;

        var amount = FromSubunit(GetLong(response.RootElement, "data", "amount") ?? 0);
        var currency = GetString(response.RootElement, "data", "currency") ?? "USD";
        var interval = GetString(response.RootElement, "data", "interval") ?? "month";
        return new PriceCatalogItem(plan, amount, currency.ToUpperInvariant(), interval, 1, false);
    }

    private async Task<JsonDocument?> SendAsync(HttpMethod method, string path, object? payload, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_secretKey))
        {
            logger.LogWarning("Paystack secret key is not configured");
            return null;
        }

        var client = httpClientFactory.CreateClient();
        client.BaseAddress = new Uri("https://api.paystack.co");
        client.Timeout = TimeSpan.FromSeconds(20);

        using var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _secretKey);
        if (payload is not null)
        {
            request.Content = new StringContent(JsonSerializer.Serialize(payload, SerializerOptions), Encoding.UTF8, "application/json");
        }

        try
        {
            using var response = await client.SendAsync(request, cancellationToken);
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Paystack API request to '{Path}' failed with status {StatusCode}", path, response.StatusCode);
                return null;
            }

            return JsonDocument.Parse(content);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("Paystack API request to '{Path}' timed out", path);
            return null;
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Paystack API request to '{Path}' failed", path);
            return null;
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Paystack API response from '{Path}' was not valid JSON", path);
            return null;
        }
    }

    private static string CreateReference(PaystackPaymentPurpose purpose)
    {
        return $"nerova_{purpose.ToString().ToLowerInvariant()}_{Guid.NewGuid():N}";
    }

    private static long ToSubunit(decimal amount)
    {
        return decimal.ToInt64(Math.Round(amount * 100m, 0, MidpointRounding.AwayFromZero));
    }

    private static decimal FromSubunit(long amount)
    {
        return decimal.Round(amount / 100m, 2);
    }

    private static PaymentMethod? CreatePaymentMethod(JsonElement root)
    {
        var brand = GetString(root, "data", "authorization", "brand") ?? GetString(root, "data", "authorization", "card_type");
        var last4 = GetString(root, "data", "authorization", "last4");
        var expMonthText = GetString(root, "data", "authorization", "exp_month");
        var expYearText = GetString(root, "data", "authorization", "exp_year");
        return brand is not null && last4 is not null && int.TryParse(expMonthText, out var expMonth) && int.TryParse(expYearText, out var expYear)
            ? new PaymentMethod(brand, last4, expMonth, expYear)
            : null;
    }

    private static PaystackPaymentPurpose? GetPaymentPurpose(JsonElement root)
    {
        var purposeText = GetString(root, "data", "metadata", "purpose");
        return Enum.TryParse<PaystackPaymentPurpose>(purposeText, true, out var purpose) ? purpose : null;
    }

    private static string? GetString(JsonElement element, params string[] path)
    {
        if (!TryGet(element, path, out var value)) return null;
        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.GetRawText(),
            _ => null
        };
    }

    private static long? GetLong(JsonElement element, params string[] path)
    {
        return TryGet(element, path, out var value) && value.TryGetInt64(out var result) ? result : null;
    }

    private static bool? GetBoolean(JsonElement element, params string[] path)
    {
        return TryGet(element, path, out var value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False ? value.GetBoolean() : null;
    }

    private static bool TryGet(JsonElement element, string[] path, out JsonElement value)
    {
        value = element;
        foreach (var segment in path)
        {
            if (value.ValueKind != JsonValueKind.Object || !value.TryGetProperty(segment, out value))
            {
                return false;
            }
        }

        return true;
    }
}
