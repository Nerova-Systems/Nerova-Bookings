namespace Account.Integrations.PayFast;

public sealed class PayFastSettings
{
    public string MerchantId { get; init; } = "";

    public string MerchantKey { get; init; } = "";

    public string Passphrase { get; init; } = "";

    public bool Sandbox { get; init; }

    public string NotifyUrl { get; init; } = "";

    public string ReturnUrl { get; init; } = "";

    public string CancelUrl { get; init; } = "";

    public string AllowedIps { get; init; } = "";
}
