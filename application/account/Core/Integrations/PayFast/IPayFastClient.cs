namespace Account.Integrations.PayFast;

public interface IPayFastClient
{
    Task<string?> ProcessOnsitePaymentAsync(IDictionary<string, string> parameters, CancellationToken cancellationToken);

    // Amount is in rand (e.g., 299.00). Converted to cents internally before calling the API.
    Task<bool> ChargeTokenAsync(string token, decimal amountRand, string itemName, CancellationToken cancellationToken);

    Task<bool> CancelSubscriptionAsync(string token, CancellationToken cancellationToken);

    Task<PayFastSubscriptionDetails?> FetchSubscriptionAsync(string token, CancellationToken cancellationToken);

    Task<bool> UpdateSubscriptionAsync(string token, decimal amountRand, DateTimeOffset nextRunDate, CancellationToken cancellationToken);

    Task<PayFastRefundResult> RefundPaymentAsync(string providerPaymentId, decimal amountRand, string reason, CancellationToken cancellationToken);

    string GetUpdateCardUrl(string token);
}

public sealed record PayFastSubscriptionDetails(
    string Token,
    string Status,
    DateTimeOffset? NextRunDate,
    string? LatestPaymentId,
    decimal? Amount
);

public sealed record PayFastRefundResult(bool Succeeded, bool Supported, string? Reference, string? ErrorMessage);
