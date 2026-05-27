using Account.Features.WhatsApp.Domain;
using JetBrains.Annotations;
using SharedKernel.Cqrs;
using SharedKernel.Domain;

namespace Account.Features.WhatsApp.Commands;

[PublicAPI]
public sealed record LinkWabaAccountCommand(TenantId TenantId, string WabaId, string PhoneNumberId, string DisplayPhoneNumber)
    : ICommand, IRequest<Result>;

public sealed class LinkWabaAccountHandler(IWabaConfigurationRepository repository)
    : IRequestHandler<LinkWabaAccountCommand, Result>
{
    public async Task<Result> Handle(LinkWabaAccountCommand command, CancellationToken cancellationToken)
    {
        var existing = await repository.GetByTenantIdAsync(command.TenantId, cancellationToken);

        if (existing is null)
        {
            var config = WabaConfiguration.Create(
                command.TenantId,
                command.WabaId,
                command.PhoneNumberId,
                command.DisplayPhoneNumber
            );
            await repository.AddAsync(config, cancellationToken);
        }
        else
        {
            existing.LinkWaba(command.WabaId, command.PhoneNumberId, command.DisplayPhoneNumber);
            repository.Update(existing);
        }

        return Result.Success();
    }
}
