using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using SharedKernel.ExecutionContext;
using SharedKernel.Outbox;
using SharedKernel.Tests.TestEntities;
using Xunit;

namespace SharedKernel.Tests.Outbox;

public sealed class OutboxProcessorTests : IDisposable
{
    private readonly SqliteInMemoryDbContextFactory<TestDbContext> _sqliteInMemoryDbContextFactory;
    private readonly TimeProvider _timeProvider = TimeProvider.System;

    public OutboxProcessorTests()
    {
        _sqliteInMemoryDbContextFactory = new SqliteInMemoryDbContextFactory<TestDbContext>(new BackgroundWorkerExecutionContext(), _timeProvider);
    }

    public void Dispose()
    {
        _sqliteInMemoryDbContextFactory.Dispose();
    }

    [Fact]
    public async Task ProcessDueMessagesAsync_WhenHandlerFails_ShouldRetryLaterAndKeepMessagePending()
    {
        await using var dbContext = _sqliteInMemoryDbContextFactory.CreateContext();
        var message = OutboxMessage.Create(typeof(TestOutboxEvent).FullName!, """{"name":"tenant"}""", _timeProvider.GetUtcNow());
        dbContext.OutboxMessages.Add(message);
        await dbContext.SaveChangesAsync(CancellationToken.None);

        var handler = Substitute.For<IOutboxMessageHandler>();
        handler.MessageType.Returns(typeof(TestOutboxEvent).FullName);
        handler.HandleAsync(Arg.Any<OutboxMessage>(), Arg.Any<CancellationToken>()).Returns(Task.FromException(new InvalidOperationException("Transport unavailable")));

        var processor = CreateProcessor(handler);
        await processor.ProcessDueMessagesAsync(CancellationToken.None);

        var savedMessage = await dbContext.OutboxMessages.SingleAsync(CancellationToken.None);
        savedMessage.ProcessedAt.Should().BeNull();
        savedMessage.Attempts.Should().Be(1);
        savedMessage.LastError.Should().Contain("Transport unavailable");
        savedMessage.NextAttemptAt.Should().BeAfter(_timeProvider.GetUtcNow());
    }

    [Fact]
    public async Task ProcessDueMessagesAsync_WhenDuplicateMessageIsHandledSuccessfully_ShouldMarkBothMessagesProcessed()
    {
        await using var dbContext = _sqliteInMemoryDbContextFactory.CreateContext();
        var messageType = typeof(TestOutboxEvent).FullName!;
        dbContext.OutboxMessages.Add(OutboxMessage.Create(messageType, """{"name":"tenant"}""", _timeProvider.GetUtcNow()));
        dbContext.OutboxMessages.Add(OutboxMessage.Create(messageType, """{"name":"tenant"}""", _timeProvider.GetUtcNow()));
        await dbContext.SaveChangesAsync(CancellationToken.None);

        var handler = Substitute.For<IOutboxMessageHandler>();
        handler.MessageType.Returns(messageType);
        handler.HandleAsync(Arg.Any<OutboxMessage>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        var processor = CreateProcessor(handler);
        await processor.ProcessDueMessagesAsync(CancellationToken.None);

        var savedMessages = await dbContext.OutboxMessages.ToArrayAsync(CancellationToken.None);
        savedMessages.Should().OnlyContain(m => m.ProcessedAt != null);
        await handler.Received(2).HandleAsync(Arg.Any<OutboxMessage>(), Arg.Any<CancellationToken>());
    }

    private OutboxMessageProcessor<TestDbContext> CreateProcessor(params IOutboxMessageHandler[] handlers)
    {
        var services = new ServiceCollection();
        services.AddSingleton(_timeProvider);
        services.AddDbContext<TestDbContext>(options => options.UseSqlite(_sqliteInMemoryDbContextFactory.Connection).UseSnakeCaseNamingConvention());
        services.AddScoped<IExecutionContext, BackgroundWorkerExecutionContext>();
        foreach (var handler in handlers)
        {
            services.AddScoped(_ => handler);
        }

        var serviceProvider = services.BuildServiceProvider();
        return new OutboxMessageProcessor<TestDbContext>(serviceProvider.GetRequiredService<IServiceScopeFactory>(), NullLogger<OutboxMessageProcessor<TestDbContext>>.Instance);
    }

    private sealed record TestOutboxEvent(string Name);
}
