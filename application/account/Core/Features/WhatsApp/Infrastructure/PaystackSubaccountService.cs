using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using SharedKernel.Cqrs;

namespace Account.Features.WhatsApp.Infrastructure;

public interface IPaystackSubaccountService
{
    Task<Result<string>> CreateSubaccount(
        string businessName,
        string settlementBank,
        string accountNumber,
        decimal percentageCharge,
        CancellationToken cancellationToken);

    Task<Result<string>> UpdateSplitPercentage(
        string subaccountCode,
        decimal newPercentage,
        CancellationToken cancellationToken);
}

public sealed class PaystackSubaccountService(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    ILogger<PaystackSubaccountService> logger
) : IPaystackSubaccountService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public async Task<Result<string>> CreateSubaccount(
        string businessName,
        string settlementBank,
        string accountNumber,
        decimal percentageCharge,
        CancellationToken cancellationToken)
    {
        var payload = new
        {
            business_name = businessName,
            settlement_bank = settlementBank,
            account_number = accountNumber,
            percentage_charge = percentageCharge
        };

        var response = await SendAsync(HttpMethod.Post, "/subaccount", payload, cancellationToken);
        if (response is null)
        {
            return Result<string>.BadRequest("Failed to create Paystack subaccount: no response received.");
        }

        if (!IsSuccessResponse(response.RootElement))
        {
            var message = GetString(response.RootElement, "message") ?? "Paystack subaccount creation failed.";
            logger.LogWarning("Paystack subaccount creation failed: {Message}", message);
            return Result<string>.BadRequest(message);
        }

        var subaccountCode = GetString(response.RootElement, "data", "subaccount_code");
        if (subaccountCode is null)
        {
            logger.LogWarning("Paystack subaccount creation response did not include subaccount_code");
            return Result<string>.BadRequest("Paystack subaccount creation response did not include subaccount_code.");
        }

        return subaccountCode;
    }

    public async Task<Result<string>> UpdateSplitPercentage(
        string subaccountCode,
        decimal newPercentage,
        CancellationToken cancellationToken)
    {
        var payload = new { percentage_charge = newPercentage };

        var response = await SendAsync(HttpMethod.Put, $"/subaccount/{subaccountCode}", payload, cancellationToken);
        if (response is null)
        {
            return Result<string>.BadRequest("Failed to update Paystack subaccount: no response received.");
        }

        if (!IsSuccessResponse(response.RootElement))
        {
            var message = GetString(response.RootElement, "message") ?? "Paystack subaccount update failed.";
            logger.LogWarning("Paystack subaccount update failed: {Message}", message);
            return Result<string>.BadRequest(message);
        }

        return subaccountCode;
    }

    private async Task<JsonDocument?> SendAsync(HttpMethod method, string path, object payload, CancellationToken cancellationToken)
    {
        try
        {
            using var client = httpClientFactory.CreateClient("Paystack");
            var secretKey = configuration["Paystack:SecretKey"];

            var request = new HttpRequestMessage(method, path)
            {
                Content = JsonContent.Create(payload, options: SerializerOptions)
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", secretKey);

            var response = await client.SendAsync(request, cancellationToken);
            var content = await response.Content.ReadAsStringAsync(cancellationToken);

            if (string.IsNullOrWhiteSpace(content))
            {
                return null;
            }

            return JsonDocument.Parse(content);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            logger.LogError(ex, "Error communicating with Paystack API at {Path}", path);
            return null;
        }
    }

    private static bool IsSuccessResponse(JsonElement root)
    {
        if (root.TryGetProperty("status", out var statusElement))
        {
            return statusElement.ValueKind == JsonValueKind.True;
        }

        return false;
    }

    private static string? GetString(JsonElement root, params string[] path)
    {
        var current = root;
        foreach (var segment in path)
        {
            if (!current.TryGetProperty(segment, out var next))
            {
                return null;
            }

            current = next;
        }

        return current.ValueKind == JsonValueKind.String ? current.GetString() : null;
    }
}
