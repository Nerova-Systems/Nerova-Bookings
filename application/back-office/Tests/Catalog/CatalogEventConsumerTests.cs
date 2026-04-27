using BackOffice.Database;
using BackOffice.Features.Catalog.Consumers;
using BackOffice.Features.Catalog.Domain;
using FluentAssertions;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using SharedKernel.Catalog;
using SharedKernel.Domain;
using Xunit;

namespace BackOffice.Tests.Catalog;

public sealed class CatalogEventConsumerTests : EndpointBaseTest<BackOfficeDbContext>
{
    [Fact]
    public async Task TenantCatalogUpsertedConsumer_WhenEventIsReceived_ShouldUpsertCatalogTenant()
    {
        var sourceEventId = Guid.NewGuid();
        var tenantId = TenantId.NewId();
        var catalogEvent = new TenantCatalogUpserted(tenantId, "Acme", "Active", "Trial", null, TimeProvider.GetUtcNow(), null);

        var consumeContext = Substitute.For<ConsumeContext<TenantCatalogUpserted>>();
        consumeContext.Message.Returns(catalogEvent);
        consumeContext.MessageId.Returns(sourceEventId);
        consumeContext.SentTime.Returns(TimeProvider.GetUtcNow().UtcDateTime);
        consumeContext.CancellationToken.Returns(CancellationToken.None);

        using var scope = Provider.CreateScope();
        var consumer = ActivatorUtilities.CreateInstance<TenantCatalogUpsertedConsumer>(scope.ServiceProvider);

        await consumer.Consume(consumeContext);

        var dbContext = scope.ServiceProvider.GetRequiredService<BackOfficeDbContext>();
        var tenant = await dbContext.Set<CatalogTenant>().SingleAsync(t => t.Id == tenantId);
        tenant.Name.Should().Be("Acme");
    }
}
