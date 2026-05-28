using JetBrains.Annotations;
using Main.Features.WhatsAppFlows.Domain;
using SharedKernel.Cqrs;
using SharedKernel.ExecutionContext;

namespace Main.Features.WhatsAppFlows.Commands;

[PublicAPI]
public sealed record RemoveCustomQuestionCommand(int Order) : ICommand, IRequest<Result>;

public sealed class RemoveCustomQuestionHandler(
    ITenantFlowConfigRepository repository,
    IExecutionContext executionContext
) : IRequestHandler<RemoveCustomQuestionCommand, Result>
{
    public async Task<Result> Handle(RemoveCustomQuestionCommand command, CancellationToken cancellationToken)
    {
        var tenantId = executionContext.TenantId;
        if (tenantId is null) return Result.Unauthorized("Authentication is required.");

        var config = await repository.GetByTenantIdAsync(tenantId, cancellationToken);
        if (config is null) return Result.NotFound("Tenant flow configuration has not been created yet.");

        if (!config.RemoveCustomQuestion(command.Order))
        {
            return Result.NotFound($"No custom question with order {command.Order} exists.");
        }

        repository.Update(config);
        return Result.Success();
    }
}
