using Account.Features.WhatsApp.Domain;
using JetBrains.Annotations;
using SharedKernel.Cqrs;

namespace Account.Features.WhatsApp.Queries;

[PublicAPI]
public sealed record GetWhatsAppFlowProfileByPhoneNumberIdQuery(string PhoneNumberId) : IRequest<WhatsAppFlowProfileDto?>;

public sealed class GetWhatsAppFlowProfileByPhoneNumberIdHandler(IWabaConfigurationRepository repository)
    : IRequestHandler<GetWhatsAppFlowProfileByPhoneNumberIdQuery, WhatsAppFlowProfileDto?>
{
    public async Task<WhatsAppFlowProfileDto?> Handle(GetWhatsAppFlowProfileByPhoneNumberIdQuery request, CancellationToken cancellationToken)
    {
        var config = await repository.GetByPhoneNumberIdAsync(request.PhoneNumberId, cancellationToken);
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
