using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Account.Features.Subscriptions.Domain;
using Microsoft.Extensions.Configuration;
using PaymentMethod = Account.Features.Subscriptions.Domain.PaymentMethod;

namespace Account.Integrations.Paystack;

public sealed class PaystackClient(IHttpClientFactory httpClientFactory, IConfiguration configuration, ILogger<PaystackClient> logger) : IPaystackClient
{
    private const string DefaultBaseUrl = "https://api.paystack.co";

    private readonly string _baseUrl = (configuration["Paystack:BaseUrl"] ?? DefaultBaseUrl).TrimEnd('/');
    private readonly string _currency = configuration["Paystack:Currency"] ?? "ZAR";
    private readonly string? _secretKey = configuration["Paystack:SecretKey"] ?? configuration["Paystack:ApiKey"] ?? Environment.GetEnvironmentVariable("PAYSTACK_SECRET_KEY");
    private readonly string? _webhookSecret = configuration["Paystack:WebhookSecret"] ?? configuration["Paystack:SecretKey"] ?? Environment.GetEnvironmentVariable("PAYSTACK_SECRET_KEY");

    public async Task<PaystackCustomerId?> CreateCustomerAsync(string tenantName, string email, long tenantId, CancellationToken cancellationToken)
    {
        var data = await SendPaystackAsync(HttpMethod.Post, "/customer", new
            {
                email,
                metadata = new { tenantId, tenantName }
            }, cancellationToken
        );

        var customerCode = data is not null ? ReadString(data.Value, "customer_code") : null;
        if (customerCode is null)
        {
            logger.LogError("Paystack did not return a customer code for tenant '{TenantName}'", tenantName);
            return null;
        }

        logger.LogInformation("Created Paystack customer '{CustomerId}' for tenant '{TenantName}'", customerCode, tenantName);
        return PaystackCustomerId.NewId(customerCode);
    }

    public async Task<CheckoutSessionResult?> CreateCheckoutSessionAsync(PaystackCustomerId paystackCustomerId, string email, SubscriptionPlan plan, string? locale, CancellationToken cancellationToken)
    {
        var planSettings = GetPlanSettings(plan);
        if (planSettings.PlanCode is null)
        {
            logger.LogError("Paystack plan code is not configured for plan '{Plan}'", plan);
            return null;
        }

        var reference = CreateReference("sub");
        var data = await SendPaystackAsync(HttpMethod.Post, "/transaction/initialize", new
            {
                email,
                amount = planSettings.AmountCents,
                currency = planSettings.Currency,
                reference,
                callback_url = GetCallbackUrl(),
                plan = planSettings.PlanCode,
                metadata = new { paystackCustomerId = paystackCustomerId.Value, plan = plan.ToString() }
            }, cancellationToken
        );

        var authorizationUrl = data is not null ? ReadString(data.Value, "authorization_url") : null;
        var returnedReference = data is not null ? ReadString(data.Value, "reference") ?? reference : reference;

        return authorizationUrl is null ? null : new CheckoutSessionResult(returnedReference, authorizationUrl);
    }

    public async Task<SubscriptionSyncResult?> SyncSubscriptionStateAsync(PaystackCustomerId paystackCustomerId, CancellationToken cancellationToken)
    {
        var customer = await FetchCustomerAsync(paystackCustomerId, cancellationToken);
        if (customer is null)
        {
            return null;
        }

        var subscriptionCode = FindSubscriptionCode(customer.Value);
        if (subscriptionCode is null)
        {
            return null;
        }

        var subscription = await FetchSubscriptionAsync(PaystackSubscriptionId.NewId(subscriptionCode), cancellationToken);
        if (subscription is null)
        {
            return null;
        }

        var planElement = TryGetObject(subscription.Value, "plan");
        var planCode = planElement is not null ? ReadString(planElement.Value, "plan_code") : null;
        var plan = MapPlan(planCode);
        var amountCents = ReadDecimal(subscription.Value, "amount") ?? (planElement is not null ? ReadDecimal(planElement.Value, "amount") : null);
        var currency = planElement is not null ? ReadString(planElement.Value, "currency")?.ToUpperInvariant() : _currency;
        var status = ReadString(subscription.Value, "status");
        var subscriptionId = ReadString(subscription.Value, "subscription_code");

        return new SubscriptionSyncResult(
            plan,
            null,
            subscriptionId is not null ? PaystackSubscriptionId.NewId(subscriptionId) : null,
            amountCents / 100m,
            currency,
            ReadDateTimeOffset(subscription.Value, "next_payment_date"),
            status is "non-renewing" or "cancelled" or "complete",
            status is "cancelled" or "complete" ? CancellationReason.CancelledByAdmin : null,
            null,
            await SyncPaymentTransactionsAsync(paystackCustomerId, cancellationToken),
            planElement is null ? null : MapAuthorizationToPaymentMethod(TryGetObject(subscription.Value, "authorization")),
            status
        );
    }

    public async Task<PaystackSubscriptionId?> GetCheckoutSessionSubscriptionIdAsync(string reference, CancellationToken cancellationToken)
    {
        var data = await SendPaystackAsync(HttpMethod.Get, $"/transaction/verify/{Uri.EscapeDataString(reference)}", null, cancellationToken);
        if (data is null || ReadString(data.Value, "status") != "success")
        {
            return null;
        }

        var subscriptionCode = ReadString(data.Value, "subscription_code")
                            ?? (TryGetObject(data.Value, "subscription") is { } subscription ? ReadString(subscription, "subscription_code") : null);

        return subscriptionCode is null ? null : PaystackSubscriptionId.NewId(subscriptionCode);
    }

    public async Task<UpgradeSubscriptionResult?> UpgradeSubscriptionAsync(PaystackSubscriptionId paystackSubscriptionId, SubscriptionPlan newPlan, CancellationToken cancellationToken)
    {
        var planSettings = GetPlanSettings(newPlan);
        if (planSettings.PlanCode is null)
        {
            return new UpgradeSubscriptionResult(null, null, $"Paystack plan code is not configured for {newPlan}.");
        }

        var subscription = await FetchSubscriptionAsync(paystackSubscriptionId, cancellationToken);
        if (subscription is null)
        {
            return null;
        }

        var customerCode = TryGetObject(subscription.Value, "customer") is { } customer ? ReadString(customer, "customer_code") : null;
        var authorizationCode = TryGetObject(subscription.Value, "authorization") is { } authorization ? ReadString(authorization, "authorization_code") : null;
        if (customerCode is null || authorizationCode is null)
        {
            return new UpgradeSubscriptionResult(null, null, "A reusable Paystack authorization is required before upgrading.");
        }

        var created = await SendPaystackAsync(HttpMethod.Post, "/subscription", new
            {
                customer = customerCode,
                plan = planSettings.PlanCode,
                authorization = authorizationCode
            }, cancellationToken
        );

        if (created is null)
        {
            return null;
        }

        await DisableSubscriptionAsync(paystackSubscriptionId, subscription.Value, cancellationToken);
        return new UpgradeSubscriptionResult(null, null);
    }

    public async Task<bool> CancelSubscriptionAtPeriodEndAsync(PaystackSubscriptionId paystackSubscriptionId, CancellationReason reason, string? feedback, CancellationToken cancellationToken)
    {
        var subscription = await FetchSubscriptionAsync(paystackSubscriptionId, cancellationToken);
        return subscription is not null && await DisableSubscriptionAsync(paystackSubscriptionId, subscription.Value, cancellationToken);
    }

    public async Task<bool> ReactivateSubscriptionAsync(PaystackSubscriptionId paystackSubscriptionId, CancellationToken cancellationToken)
    {
        var subscription = await FetchSubscriptionAsync(paystackSubscriptionId, cancellationToken);
        var token = subscription is not null ? ReadString(subscription.Value, "email_token") : null;
        if (token is null)
        {
            return false;
        }

        var data = await SendPaystackAsync(HttpMethod.Post, "/subscription/enable", new { code = paystackSubscriptionId.Value, token }, cancellationToken);
        return data is not null;
    }

    public Task<PriceCatalogItem[]> GetPriceCatalogAsync(CancellationToken cancellationToken)
    {
        var standard = GetPlanSettings(SubscriptionPlan.Standard);
        var premium = GetPlanSettings(SubscriptionPlan.Premium);

        var items = new List<PriceCatalogItem>(2);
        if (standard.PlanCode is not null)
        {
            items.Add(new PriceCatalogItem(SubscriptionPlan.Standard, standard.AmountCents / 100m, standard.Currency, "month", 1, false));
        }

        if (premium.PlanCode is not null)
        {
            items.Add(new PriceCatalogItem(SubscriptionPlan.Premium, premium.AmountCents / 100m, premium.Currency, "month", 1, false));
        }

        return Task.FromResult(items.ToArray());
    }

    public PaystackWebhookEventResult? VerifyWebhookSignature(string payload, string signatureHeader)
    {
        if (string.IsNullOrWhiteSpace(_webhookSecret) || string.IsNullOrWhiteSpace(signatureHeader))
        {
            return null;
        }

        var expectedSignature = ComputeSignature(payload, _webhookSecret);
        if (!FixedTimeEquals(expectedSignature, signatureHeader.Trim()))
        {
            return null;
        }

        using var document = JsonDocument.Parse(payload);
        var root = document.RootElement;
        var eventType = ReadString(root, "event") ?? "unknown";
        var data = TryGetObject(root, "data");
        var customerId = data is not null ? ExtractCustomerId(data.Value) : null;
        var eventId = data is not null
            ? ReadString(data.Value, "id") ?? ReadString(data.Value, "reference") ?? ReadString(data.Value, "subscription_code")
            : null;

        eventId ??= $"{eventType}_{expectedSignature[..Math.Min(32, expectedSignature.Length)]}";
        return new PaystackWebhookEventResult(eventId, eventType, customerId);
    }

    public async Task<CustomerBillingResult?> GetCustomerBillingInfoAsync(PaystackCustomerId paystackCustomerId, CancellationToken cancellationToken)
    {
        var customer = await FetchCustomerAsync(paystackCustomerId, cancellationToken);
        if (customer is null)
        {
            return null;
        }

        var firstName = ReadString(customer.Value, "first_name");
        var lastName = ReadString(customer.Value, "last_name");
        var name = string.Join(" ", new[] { firstName, lastName }.Where(s => !string.IsNullOrWhiteSpace(s)));
        var email = ReadString(customer.Value, "email");
        var billingInfo = new BillingInfo(string.IsNullOrWhiteSpace(name) ? null : name, null, email, null);
        var paymentMethod = ReadFirstArrayObject(customer.Value, "authorizations") is { } authorization ? MapAuthorizationToPaymentMethod(authorization) : null;

        return new CustomerBillingResult(billingInfo, false, paymentMethod);
    }

    public async Task<bool> UpdateCustomerBillingInfoAsync(PaystackCustomerId paystackCustomerId, BillingInfo billingInfo, string? locale, CancellationToken cancellationToken)
    {
        var nameParts = (billingInfo.Name ?? "").Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var data = await SendPaystackAsync(HttpMethod.Put, $"/customer/{Uri.EscapeDataString(paystackCustomerId.Value)}", new
            {
                first_name = nameParts.Length > 0 ? nameParts[0] : null,
                last_name = nameParts.Length > 1 ? nameParts[1] : null,
                metadata = new { address = billingInfo.Address, taxId = billingInfo.TaxId, locale }
            }, cancellationToken
        );

        return data is not null;
    }

    public Task<bool> SyncCustomerTaxIdAsync(PaystackCustomerId paystackCustomerId, string? taxId, CancellationToken cancellationToken)
    {
        return Task.FromResult(true);
    }

    public async Task<string?> CreatePaymentMethodUpdateLinkAsync(PaystackSubscriptionId paystackSubscriptionId, CancellationToken cancellationToken)
    {
        var data = await SendPaystackAsync(HttpMethod.Get, $"/subscription/{Uri.EscapeDataString(paystackSubscriptionId.Value)}/manage/link", null, cancellationToken);
        return data is not null ? ReadString(data.Value, "link") : null;
    }

    public async Task<PaymentMethod?> GetPaymentMethodFromTransactionAsync(string reference, CancellationToken cancellationToken)
    {
        var data = await SendPaystackAsync(HttpMethod.Get, $"/transaction/verify/{Uri.EscapeDataString(reference)}", null, cancellationToken);
        if (data is null || ReadString(data.Value, "status") != "success")
        {
            return null;
        }

        return MapAuthorizationToPaymentMethod(TryGetObject(data.Value, "authorization"));
    }

    public Task<bool> SetSubscriptionDefaultPaymentMethodAsync(PaystackSubscriptionId paystackSubscriptionId, string paymentMethodId, CancellationToken cancellationToken)
    {
        return Task.FromResult(!string.IsNullOrWhiteSpace(paymentMethodId));
    }

    public Task<bool> SetCustomerDefaultPaymentMethodAsync(PaystackCustomerId paystackCustomerId, string paymentMethodId, CancellationToken cancellationToken)
    {
        return Task.FromResult(!string.IsNullOrWhiteSpace(paymentMethodId));
    }

    public async Task<OpenInvoiceResult?> GetOpenInvoiceAsync(PaystackSubscriptionId paystackSubscriptionId, CancellationToken cancellationToken)
    {
        var subscription = await FetchSubscriptionAsync(paystackSubscriptionId, cancellationToken);
        if (subscription is null || ReadString(subscription.Value, "open_invoice") is null)
        {
            return null;
        }

        var amountCents = ReadDecimal(subscription.Value, "amount") ?? 0m;
        var currency = TryGetObject(subscription.Value, "plan") is { } plan ? ReadString(plan, "currency")?.ToUpperInvariant() ?? _currency : _currency;
        return new OpenInvoiceResult(amountCents / 100m, currency);
    }

    public Task<InvoiceRetryResult?> RetryOpenInvoicePaymentAsync(PaystackSubscriptionId paystackSubscriptionId, string? paymentMethodId, CancellationToken cancellationToken)
    {
        return Task.FromResult<InvoiceRetryResult?>(new InvoiceRetryResult(false, null, null, "Paystack does not expose an invoice retry endpoint for subscriptions. Ask the customer to update their payment method or wait for Paystack's next retry."));
    }

    public Task<UpgradePreviewResult?> GetUpgradePreviewAsync(PaystackSubscriptionId paystackSubscriptionId, SubscriptionPlan newPlan, CancellationToken cancellationToken)
    {
        var planSettings = GetPlanSettings(newPlan);
        return Task.FromResult<UpgradePreviewResult?>(new UpgradePreviewResult(planSettings.AmountCents / 100m, planSettings.Currency, [new UpgradePreviewLineItem(newPlan.ToString(), planSettings.AmountCents / 100m, planSettings.Currency, false, false)]));
    }

    public Task<CheckoutPreviewResult?> GetCheckoutPreviewAsync(PaystackCustomerId paystackCustomerId, SubscriptionPlan plan, CancellationToken cancellationToken)
    {
        var planSettings = GetPlanSettings(plan);
        return Task.FromResult<CheckoutPreviewResult?>(new CheckoutPreviewResult(planSettings.AmountCents / 100m, planSettings.Currency, 0m));
    }

    public async Task<SubscribeResult?> CreateSubscriptionWithSavedPaymentMethodAsync(PaystackCustomerId paystackCustomerId, SubscriptionPlan plan, CancellationToken cancellationToken)
    {
        var planSettings = GetPlanSettings(plan);
        if (planSettings.PlanCode is null)
        {
            return null;
        }

        var customer = await FetchCustomerAsync(paystackCustomerId, cancellationToken);
        var authorizationCode = customer is not null && ReadFirstArrayObject(customer.Value, "authorizations") is { } authorization
            ? ReadString(authorization, "authorization_code")
            : null;

        if (authorizationCode is null)
        {
            return null;
        }

        var data = await SendPaystackAsync(HttpMethod.Post, "/subscription", new
            {
                customer = paystackCustomerId.Value,
                plan = planSettings.PlanCode,
                authorization = authorizationCode
            }, cancellationToken
        );

        return data is null ? null : new SubscribeResult(null, null);
    }

    public async Task<PaymentTransaction[]?> SyncPaymentTransactionsAsync(PaystackCustomerId paystackCustomerId, CancellationToken cancellationToken)
    {
        var customer = await FetchCustomerAsync(paystackCustomerId, cancellationToken);
        var customerId = customer is not null ? ReadString(customer.Value, "id") : null;
        if (customerId is null)
        {
            return null;
        }

        var data = await SendPaystackAsync(HttpMethod.Get, $"/transaction?customer={Uri.EscapeDataString(customerId)}&perPage=100", null, cancellationToken);
        if (data is null || data.Value.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return data.Value.EnumerateArray().Select(MapTransaction).ToArray();
    }

    private async Task<JsonElement?> FetchCustomerAsync(PaystackCustomerId paystackCustomerId, CancellationToken cancellationToken)
    {
        return await SendPaystackAsync(HttpMethod.Get, $"/customer/{Uri.EscapeDataString(paystackCustomerId.Value)}", null, cancellationToken);
    }

    private async Task<JsonElement?> FetchSubscriptionAsync(PaystackSubscriptionId paystackSubscriptionId, CancellationToken cancellationToken)
    {
        return await SendPaystackAsync(HttpMethod.Get, $"/subscription/{Uri.EscapeDataString(paystackSubscriptionId.Value)}", null, cancellationToken);
    }

    private async Task<bool> DisableSubscriptionAsync(PaystackSubscriptionId paystackSubscriptionId, JsonElement subscription, CancellationToken cancellationToken)
    {
        var token = ReadString(subscription, "email_token");
        if (token is null)
        {
            return false;
        }

        var data = await SendPaystackAsync(HttpMethod.Post, "/subscription/disable", new { code = paystackSubscriptionId.Value, token }, cancellationToken);
        return data is not null;
    }

    private async Task<JsonElement?> SendPaystackAsync(HttpMethod method, string path, object? body, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_secretKey))
        {
            logger.LogWarning("Paystack secret key is not configured");
            return null;
        }

        using var request = new HttpRequestMessage(method, $"{_baseUrl}{path}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _secretKey);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        try
        {
            var response = await httpClientFactory.CreateClient().SendAsync(request, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Paystack request '{Method} {Path}' failed with status {StatusCode}: {Body}", method, path, response.StatusCode, responseBody);
                return null;
            }

            if (string.IsNullOrWhiteSpace(responseBody))
            {
                return default(JsonElement);
            }

            using var document = JsonDocument.Parse(responseBody);
            var root = document.RootElement;
            if (root.TryGetProperty("status", out var status) && status.ValueKind == JsonValueKind.False)
            {
                logger.LogWarning("Paystack request '{Method} {Path}' returned unsuccessful response: {Message}", method, path, ReadString(root, "message"));
                return null;
            }

            return root.TryGetProperty("data", out var data) ? data.Clone() : default(JsonElement);
        }
        catch (TaskCanceledException ex)
        {
            logger.LogError(ex, "Timeout during Paystack request '{Method} {Path}'", method, path);
            return null;
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "Paystack response for '{Method} {Path}' was not valid JSON", method, path);
            return null;
        }
    }

    private PlanSettings GetPlanSettings(SubscriptionPlan plan)
    {
        return plan switch
        {
            SubscriptionPlan.Standard => new PlanSettings(
                configuration["Paystack:Plans:Standard:Code"] ?? configuration["Paystack:StandardPlanCode"],
                GetAmountCents("Paystack:Plans:Standard:AmountCents", "Paystack:StandardAmountCents", 2900),
                configuration["Paystack:Plans:Standard:Currency"] ?? _currency
            ),
            SubscriptionPlan.Premium => new PlanSettings(
                configuration["Paystack:Plans:Premium:Code"] ?? configuration["Paystack:PremiumPlanCode"],
                GetAmountCents("Paystack:Plans:Premium:AmountCents", "Paystack:PremiumAmountCents", 9900),
                configuration["Paystack:Plans:Premium:Currency"] ?? _currency
            ),
            _ => new PlanSettings(null, 0, _currency)
        };
    }

    private int GetAmountCents(string primaryKey, string fallbackKey, int fallback)
    {
        return configuration.GetValue<int?>(primaryKey)
            ?? configuration.GetValue<int?>(fallbackKey)
            ?? fallback;
    }

    private string? GetCallbackUrl()
    {
        return configuration["Paystack:CallbackUrl"];
    }

    private SubscriptionPlan MapPlan(string? planCode)
    {
        if (planCode is null) return SubscriptionPlan.Basis;

        if (planCode == GetPlanSettings(SubscriptionPlan.Standard).PlanCode)
        {
            return SubscriptionPlan.Standard;
        }

        if (planCode == GetPlanSettings(SubscriptionPlan.Premium).PlanCode)
        {
            return SubscriptionPlan.Premium;
        }

        return SubscriptionPlan.Basis;
    }

    private static string CreateReference(string prefix)
    {
        return $"NB-{prefix}-{Guid.NewGuid():N}";
    }

    private static string? FindSubscriptionCode(JsonElement customer)
    {
        if (!customer.TryGetProperty("subscriptions", out var subscriptions) || subscriptions.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var preferred = subscriptions.EnumerateArray()
            .FirstOrDefault(s => ReadString(s, "status") is "active" or "attention" or "non-renewing");

        if (preferred.ValueKind != JsonValueKind.Undefined)
        {
            return ReadString(preferred, "subscription_code");
        }

        return subscriptions.EnumerateArray().Select(s => ReadString(s, "subscription_code")).FirstOrDefault(s => s is not null);
    }

    private static PaystackCustomerId? ExtractCustomerId(JsonElement data)
    {
        var customerCode = ReadString(data, "customer_code")
                        ?? (TryGetObject(data, "customer") is { } customer ? ReadString(customer, "customer_code") : null);

        return PaystackCustomerId.TryParse(customerCode, out var customerId) ? customerId : null;
    }

    private static PaymentMethod? MapAuthorizationToPaymentMethod(JsonElement? authorization)
    {
        if (authorization is null || authorization.Value.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var brand = ReadString(authorization.Value, "brand") ?? ReadString(authorization.Value, "card_type") ?? "card";
        var last4 = ReadString(authorization.Value, "last4") ?? "****";
        var expMonth = ReadInt(authorization.Value, "exp_month") ?? 0;
        var expYear = ReadInt(authorization.Value, "exp_year") ?? 0;
        return new PaymentMethod(brand.Trim(), last4, expMonth, expYear);
    }

    private static PaymentTransaction MapTransaction(JsonElement transaction)
    {
        var amount = (ReadDecimal(transaction, "amount") ?? 0m) / 100m;
        var currency = ReadString(transaction, "currency")?.ToUpperInvariant() ?? "ZAR";
        var status = ReadString(transaction, "status") switch
        {
            "success" => PaymentTransactionStatus.Succeeded,
            "failed" => PaymentTransactionStatus.Failed,
            "reversed" => PaymentTransactionStatus.Refunded,
            _ => PaymentTransactionStatus.Pending
        };

        return new PaymentTransaction(
            PaymentTransactionId.NewId(),
            amount,
            currency,
            status,
            ReadDateTimeOffset(transaction, "paid_at") ?? ReadDateTimeOffset(transaction, "created_at") ?? DateTimeOffset.UtcNow,
            ReadString(transaction, "gateway_response"),
            null,
            null
        );
    }

    private static JsonElement? TryGetObject(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.Object ? property : null;
    }

    private static JsonElement? ReadFirstArrayObject(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var item in property.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.Object)
            {
                return item;
            }
        }

        return null;
    }

    private static string? ReadString(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (!element.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        return property.ValueKind switch
        {
            JsonValueKind.String => property.GetString(),
            JsonValueKind.Number => property.ToString(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => null
        };
    }

    private static int? ReadInt(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (!element.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        if (property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out var number))
        {
            return number;
        }

        return int.TryParse(property.GetString(), out var parsed) ? parsed : null;
    }

    private static decimal? ReadDecimal(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (!element.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        if (property.ValueKind == JsonValueKind.Number && property.TryGetDecimal(out var number))
        {
            return number;
        }

        return decimal.TryParse(property.GetString(), out var parsed) ? parsed : null;
    }

    private static DateTimeOffset? ReadDateTimeOffset(JsonElement element, string propertyName)
    {
        var value = ReadString(element, propertyName);
        return DateTimeOffset.TryParse(value, out var parsed) ? parsed : null;
    }

    private static string ComputeSignature(string payload, string secret)
    {
        using var hmac = new HMACSHA512(Encoding.UTF8.GetBytes(secret));
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
    }

    private static bool FixedTimeEquals(string expected, string actual)
    {
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var actualBytes = Encoding.UTF8.GetBytes(actual.ToLowerInvariant());
        return expectedBytes.Length == actualBytes.Length && CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes);
    }

    private sealed record PlanSettings(string? PlanCode, int AmountCents, string Currency);
}
