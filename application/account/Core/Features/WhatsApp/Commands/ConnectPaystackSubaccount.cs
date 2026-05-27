using Account.Features.WhatsApp.Domain;
using Account.Features.WhatsApp.Infrastructure;
using JetBrains.Annotations;
using SharedKernel.Cqrs;
using SharedKernel.Domain;

namespace Account.Features.WhatsApp.Commands;

[PublicAPI]
public sealed record ConnectPaystackSubaccountCommand(
    TenantId TenantId,
    string BusinessName,
    string BankCode,
    string AccountNumber,
    decimal PercentageFee
) : ICommand, IRequest<Result<ConnectPaystackSubaccountResponse>>;

[PublicAPI]
public sealed record ConnectPaystackSubaccountResponse(string SubaccountCode);

public sealed class ConnectPaystackSubaccountHandler(
    IWabaConfigurationRepository repository,
    IPaystackSubaccountService paystackSubaccountService
) : IRequestHandler<ConnectPaystackSubaccountCommand, Result<ConnectPaystackSubaccountResponse>>
{
    public async Task<Result<ConnectPaystackSubaccountResponse>> Handle(
        ConnectPaystackSubaccountCommand command,
        CancellationToken cancellationToken)
    {
        var config = await repository.GetByTenantIdAsync(command.TenantId, cancellationToken);
        if (config is null)
        {
            return Result<ConnectPaystackSubaccountResponse>.NotFound("WhatsApp configuration not found for this tenant.");
        }

        var createResult = await paystackSubaccountService.CreateSubaccount(
            command.BusinessName,
            command.BankCode,
            command.AccountNumber,
            command.PercentageFee,
            cancellationToken
        );

        if (!createResult.IsSuccess)
        {
            return Result<ConnectPaystackSubaccountResponse>.From(createResult);
        }

        config.SetSubaccountCode(createResult.Value!);
        repository.Update(config);

        return new ConnectPaystackSubaccountResponse(createResult.Value!);
    }
}
