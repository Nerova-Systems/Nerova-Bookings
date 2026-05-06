using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Main.Features.Appointments;

public interface IPaystackClient
{
    Task<PaystackTransactionResult?> InitializeTransactionAsync(PaystackTransactionRequest request, CancellationToken cancellationToken);

    Task<bool> IsTransactionSuccessfulAsync(string reference, int amountCents, CancellationToken cancellationToken);

    Task<IReadOnlyList<PaystackBankOption>> ListBanksAsync(CancellationToken cancellationToken);

    Task<PaystackResolvedAccount> ResolveAccountAsync(string bankCode, string accountNumber, CancellationToken cancellationToken);

    Task<PaystackSubaccountResult> CreateSubaccountAsync(PaystackSubaccountRequest request, CancellationToken cancellationToken);

    Task<PaystackSubaccountResult> UpdateSubaccountAsync(string subaccountCode, PaystackSubaccountRequest request, CancellationToken cancellationToken);

    Task<IReadOnlyList<PaystackSettlementResult>> ListSettlementsAsync(string subaccountCode, CancellationToken cancellationToken);

    Task<PaystackSplitResult> CreateSplitAsync(PaystackSplitRequest request, CancellationToken cancellationToken);

    Task<PaystackVirtualTerminalResult> CreateVirtualTerminalAsync(PaystackVirtualTerminalRequest request, CancellationToken cancellationToken);

    Task AssignSplitToVirtualTerminalAsync(string terminalCode, string splitCode, CancellationToken cancellationToken);
}

public sealed class PaystackClient(IHttpClientFactory httpClientFactory) : IPaystackClient
{
    public async Task<PaystackTransactionResult?> InitializeTransactionAsync(PaystackTransactionRequest request, CancellationToken cancellationToken)
    {
        var secret = GetSecretOrNull();
        if (secret is null) return null;

        using var message = new HttpRequestMessage(HttpMethod.Post, "https://api.paystack.co/transaction/initialize")
        {
            Content = JsonContent.Create(new
                {
                    email = request.Email,
                    amount = request.AmountCents,
                    currency = "ZAR",
                    reference = request.Reference,
                    callback_url = request.CallbackUrl,
                    channels = new[] { "card", "bank", "apple_pay", "eft", "capitec_pay" },
                    subaccount = request.SubaccountCode
                }
            )
        };
        AddAuthorization(message, secret);
        var response = await httpClientFactory.CreateClient().SendAsync(message, cancellationToken);
        if (!response.IsSuccessStatusCode) return null;
        var json = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        var data = json.GetProperty("data");
        return new PaystackTransactionResult(
            data.GetProperty("authorization_url").GetString() ?? string.Empty,
            data.GetProperty("access_code").GetString() ?? string.Empty,
            data.GetProperty("reference").GetString() ?? request.Reference
        );
    }

    public async Task<bool> IsTransactionSuccessfulAsync(string reference, int amountCents, CancellationToken cancellationToken)
    {
        var secret = GetSecretOrNull();
        if (secret is null) return false;

        using var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.paystack.co/transaction/verify/{reference}");
        AddAuthorization(request, secret);
        var response = await httpClientFactory.CreateClient().SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode) return false;

        var json = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        var data = json.GetProperty("data");
        return json.GetProperty("status").GetBoolean() &&
               data.GetProperty("status").GetString() == "success" &&
               data.GetProperty("reference").GetString() == reference &&
               IsExpectedCurrency(data) &&
               IsExpectedAmount(data, amountCents);
    }

    public async Task<IReadOnlyList<PaystackBankOption>> ListBanksAsync(CancellationToken cancellationToken)
    {
        var secret = GetSecretOrThrow();
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.paystack.co/bank?country=south%20africa&currency=ZAR&enabled_for_verification=true&perPage=100");
        AddAuthorization(request, secret);
        var response = await httpClientFactory.CreateClient().SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        return json.GetProperty("data")
            .EnumerateArray()
            .Select(bank => new PaystackBankOption(
                    bank.GetProperty("name").GetString() ?? string.Empty,
                    bank.GetProperty("code").GetString() ?? string.Empty,
                    bank.TryGetProperty("currency", out var currency) ? currency.GetString() ?? "ZAR" : "ZAR",
                    bank.TryGetProperty("country", out var country) ? country.GetString() ?? "South Africa" : "South Africa"
                )
            )
            .Where(bank => !string.IsNullOrWhiteSpace(bank.Code))
            .ToList();
    }

    public async Task<PaystackResolvedAccount> ResolveAccountAsync(string bankCode, string accountNumber, CancellationToken cancellationToken)
    {
        var secret = GetSecretOrThrow();
        using var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.paystack.co/bank/resolve?account_number={Uri.EscapeDataString(accountNumber)}&bank_code={Uri.EscapeDataString(bankCode)}");
        AddAuthorization(request, secret);
        var response = await httpClientFactory.CreateClient().SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        var data = json.GetProperty("data");
        return new PaystackResolvedAccount(
            data.GetProperty("account_number").GetString() ?? accountNumber,
            data.GetProperty("account_name").GetString() ?? string.Empty
        );
    }

    public Task<PaystackSubaccountResult> CreateSubaccountAsync(PaystackSubaccountRequest request, CancellationToken cancellationToken)
    {
        return SendSubaccountAsync(HttpMethod.Post, "https://api.paystack.co/subaccount", request, cancellationToken);
    }

    public Task<PaystackSubaccountResult> UpdateSubaccountAsync(string subaccountCode, PaystackSubaccountRequest request, CancellationToken cancellationToken)
    {
        return SendSubaccountAsync(HttpMethod.Put, $"https://api.paystack.co/subaccount/{Uri.EscapeDataString(subaccountCode)}", request, cancellationToken);
    }

    public async Task<IReadOnlyList<PaystackSettlementResult>> ListSettlementsAsync(string subaccountCode, CancellationToken cancellationToken)
    {
        var secret = GetSecretOrThrow();
        using var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.paystack.co/settlement?perPage=10&subaccount={Uri.EscapeDataString(subaccountCode)}");
        AddAuthorization(request, secret);
        var response = await httpClientFactory.CreateClient().SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        return json.GetProperty("data")
            .EnumerateArray()
            .Select(settlement => new PaystackSettlementResult(
                    settlement.GetProperty("id").ToString(),
                    settlement.GetProperty("status").GetString() ?? string.Empty,
                    settlement.TryGetProperty("total_amount", out var total) ? total.GetInt32() : 0,
                    settlement.TryGetProperty("effective_amount", out var effective) ? effective.GetInt32() : 0,
                    settlement.TryGetProperty("total_fees", out var fees) ? fees.GetInt32() : 0,
                    settlement.TryGetProperty("settlement_date", out var date) && DateTimeOffset.TryParse(date.GetString(), out var parsed) ? parsed : null
                )
            )
            .ToList();
    }

    public async Task<PaystackSplitResult> CreateSplitAsync(PaystackSplitRequest request, CancellationToken cancellationToken)
    {
        var secret = GetSecretOrThrow();
        using var message = new HttpRequestMessage(HttpMethod.Post, "https://api.paystack.co/split")
        {
            Content = JsonContent.Create(new
                {
                    name = request.Name,
                    type = "percentage",
                    currency = request.Currency,
                    subaccounts = new[] { new { subaccount = request.SubaccountCode, share = 100 } },
                    bearer_type = "subaccount",
                    bearer_subaccount = request.SubaccountCode
                }
            )
        };
        AddAuthorization(message, secret);
        var response = await httpClientFactory.CreateClient().SendAsync(message, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        return new PaystackSplitResult(json.GetProperty("data").GetProperty("split_code").GetString() ?? string.Empty);
    }

    public async Task<PaystackVirtualTerminalResult> CreateVirtualTerminalAsync(PaystackVirtualTerminalRequest request, CancellationToken cancellationToken)
    {
        var secret = GetSecretOrThrow();
        using var message = new HttpRequestMessage(HttpMethod.Post, "https://api.paystack.co/virtual_terminal")
        {
            Content = JsonContent.Create(new
                {
                    name = request.Name,
                    currency = request.Currency,
                    destinations = new[] { new { target = request.DestinationPhone, name = request.DestinationName } },
                    metadata = JsonSerializer.Serialize(new { tenantId = request.TenantReference })
                }
            )
        };
        AddAuthorization(message, secret);
        var response = await httpClientFactory.CreateClient().SendAsync(message, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        var data = json.GetProperty("data");
        return new PaystackVirtualTerminalResult(
            data.GetProperty("code").GetString() ?? string.Empty,
            data.GetProperty("name").GetString() ?? request.Name,
            data.TryGetProperty("active", out var active) && active.ValueKind == JsonValueKind.True,
            data.TryGetProperty("currency", out var currency) ? currency.GetString() ?? request.Currency : request.Currency
        );
    }

    public async Task AssignSplitToVirtualTerminalAsync(string terminalCode, string splitCode, CancellationToken cancellationToken)
    {
        var secret = GetSecretOrThrow();
        using var message = new HttpRequestMessage(HttpMethod.Put, $"https://api.paystack.co/virtual_terminal/{Uri.EscapeDataString(terminalCode)}/split_code")
        {
            Content = JsonContent.Create(new { split_code = splitCode })
        };
        AddAuthorization(message, secret);
        var response = await httpClientFactory.CreateClient().SendAsync(message, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    private async Task<PaystackSubaccountResult> SendSubaccountAsync(HttpMethod method, string url, PaystackSubaccountRequest request, CancellationToken cancellationToken)
    {
        var secret = GetSecretOrThrow();
        using var message = new HttpRequestMessage(method, url)
        {
            Content = JsonContent.Create(new
                {
                    business_name = request.BusinessName,
                    settlement_bank = request.BankCode,
                    account_number = request.AccountNumber,
                    percentage_charge = 0,
                    description = request.Description,
                    primary_contact_name = request.PrimaryContactName,
                    primary_contact_email = request.PrimaryContactEmail,
                    primary_contact_phone = request.PrimaryContactPhone
                }
            )
        };
        AddAuthorization(message, secret);
        var response = await httpClientFactory.CreateClient().SendAsync(message, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        var data = json.GetProperty("data");
        return new PaystackSubaccountResult(
            data.GetProperty("subaccount_code").GetString() ?? string.Empty,
            data.TryGetProperty("id", out var id) && id.TryGetInt32(out var parsedId) ? parsedId : null,
            data.TryGetProperty("business_name", out var businessName) ? businessName.GetString() ?? request.BusinessName : request.BusinessName,
            data.TryGetProperty("settlement_bank", out var bank) ? bank.GetString() ?? request.BankName : request.BankName,
            request.BankCode,
            data.TryGetProperty("account_name", out var accountName) ? accountName.GetString() ?? request.AccountName : request.AccountName,
            data.TryGetProperty("account_number", out var accountNumber) ? accountNumber.GetString() ?? request.AccountNumber : request.AccountNumber,
            data.TryGetProperty("currency", out var currency) ? currency.GetString() ?? "ZAR" : "ZAR",
            data.TryGetProperty("active", out var active) && active.ValueKind switch { JsonValueKind.True => true, JsonValueKind.Number => active.GetInt32() == 1, _ => false },
            data.TryGetProperty("is_verified", out var verified) && verified.ValueKind == JsonValueKind.True,
            data.TryGetProperty("settlement_schedule", out var schedule) ? schedule.GetString() ?? "auto" : "auto"
        );
    }

    private static void AddAuthorization(HttpRequestMessage request, string secret)
    {
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", secret);
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode) return;
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new InvalidOperationException(string.IsNullOrWhiteSpace(body) ? "Paystack request failed." : body);
    }

    private static string GetSecretOrThrow()
    {
        return GetSecretOrNull() ?? throw new InvalidOperationException("PAYSTACK_SECRET_KEY is not configured.");
    }

    private static string? GetSecretOrNull()
    {
        var secret = Environment.GetEnvironmentVariable("PAYSTACK_SECRET_KEY");
        return !string.IsNullOrWhiteSpace(secret) && secret.StartsWith("sk_", StringComparison.Ordinal) ? secret : null;
    }

    private static bool IsExpectedCurrency(JsonElement data)
    {
        return !data.TryGetProperty("currency", out var currency) ||
               string.Equals(currency.GetString(), "ZAR", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsExpectedAmount(JsonElement data, int amountCents)
    {
        if (TryReadInt32(data, "requested_amount", out var requestedAmount))
        {
            return requestedAmount == amountCents;
        }

        return TryReadInt32(data, "amount", out var amount) && amount == amountCents;
    }

    private static bool TryReadInt32(JsonElement data, string propertyName, out int value)
    {
        value = 0;
        return data.TryGetProperty(propertyName, out var property) &&
               property.ValueKind == JsonValueKind.Number &&
               property.TryGetInt32(out value);
    }
}

public sealed record PaystackTransactionRequest(string Reference, string Email, int AmountCents, string CallbackUrl, string SubaccountCode);

public sealed record PaystackTransactionResult(string AuthorizationUrl, string AccessCode, string Reference);

public sealed record PaystackBankOption(string Name, string Code, string Currency, string Country);

public sealed record PaystackResolvedAccount(string AccountNumber, string AccountName);

public sealed record PaystackSubaccountRequest(
    string BusinessName,
    string BankName,
    string BankCode,
    string AccountNumber,
    string AccountName,
    string Description,
    string? PrimaryContactName,
    string? PrimaryContactEmail,
    string? PrimaryContactPhone
);

public sealed record PaystackSubaccountResult(
    string SubaccountCode,
    int? SubaccountId,
    string BusinessName,
    string BankName,
    string BankCode,
    string AccountName,
    string AccountNumber,
    string Currency,
    bool Active,
    bool IsVerified,
    string SettlementSchedule
);

public sealed record PaystackSettlementResult(
    string Id,
    string Status,
    int TotalAmountCents,
    int EffectiveAmountCents,
    int FeesCents,
    DateTimeOffset? SettlementDate
);

public sealed record PaystackSplitRequest(string Name, string SubaccountCode, string Currency);

public sealed record PaystackSplitResult(string SplitCode);

public sealed record PaystackVirtualTerminalRequest(string Name, string DestinationPhone, string DestinationName, string Currency, string TenantReference);

public sealed record PaystackVirtualTerminalResult(string Code, string Name, bool Active, string Currency);
