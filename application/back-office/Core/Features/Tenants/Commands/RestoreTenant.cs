using System.Net;
using JetBrains.Annotations;
using SharedKernel.Cqrs;
using SharedKernel.Domain;

namespace BackOffice.Features.Tenants.Commands;

[PublicAPI]
public sealed record RestoreTenantCommand(TenantId Id) : ICommand, IRequest<Result>;

public sealed class RestoreTenantHandler(IHttpClientFactory httpClientFactory)
    : IRequestHandler<RestoreTenantCommand, Result>
{
    public async Task<Result> Handle(RestoreTenantCommand command, CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient("AccountInternal");
        var response = await client.PostAsync($"/internal-api/account/tenants/{command.Id}/restore", null, cancellationToken);

        return response.StatusCode switch
        {
            HttpStatusCode.OK or HttpStatusCode.NoContent => Result.Success(),
            HttpStatusCode.NotFound => Result.NotFound($"Deleted tenant with id '{command.Id}' not found."),
            _ => Result.BadRequest($"Failed to restore tenant '{command.Id}'.")
        };
    }
}
