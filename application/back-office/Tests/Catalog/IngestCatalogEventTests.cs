using System.Net.Http.Json;
using System.Text.Json;
using BackOffice.Database;
using BackOffice.Features.Catalog.Commands;
using BackOffice.Features.Catalog.Domain;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel.Catalog;
using SharedKernel.Configuration;
using SharedKernel.Domain;
using SharedKernel.Tests;
using Xunit;

namespace BackOffice.Tests.Catalog;

public sealed class IngestCatalogEventTests : EndpointBaseTest<BackOfficeDbContext>
{
    [Fact]
    public async Task IngestCatalogEvent_WhenDuplicateEventIsReceived_ShouldProcessItOnce()
    {
        var sourceEventId = Guid.NewGuid();
        var tenantId = TenantId.NewId();
        var catalogEvent = new TenantCatalogUpserted(tenantId, "Acme", "Active", "Trial", null, TimeProvider.GetUtcNow(), null);
        var envelope = new CatalogEventEnvelope(
            sourceEventId,
            typeof(TenantCatalogUpserted).FullName!,
            JsonSerializer.Serialize(catalogEvent, SharedDependencyConfiguration.DefaultJsonSerializerOptions),
            TimeProvider.GetUtcNow()
        );

        using var scope = Provider.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var firstResponse = await mediator.Send(new IngestCatalogEventCommand(envelope.SourceEventId, envelope.Type, envelope.Payload, envelope.OccurredAt));
        var secondResponse = await mediator.Send(new IngestCatalogEventCommand(envelope.SourceEventId, envelope.Type, envelope.Payload, envelope.OccurredAt));

        firstResponse.IsSuccess.Should().BeTrue();
        secondResponse.IsSuccess.Should().BeTrue();

        var dbContext = scope.ServiceProvider.GetRequiredService<BackOfficeDbContext>();
        (await dbContext.Set<CatalogTenant>().CountAsync(t => t.Id == tenantId)).Should().Be(1);
        (await dbContext.Set<ProcessedCatalogEvent>().CountAsync(e => e.Id == sourceEventId)).Should().Be(1);
    }
}
