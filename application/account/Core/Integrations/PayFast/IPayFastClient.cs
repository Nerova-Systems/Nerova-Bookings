namespace Account.Integrations.PayFast;

public interface IPayFastClient
{
    Task<string?> ProcessOnsitePaymentAsync(SortedDictionary<string, string> parameters, CancellationToken cancellationToken);

    // Amount is in rand (e.g., 299.00). Converted to cents internally before calling the API.
    Task<bool> ChargeTokenAsync(string token, decimal amountRand, string itemName, CancellationToken cancellationToken);

    Task<bool> CancelSubscriptionAsync(string token, CancellationToken cancellationToken);

    string GetUpdateCardUrl(string token);
}
