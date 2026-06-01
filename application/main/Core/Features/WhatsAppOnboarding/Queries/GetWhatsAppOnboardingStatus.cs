using JetBrains.Annotations;
using Main.Features.WhatsAppOnboarding.Domain;
using SharedKernel.Cqrs;

namespace Main.Features.WhatsAppOnboarding.Queries;

[PublicAPI]
public sealed record GetWhatsAppOnboardingStatusQuery : IRequest<Result<GetWhatsAppOnboardingStatusResponse>>;

[PublicAPI]
public sealed record GetWhatsAppOnboardingStatusResponse(bool IsConnected, string? BusinessName, string? PhoneNumber, string Status);

public sealed class GetWhatsAppOnboardingStatusHandler(IWhatsAppBusinessAccountRepository whatsAppBusinessAccountRepository)
    : IRequestHandler<GetWhatsAppOnboardingStatusQuery, Result<GetWhatsAppOnboardingStatusResponse>>
{
    public async Task<Result<GetWhatsAppOnboardingStatusResponse>> Handle(GetWhatsAppOnboardingStatusQuery query, CancellationToken cancellationToken)
    {
        var account = await whatsAppBusinessAccountRepository.GetByTenantAsync(cancellationToken);
        if (account is null)
        {
            return new GetWhatsAppOnboardingStatusResponse(false, null, null, nameof(WhatsAppBusinessAccountStatus.NotConnected));
        }

        return new GetWhatsAppOnboardingStatusResponse(true, account.BusinessName, account.PhoneNumber.DisplayPhoneNumber, account.Status.ToString());
    }
}
