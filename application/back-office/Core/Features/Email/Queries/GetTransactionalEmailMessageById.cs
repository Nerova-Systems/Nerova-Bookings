using System.Net;
using System.Net.Http.Json;
using JetBrains.Annotations;
using SharedKernel.Cqrs;
using SharedKernel.Integrations.Email;

namespace BackOffice.Features.Email.Queries;

[PublicAPI]
public sealed record GetTransactionalEmailMessageByIdQuery(Guid Id) : IRequest<Result<TransactionalEmailMessageResponse>>;

public sealed class GetTransactionalEmailMessageByIdHandler(IHttpClientFactory httpClientFactory)
    : IRequestHandler<GetTransactionalEmailMessageByIdQuery, Result<TransactionalEmailMessageResponse>>
{
    public async Task<Result<TransactionalEmailMessageResponse>> Handle(GetTransactionalEmailMessageByIdQuery query, CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient("AccountInternal");
        var response = await client.GetAsync($"/internal-api/account/email/messages/{query.Id}", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return Result<TransactionalEmailMessageResponse>.NotFound($"Transactional email message '{query.Id}' was not found.");
        }

        if (!response.IsSuccessStatusCode)
        {
            return Result<TransactionalEmailMessageResponse>.BadRequest("Failed to load transactional email message from Account.");
        }

        var message = await response.Content.ReadFromJsonAsync<TransactionalEmailMessageResponse>(cancellationToken);
        return message is null
            ? Result<TransactionalEmailMessageResponse>.NotFound($"Transactional email message '{query.Id}' was not found.")
            : message;
    }
}
