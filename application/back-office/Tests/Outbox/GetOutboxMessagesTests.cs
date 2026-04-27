using System.Net;
using BackOffice.Database;
using BackOffice.Features.Outbox.Queries;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel.Outbox;
using SharedKernel.Tests;
using Xunit;

namespace BackOffice.Tests.Outbox;

public sealed class GetOutboxMessagesTests : EndpointBaseTest<BackOfficeDbContext>
{
    [Fact]
    public async Task GetOutboxMessages_WhenUserIsNotInternal_ShouldReturnForbidden()
    {
        var response = await AuthenticatedOwnerHttpClient.GetAsync("/api/back-office/outbox/messages");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetOutboxMessages_WhenSysOp_ShouldReturnOutboxHealth()
    {
        var pendingMessage = OutboxMessage.Create(typeof(TestOutboxEvent).FullName!, """{"name":"pending"}""", TimeProvider.GetUtcNow());
        var failedMessage = OutboxMessage.Create(typeof(TestOutboxEvent).FullName!, """{"name":"failed"}""", TimeProvider.GetUtcNow());
        failedMessage.MarkDeadLettered("Transport unavailable", TimeProvider.GetUtcNow());

        using (var scope = Provider.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<BackOfficeDbContext>();
            dbContext.OutboxMessages.Add(pendingMessage);
            dbContext.OutboxMessages.Add(failedMessage);
            await dbContext.SaveChangesAsync();
        }

        var response = await AuthenticatedSysOpHttpClient.GetAsync("/api/back-office/outbox/messages");

        response.ShouldBeSuccessfulGetRequest();
        var outboxMessagesResponse = await response.DeserializeResponse<OutboxMessagesResponse>();
        outboxMessagesResponse!.Messages.Should().Contain(m => m.Id == pendingMessage.Id && m.Status == OutboxMessageStatus.Pending);
        outboxMessagesResponse.Messages.Should().Contain(m => m.Id == failedMessage.Id && m.Status == OutboxMessageStatus.DeadLettered);
    }

    private sealed record TestOutboxEvent(string Name);
}
