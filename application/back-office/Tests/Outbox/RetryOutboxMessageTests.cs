using BackOffice.Database;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel.Outbox;
using SharedKernel.Tests;
using Xunit;

namespace BackOffice.Tests.Outbox;

public sealed class RetryOutboxMessageTests : EndpointBaseTest<BackOfficeDbContext>
{
    [Fact]
    public async Task RetryOutboxMessage_WhenMessageIsDeadLettered_ShouldMakeMessageEligibleForProcessing()
    {
        var message = OutboxMessage.Create(typeof(TestOutboxEvent).FullName!, """{"name":"failed"}""", TimeProvider.GetUtcNow());
        message.MarkDeadLettered("Transport unavailable", TimeProvider.GetUtcNow());

        using (var scope = Provider.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<BackOfficeDbContext>();
            dbContext.OutboxMessages.Add(message);
            await dbContext.SaveChangesAsync();
        }

        var response = await AuthenticatedSysOpHttpClient.PostAsync($"/api/back-office/outbox/messages/{message.Id}/retry", null);

        await response.ShouldBeSuccessfulPostRequest(hasLocation: false);

        using var assertionScope = Provider.CreateScope();
        var savedMessage = await assertionScope.ServiceProvider.GetRequiredService<BackOfficeDbContext>().OutboxMessages.SingleAsync(m => m.Id == message.Id);
        savedMessage.ProcessedAt.Should().BeNull();
        savedMessage.DeadLetteredAt.Should().BeNull();
        savedMessage.LockedUntilAt.Should().BeNull();
        savedMessage.NextAttemptAt.Should().BeOnOrBefore(TimeProvider.GetUtcNow());
    }

    private sealed record TestOutboxEvent(string Name);
}
