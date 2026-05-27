using JetBrains.Annotations;
using SharedKernel.Domain;

namespace Main.Features.WhatsAppFlows.Infrastructure;

/// <summary>
///     Read-only DTO mirroring the WABA + key material owned by the account SCS. Fetched on
///     demand by <see cref="IWhatsAppFlowProfileSync" />; never persisted in main.
/// </summary>
[PublicAPI]
public sealed record WhatsAppFlowProfile(
    TenantId TenantId,
    string WabaId,
    string? PhoneNumberId,
    string? DisplayPhoneNumber,
    string? FlowId,
    string FlowStatus,
    string OnboardingGateStatus,
    string? WabaAccessToken,
    string? EncryptedPrivateKey,
    string? PrivateKeyIv,
    string? PublicKeyFingerprint
)
{
    public bool IsOnboardingComplete => string.Equals(OnboardingGateStatus, "Complete", StringComparison.OrdinalIgnoreCase);
}

public interface IWhatsAppFlowProfileSync
{
    Task<WhatsAppFlowProfile?> GetByTenantId(TenantId tenantId, CancellationToken cancellationToken);

    Task<bool> UpdateFlowStatus(TenantId tenantId, string flowId, string status, string? generatedFlowJson, CancellationToken cancellationToken);
}
