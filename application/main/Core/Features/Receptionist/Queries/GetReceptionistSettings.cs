using JetBrains.Annotations;
using Main.Features.Receptionist.Domain;
using SharedKernel.Cqrs;
using SharedKernel.ExecutionContext;

namespace Main.Features.Receptionist.Queries;

[PublicAPI]
public sealed record ReceptionistSettingsResponse(bool IsEnabled, ReceptionistTone Tone, string[] Languages, string? FaqNotes, string? OwnerPhoneNumber);

[PublicAPI]
public sealed record GetReceptionistSettingsQuery : IRequest<Result<ReceptionistSettingsResponse>>;

public sealed class GetReceptionistSettingsHandler(IReceptionistSettingsRepository receptionistSettingsRepository, IExecutionContext executionContext)
    : IRequestHandler<GetReceptionistSettingsQuery, Result<ReceptionistSettingsResponse>>
{
    public async Task<Result<ReceptionistSettingsResponse>> Handle(GetReceptionistSettingsQuery query, CancellationToken cancellationToken)
    {
        if (executionContext.TenantId is null)
        {
            return Result<ReceptionistSettingsResponse>.Unauthorized("Authentication is required.");
        }

        var settings = await receptionistSettingsRepository.GetByTenantAsync(cancellationToken);
        if (settings is null)
        {
            return Result<ReceptionistSettingsResponse>.Success(new ReceptionistSettingsResponse(false, ReceptionistTone.Friendly, ["English"], null, null));
        }

        return Result<ReceptionistSettingsResponse>.Success(new ReceptionistSettingsResponse(settings.IsEnabled, settings.Tone, [.. settings.Languages], settings.FaqNotes, settings.OwnerPhoneNumber));
    }
}
