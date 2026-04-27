using BackOffice.Features.Catalog.Commands;
using SharedKernel.ApiResults;
using SharedKernel.Catalog;
using SharedKernel.Endpoints;

namespace BackOffice.Api.Endpoints;

public sealed class CatalogEndpoints : IEndpoints
{
    public void MapEndpoints(IEndpointRouteBuilder routes)
    {
        routes.MapPost("/internal-api/back-office/catalog/events", async Task<ApiResult> (CatalogEventEnvelope envelope, IMediator mediator)
            => await mediator.Send(new IngestCatalogEventCommand(envelope.SourceEventId, envelope.Type, envelope.Payload, envelope.OccurredAt))
        );
    }
}
