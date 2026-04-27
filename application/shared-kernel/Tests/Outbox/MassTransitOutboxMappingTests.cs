using FluentAssertions;
using MassTransit;
using MassTransit.EntityFrameworkCoreIntegration;
using Microsoft.EntityFrameworkCore;
using SharedKernel.ExecutionContext;
using SharedKernel.Tests.TestEntities;
using Xunit;

namespace SharedKernel.Tests.Outbox;

public sealed class MassTransitOutboxMappingTests : IDisposable
{
    private readonly SqliteInMemoryDbContextFactory<TestDbContext> _sqliteInMemoryDbContextFactory;
    private readonly TestDbContext _testDbContext;

    public MassTransitOutboxMappingTests()
    {
        _sqliteInMemoryDbContextFactory = new SqliteInMemoryDbContextFactory<TestDbContext>(new BackgroundWorkerExecutionContext(), TimeProvider.System);
        _testDbContext = _sqliteInMemoryDbContextFactory.CreateContext();
    }

    public void Dispose()
    {
        _sqliteInMemoryDbContextFactory.Dispose();
    }

    [Fact]
    public void SharedKernelDbContext_WhenConfigured_ShouldMapMassTransitOutboxEntities()
    {
        _testDbContext.Model.FindEntityType(typeof(InboxState)).Should().NotBeNull();
        _testDbContext.Model.FindEntityType(typeof(OutboxMessage)).Should().NotBeNull();
        _testDbContext.Model.FindEntityType(typeof(OutboxState)).Should().NotBeNull();
    }
}
