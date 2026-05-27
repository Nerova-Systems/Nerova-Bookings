using Account.Features.WhatsApp.Domain;
using Account.Features.WhatsApp.Infrastructure;
using JetBrains.Annotations;
using Microsoft.Extensions.Configuration;
using SharedKernel.Cqrs;
using SharedKernel.Domain;

namespace Account.Features.WhatsApp.Commands;

[PublicAPI]
public sealed record GenerateWabaKeyPairCommand(TenantId TenantId) : ICommand, IRequest<Result<GenerateWabaKeyPairResponse>>;

[PublicAPI]
public sealed record GenerateWabaKeyPairResponse(string PublicKeyPem, string Fingerprint);

public sealed class GenerateWabaKeyPairHandler(
    IWabaConfigurationRepository repository,
    IWabaEncryptionService encryptionService,
    IConfiguration configuration
) : IRequestHandler<GenerateWabaKeyPairCommand, Result<GenerateWabaKeyPairResponse>>
{
    public async Task<Result<GenerateWabaKeyPairResponse>> Handle(GenerateWabaKeyPairCommand command, CancellationToken cancellationToken)
    {
        var config = await repository.GetByTenantIdAsync(command.TenantId, cancellationToken);
        if (config is null)
        {
            return Result<GenerateWabaKeyPairResponse>.NotFound("WhatsApp configuration not found for this tenant.");
        }

        var passphrase = configuration["WhatsApp:EncryptionPassphrase"] ?? string.Empty;
        var keyPair = encryptionService.GenerateKeyPair(passphrase);

        config.SetKeyPair(keyPair.EncryptedPrivateKeyBase64, keyPair.IvBase64, keyPair.Fingerprint);
        repository.Update(config);

        return new GenerateWabaKeyPairResponse(keyPair.PublicKeyPem, keyPair.Fingerprint);
    }
}
