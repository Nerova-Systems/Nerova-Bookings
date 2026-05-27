using Account.Features.WhatsApp.Domain;
using JetBrains.Annotations;
using SharedKernel.Domain;

namespace Account.Features.WhatsApp.Queries;

[PublicAPI]
public sealed record GetWabaOnboardingStatusQuery(TenantId TenantId) : IRequest<WabaOnboardingStatusResponse?>;

[PublicAPI]
public sealed record WabaOnboardingStatusResponse(
    WabaOnboardingStatus Status,
    bool WabaLinked,
    bool PhoneRegistered,
    bool KeyPairGenerated,
    bool PaystackConnected,
    string? DisplayPhoneNumber,
    string? PublicKeyFingerprint,
    bool CanPublishFlow
);

public sealed class GetWabaOnboardingStatusHandler(IWabaConfigurationRepository repository)
    : IRequestHandler<GetWabaOnboardingStatusQuery, WabaOnboardingStatusResponse?>
{
    public async Task<WabaOnboardingStatusResponse?> Handle(GetWabaOnboardingStatusQuery query, CancellationToken cancellationToken)
    {
        var config = await repository.GetByTenantIdAsync(query.TenantId, cancellationToken);
        if (config is null)
        {
            return null;
        }

        var status = config.OnboardingGateStatus;

        return new WabaOnboardingStatusResponse(
            Status: status,
            WabaLinked: config.WabaId != string.Empty && config.PhoneNumberId is not null,
            PhoneRegistered: config.PhoneNumberId is not null,
            KeyPairGenerated: config.EncryptedPrivateKey is not null && config.PublicKeyFingerprint is not null,
            PaystackConnected: config.SubaccountCode is not null,
            DisplayPhoneNumber: config.DisplayPhoneNumber,
            PublicKeyFingerprint: config.PublicKeyFingerprint,
            CanPublishFlow: status == WabaOnboardingStatus.Complete
        );
    }
}
