using System.Net.Http.Json;
using SharedKernel.Catalog;
using SharedKernel.Configuration;
using SharedKernel.Outbox;

namespace Account.Features.Catalog;

public static class CatalogOutboxForwarder
{
    private static readonly HashSet<string> SupportedMessageTypes =
    [
        typeof(TenantCatalogUpserted).FullName!,
        typeof(TenantCatalogDeleted).FullName!,
        typeof(UserCatalogUpserted).FullName!,
        typeof(UserCatalogDeleted).FullName!
    ];

    public static IEnumerable<IOutboxMessageHandler> CreateHandlers(IHttpClientFactory httpClientFactory)
    {
        return SupportedMessageTypes.Select(messageType => new TypedCatalogOutboxForwarder(messageType, httpClientFactory));
    }

    private sealed class TypedCatalogOutboxForwarder(string messageType, IHttpClientFactory httpClientFactory) : IOutboxMessageHandler
    {
        public string MessageType => messageType;

        public async Task HandleAsync(OutboxMessage message, CancellationToken cancellationToken)
        {
            var client = httpClientFactory.CreateClient("BackOfficeInternal");
            var envelope = new CatalogEventEnvelope(message.Id, message.Type, message.Payload, message.CreatedAt);
            var response = await client.PostAsJsonAsync("/internal-api/back-office/catalog/events", envelope, SharedDependencyConfiguration.DefaultJsonSerializerOptions, cancellationToken);
            response.EnsureSuccessStatusCode();
        }
    }
}
