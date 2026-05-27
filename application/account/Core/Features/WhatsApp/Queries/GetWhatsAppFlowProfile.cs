using Account.Features.WhatsApp.Domain;
using JetBrains.Annotations;
using SharedKernel.Cqrs;
using SharedKernel.Domain;

namespace Account.Features.WhatsApp.Queries;

[PublicAPI]
public sealed record WhatsAppFlowProfileDto(
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
    string? PublicKeyFingerprint,
    string? PaystackSubaccountCode
);

[PublicAPI]
public sealed record GetWhatsAppFlowProfileQuery(TenantId TenantId) : IRequest<WhatsAppFlowProfileDto?>;

public sealed class GetWhatsAppFlowProfileHandler(IWabaConfigurationRepository repository)
    : IRequestHandler<GetWhatsAppFlowProfileQuery, WhatsAppFlowProfileDto?>
{
    public async Task<WhatsAppFlowProfileDto?> Handle(GetWhatsAppFlowProfileQuery request, CancellationToken cancellationToken)
    {
        var config = await repository.GetByTenantIdAsync(request.TenantId, cancellationToken);
        if (config is null) return null;
        return new WhatsAppFlowProfileDto(
            config.TenantId,
            config.WabaId,
            config.PhoneNumberId,
            config.DisplayPhoneNumber,
            config.FlowId,
            config.FlowStatus.ToString(),
            config.OnboardingGateStatus.ToString(),
            config.WabaAccessToken,
            config.EncryptedPrivateKey,
            config.PrivateKeyIv,
            config.PublicKeyFingerprint,
            config.SubaccountCode
        );
    }
}
