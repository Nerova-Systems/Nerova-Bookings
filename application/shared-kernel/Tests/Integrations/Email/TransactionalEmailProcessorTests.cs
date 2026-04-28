using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using SharedKernel.ExecutionContext;
using SharedKernel.Integrations.Email;
using SharedKernel.Tests.TestEntities;
using Xunit;

namespace SharedKernel.Tests.Integrations.Email;

public sealed class TransactionalEmailProcessorTests : IDisposable
{
    private readonly SqliteInMemoryDbContextFactory<TestDbContext> _dbContextFactory;
    private readonly IEmailClient _emailClient = Substitute.For<IEmailClient>();
    private readonly TimeProvider _timeProvider = TimeProvider.System;

    public TransactionalEmailProcessorTests()
    {
        _dbContextFactory = new SqliteInMemoryDbContextFactory<TestDbContext>(new BackgroundWorkerExecutionContext(), _timeProvider);
    }

    public void Dispose()
    {
        _dbContextFactory.Dispose();
    }

    [Fact]
    public async Task EnqueueAsync_ShouldPersistPendingEmailMessage()
    {
        await using var dbContext = _dbContextFactory.CreateContext();
        var queue = new TransactionalEmailQueue<TestDbContext>(dbContext, _timeProvider);

        await queue.EnqueueAsync(
            "customer@example.com",
            "Payment failed",
            "<p>Please update your card.</p>",
            TransactionalEmailTemplateKeys.PaymentFailed,
            "subscription-1",
            CancellationToken.None
        );
        await dbContext.SaveChangesAsync(CancellationToken.None);

        var message = await dbContext.Set<TransactionalEmailMessage>().SingleAsync(CancellationToken.None);
        message.Recipient.Should().Be("customer@example.com");
        message.Subject.Should().Be("Payment failed");
        message.TemplateKey.Should().Be(TransactionalEmailTemplateKeys.PaymentFailed);
        message.Status.Should().Be(TransactionalEmailStatus.Pending);
        message.Attempts.Should().Be(0);
    }

    [Fact]
    public async Task ProcessDueMessagesAsync_WhenEmailSends_ShouldMarkMessageSent()
    {
        await using (var dbContext = _dbContextFactory.CreateContext())
        {
            dbContext.Set<TransactionalEmailMessage>().Add(
                TransactionalEmailMessage.Create(
                    "customer@example.com",
                    "Payment failed",
                    "<p>Please update your card.</p>",
                    TransactionalEmailTemplateKeys.PaymentFailed,
                    "subscription-1",
                    _timeProvider.GetUtcNow()
                )
            );
            await dbContext.SaveChangesAsync(CancellationToken.None);
        }

        var processor = CreateProcessor();
        await processor.ProcessDueMessagesAsync(CancellationToken.None);

        await using var savedContext = _dbContextFactory.CreateContext();
        var message = await savedContext.Set<TransactionalEmailMessage>().SingleAsync(CancellationToken.None);
        message.Status.Should().Be(TransactionalEmailStatus.Sent);
        message.SentAt.Should().NotBeNull();
        message.Attempts.Should().Be(1);
        await _emailClient.Received(1).SendAsync("customer@example.com", "Payment failed", "<p>Please update your card.</p>", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessDueMessagesAsync_WhenEmailSendFails_ShouldStoreErrorAndScheduleRetry()
    {
        await using (var dbContext = _dbContextFactory.CreateContext())
        {
            dbContext.Set<TransactionalEmailMessage>().Add(
                TransactionalEmailMessage.Create(
                    "customer@example.com",
                    "Payment failed",
                    "<p>Please update your card.</p>",
                    TransactionalEmailTemplateKeys.PaymentFailed,
                    "subscription-1",
                    _timeProvider.GetUtcNow()
                )
            );
            await dbContext.SaveChangesAsync(CancellationToken.None);
        }
        _emailClient.SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("SMTP unavailable")));

        var processor = CreateProcessor();
        await processor.ProcessDueMessagesAsync(CancellationToken.None);

        await using var savedContext = _dbContextFactory.CreateContext();
        var message = await savedContext.Set<TransactionalEmailMessage>().SingleAsync(CancellationToken.None);
        message.Status.Should().Be(TransactionalEmailStatus.Pending);
        message.Attempts.Should().Be(1);
        message.LastError.Should().Contain("SMTP unavailable");
        message.NextAttemptAt.Should().BeAfter(_timeProvider.GetUtcNow());
    }

    private TransactionalEmailProcessor<TestDbContext> CreateProcessor()
    {
        var services = new ServiceCollection();
        services.AddSingleton(_timeProvider);
        services.AddDbContext<TestDbContext>(options => options.UseSqlite(_dbContextFactory.Connection).UseSnakeCaseNamingConvention());
        services.AddScoped<IExecutionContext, BackgroundWorkerExecutionContext>();
        services.AddScoped(_ => _emailClient);

        var serviceProvider = services.BuildServiceProvider();
        return new TransactionalEmailProcessor<TestDbContext>(
            serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<TransactionalEmailProcessor<TestDbContext>>.Instance
        );
    }
}
