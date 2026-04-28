using System.Net.Http.Json;
using JetBrains.Annotations;
using SharedKernel.Cqrs;
using SharedKernel.Integrations.Email;

namespace BackOffice.Features.Email.Queries;

[PublicAPI]
public sealed record GetTransactionalEmailMessagesQuery(TransactionalEmailStatus? Status = null, int PageOffset = 0, int PageSize = 50)
    : IRequest<Result<TransactionalEmailMessagesResponse>>;

public sealed class GetTransactionalEmailMessagesHandler(IHttpClientFactory httpClientFactory)
    : IRequestHandler<GetTransactionalEmailMessagesQuery, Result<TransactionalEmailMessagesResponse>>
{
    public async Task<Result<TransactionalEmailMessagesResponse>> Handle(GetTransactionalEmailMessagesQuery query, CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient("AccountInternal");
        var queryString = $"?pageOffset={Math.Max(query.PageOffset, 0)}&pageSize={Math.Clamp(query.PageSize, 1, 100)}";
        if (query.Status is not null)
        {
            queryString += $"&status={query.Status}";
        }

        var response = await client.GetAsync($"/internal-api/account/email/messages{queryString}", cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return Result<TransactionalEmailMessagesResponse>.BadRequest("Failed to load transactional email messages from Account.");
        }

        var messages = await response.Content.ReadFromJsonAsync<TransactionalEmailMessagesResponse>(cancellationToken);
        return messages ?? new TransactionalEmailMessagesResponse(0, []);
    }
}
