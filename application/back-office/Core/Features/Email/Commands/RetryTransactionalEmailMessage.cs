using System.Net;
using JetBrains.Annotations;
using SharedKernel.Cqrs;

namespace BackOffice.Features.Email.Commands;

[PublicAPI]
public sealed record RetryTransactionalEmailMessageCommand(Guid Id) : ICommand, IRequest<Result>;

public sealed class RetryTransactionalEmailMessageHandler(IHttpClientFactory httpClientFactory)
    : IRequestHandler<RetryTransactionalEmailMessageCommand, Result>
{
    public async Task<Result> Handle(RetryTransactionalEmailMessageCommand command, CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient("AccountInternal");
        var response = await client.PostAsync($"/internal-api/account/email/messages/{command.Id}/retry", null, cancellationToken);

        return response.StatusCode switch
        {
            HttpStatusCode.OK or HttpStatusCode.NoContent => Result.Success(),
            HttpStatusCode.NotFound => Result.NotFound($"Transactional email message '{command.Id}' was not found."),
            HttpStatusCode.BadRequest => Result.BadRequest("Transactional email message cannot be retried."),
            _ => Result.BadRequest("Failed to retry transactional email message in Account.")
        };
    }
}
