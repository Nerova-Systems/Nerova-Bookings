using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SharedKernel.ExecutionContext;
using SharedKernel.Outbox;
using SharedKernel.Persistence;
using SharedKernel.Tests.TestEntities;
using Xunit;

namespace SharedKernel.Tests.Outbox;

public sealed class OutboxPublisherTests : IDisposable
{
    private readonly SqliteInMemoryDbContextFactory<TestDbContext> _sqliteInMemoryDbContextFactory;
    private readonly TestDbContext _testDbContext;

    public OutboxPublisherTests()
    {
        _sqliteInMemoryDbContextFactory = new SqliteInMemoryDbContextFactory<TestDbContext>(new BackgroundWorkerExecutionContext(), TimeProvider.System);
        _testDbContext = _sqliteInMemoryDbContextFactory.CreateContext();
    }

    public void Dispose()
    {
        _sqliteInMemoryDbContextFactory.Dispose();
    }

    [Fact]
    public async Task EnqueueAsync_WhenUnitOfWorkCommits_ShouldSaveMessageWithAggregateChanges()
    {
        var outboxPublisher = new OutboxPublisher(_testDbContext, TimeProvider.System);
        var unitOfWork = new UnitOfWork(_testDbContext);
        var aggregate = TestAggregate.Create("Created through command");
        _ = aggregate.GetAndClearDomainEvents();

        _testDbContext.TestAggregates.Add(aggregate);
        await outboxPublisher.EnqueueAsync(new TestOutboxEvent(aggregate.Id.ToString()!, aggregate.Name), CancellationToken.None);
        await unitOfWork.CommitAsync(CancellationToken.None);

        var savedAggregate = await _testDbContext.TestAggregates.SingleAsync(CancellationToken.None);
        var savedMessage = await _testDbContext.OutboxMessages.SingleAsync(CancellationToken.None);
        savedAggregate.Name.Should().Be("Created through command");
        savedMessage.Type.Should().Be(typeof(TestOutboxEvent).FullName);
        savedMessage.ProcessedAt.Should().BeNull();
    }

    private sealed record TestOutboxEvent(string AggregateId, string Name);
}
