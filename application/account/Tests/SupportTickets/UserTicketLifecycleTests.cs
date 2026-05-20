using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Account.Database;
using Account.Features.SupportTickets.Commands;
using Account.Features.SupportTickets.Domain;
using Account.Features.SupportTickets.Queries;
using FluentAssertions;
using SharedKernel.Domain;
using SharedKernel.Tests.Persistence;
using Xunit;

namespace Account.Tests.SupportTickets;

public sealed class UserTicketLifecycleTests : EndpointBaseTest<AccountDbContext>, IClassFixture<AccountWebApplicationFactory>
{
    public UserTicketLifecycleTests(AccountWebApplicationFactory factory) : base(factory)
    {
        Environment.SetEnvironmentVariable("BLOB_STORAGE_URL", "https://test.blob.core.windows.net");
    }

    [Fact]
    public async Task ReplyToTicketAsUser_WhenPostedToOwnTicket_ShouldAppendMessageAndTransitionToAwaitingAgent()
    {
        // Arrange
        var ticketId = await CreateTicketViaApi();
        SetTicketStatus(ticketId, SupportTicketStatus.AwaitingUser);
        TelemetryEventsCollectorSpy.Reset();
        var form = new MultipartFormDataContent
        {
            { new StringContent("Thanks for the update — here is additional info."), "body" },
            { new StringContent("false"), "markAsResolved" }
        };

        // Act
        var response = await AuthenticatedOwnerHttpClient.PostAsync($"/api/account/support-tickets/{ticketId}/reply", form);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var status = Connection.ExecuteScalar<string>("SELECT status FROM support_tickets WHERE id = @id", [new { id = ticketId.ToString() }]);
        status.Should().Be(nameof(SupportTicketStatus.AwaitingAgent));
        TelemetryEventsCollectorSpy.CollectedEvents.Should().Contain(e => e.GetType().Name == "SupportTicketReplyPosted");
    }

    [Fact]
    public async Task ReplyToTicketAsUser_WhenReporterIsAnotherUserInSameTenant_ShouldReturnNotFound()
    {
        // Arrange
        var ticketId = await CreateTicketViaApi(); // Created by Tenant1Owner
        var form = new MultipartFormDataContent
        {
            { new StringContent("I should not be able to reply to someone else's ticket."), "body" },
            { new StringContent("false"), "markAsResolved" }
        };

        // Act
        var response = await AuthenticatedMemberHttpClient.PostAsync($"/api/account/support-tickets/{ticketId}/reply", form);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetTicketDetail_WhenInternalNoteExists_ShouldFilterItOutInResponse()
    {
        // Arrange
        var ticketId = await CreateTicketViaApi();
        SeedInternalNote(ticketId, "Investigating with infra team — do not share.");
        TelemetryEventsCollectorSpy.Reset();

        // Act
        var response = await AuthenticatedOwnerHttpClient.GetAsync($"/api/account/support-tickets/{ticketId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<TicketDetailResponse>();
        payload.Should().NotBeNull();
        payload.Messages.Should().OnlyContain(m => m.AuthorKind != SupportMessageAuthorKind.Internal);
        payload.Messages.Should().NotContain(m => m.Body.Contains("Investigating with infra team"));
    }

    [Fact]
    public async Task MarkResolvedByUser_WhenCalledOnAwaitingTicket_ShouldTransitionToResolved()
    {
        // Arrange
        var ticketId = await CreateTicketViaApi();
        TelemetryEventsCollectorSpy.Reset();

        // Act
        var response = await AuthenticatedOwnerHttpClient.PostAsync($"/api/account/support-tickets/{ticketId}/mark-resolved", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var status = Connection.ExecuteScalar<string>("SELECT status FROM support_tickets WHERE id = @id", [new { id = ticketId.ToString() }]);
        status.Should().Be(nameof(SupportTicketStatus.Resolved));
    }

    [Fact]
    public async Task CloseTicketByUser_WhenSubmittedWithCsat_ShouldPersistCsatAndCloseTicket()
    {
        // Arrange
        var ticketId = await CreateTicketViaApi();
        SetTicketStatus(ticketId, SupportTicketStatus.Resolved);
        TelemetryEventsCollectorSpy.Reset();
        var command = new CloseTicketByUserCommand { CsatScore = SupportTicketCsatScore.Helpful, CsatComment = "Quick and clear." };

        // Act
        var response = await AuthenticatedOwnerHttpClient.PostAsJsonAsync($"/api/account/support-tickets/{ticketId}/close", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var csatJson = Connection.ExecuteScalar<string>("SELECT csat FROM support_tickets WHERE id = @id", [new { id = ticketId.ToString() }]);
        csatJson.Should().NotBeNullOrEmpty();
        csatJson.Should().Contain("Helpful");
        var status = Connection.ExecuteScalar<string>("SELECT status FROM support_tickets WHERE id = @id", [new { id = ticketId.ToString() }]);
        status.Should().Be(nameof(SupportTicketStatus.Closed));
        TelemetryEventsCollectorSpy.CollectedEvents.Should().Contain(e => e.GetType().Name == "SupportTicketCsatSubmitted");
    }

    [Fact]
    public async Task ReopenTicket_WhenClosed_ShouldTransitionToAwaitingAgentAndPreserveCsat()
    {
        // Arrange
        var ticketId = await CreateTicketViaApi();
        SetTicketStatus(ticketId, SupportTicketStatus.Resolved);
        var closeCommand = new CloseTicketByUserCommand { CsatScore = SupportTicketCsatScore.Ok };
        var closeResponse = await AuthenticatedOwnerHttpClient.PostAsJsonAsync($"/api/account/support-tickets/{ticketId}/close", closeCommand);
        closeResponse.EnsureSuccessStatusCode();
        TelemetryEventsCollectorSpy.Reset();

        // Act
        var response = await AuthenticatedOwnerHttpClient.PostAsync($"/api/account/support-tickets/{ticketId}/reopen", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var status = Connection.ExecuteScalar<string>("SELECT status FROM support_tickets WHERE id = @id", [new { id = ticketId.ToString() }]);
        status.Should().Be(nameof(SupportTicketStatus.AwaitingAgent));
        var csatJson = Connection.ExecuteScalar<string>("SELECT csat FROM support_tickets WHERE id = @id", [new { id = ticketId.ToString() }]);
        csatJson.Should().Contain("Ok");
    }

    [Fact]
    public async Task GetMyTickets_WhenCalled_ShouldOnlyReturnReportersOwnTickets()
    {
        // Arrange
        await CreateTicketViaApi(AuthenticatedOwnerHttpClient);
        await CreateTicketViaApi(AuthenticatedMemberHttpClient);

        // Act
        var response = await AuthenticatedOwnerHttpClient.GetAsync("/api/account/support-tickets");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<MyTicketsResponse>();
        payload.Should().NotBeNull();
        payload.Active.Should().OnlyContain(t => t.ShortDisplayId.Length == 6);
        var rowsForOwner = Connection.ExecuteScalar<long>(
            "SELECT COUNT(*) FROM support_tickets WHERE reporter_id = @reporterId",
            [new { reporterId = DatabaseSeeder.Tenant1Owner.Id.ToString() }]
        );
        payload.Active.Length.Should().Be((int)rowsForOwner);
    }

    [Fact]
    public async Task GetTicketDetail_WhenTicketBelongsToAnotherTenant_ShouldReturnNotFound()
    {
        // Arrange
        var otherTenantId = SeedOtherTenant();
        var otherTicketId = SeedOtherTenantTicket(otherTenantId);

        // Act
        var response = await AuthenticatedOwnerHttpClient.GetAsync($"/api/account/support-tickets/{otherTicketId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ReplyToTicketAsUser_WhenTicketBelongsToAnotherTenant_ShouldReturnNotFound()
    {
        // Arrange
        var otherTenantId = SeedOtherTenant();
        var otherTicketId = SeedOtherTenantTicket(otherTenantId);
        var form = new MultipartFormDataContent
        {
            { new StringContent("Cross-tenant reply attempt — should be invisible."), "body" },
            { new StringContent("false"), "markAsResolved" }
        };

        // Act
        var response = await AuthenticatedOwnerHttpClient.PostAsync($"/api/account/support-tickets/{otherTicketId}/reply", form);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private long SeedOtherTenant()
    {
        var otherTenantId = DatabaseSeeder.Tenant1.Id.Value + 9999;
        var now = DateTimeOffset.UtcNow;
        Connection.Insert("tenants", [
                ("id", otherTenantId),
                ("created_at", now),
                ("modified_at", null),
                ("deleted_at", null),
                ("name", "Other Tenant"),
                ("state", "Active"),
                ("plan", "Basis"),
                ("suspension_reason", null),
                ("suspended_at", null),
                ("logo", "{}"),
                ("rollout_bucket", 0),
                ("ab_inclusion_pin", null)
            ]
        );
        return otherTenantId;
    }

    private SupportTicketId SeedOtherTenantTicket(long otherTenantId)
    {
        var id = SupportTicketId.NewId();
        var now = DateTimeOffset.UtcNow;
        Connection.Insert("support_tickets", [
                ("tenant_id", otherTenantId),
                ("id", id.ToString()),
                ("created_at", now.AddMinutes(-10)),
                ("modified_at", null),
                ("short_display_id", "OTHER1"),
                ("reporter_id", UserId.NewId().ToString()),
                ("reporter_role_snapshot", "Owner"),
                ("reporter_email_snapshot", "other@tenant.example"),
                ("subject", "Other tenant's ticket"),
                ("category", nameof(SupportTicketCategory.Other)),
                ("status", nameof(SupportTicketStatus.New)),
                ("assignee", null),
                ("last_activity_at", now),
                ("resolved_at", null),
                ("closed_at", null),
                ("csat", null),
                ("messages", "[]"),
                ("history_events", "[]")
            ]
        );
        return id;
    }

    private async Task<SupportTicketId> CreateTicketViaApi(HttpClient? client = null)
    {
        var form = new MultipartFormDataContent
        {
            { new StringContent("Subject for lifecycle test"), "subject" },
            { new StringContent("Body for the lifecycle test that exceeds the minimum length."), "body" },
            { new StringContent(nameof(SupportTicketCategory.Account)), "category" }
        };
        var response = await (client ?? AuthenticatedOwnerHttpClient).PostAsync("/api/account/support-tickets", form);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<SupportTicketId>())!;
    }

    private void SetTicketStatus(SupportTicketId ticketId, SupportTicketStatus status)
    {
        Connection.Update("support_tickets", "id", ticketId.ToString(), [("status", status.ToString())]);
    }

    private void SeedInternalNote(SupportTicketId ticketId, string body)
    {
        var note = new SupportMessage(SupportMessageId.NewId(), "staff-oid", SupportMessageAuthorKind.Internal, "Support Staff", body, [], DateTimeOffset.UtcNow);
        var existingJson = Connection.ExecuteScalar<string>("SELECT messages FROM support_tickets WHERE id = @id", [new { id = ticketId.ToString() }]);
        var existing = JsonSerializer.Deserialize<SupportMessage[]>(existingJson) ?? [];
        var combined = existing.Append(note).ToArray();
        Connection.Update("support_tickets", "id", ticketId.ToString(), [("messages", JsonSerializer.Serialize(combined))]);
    }
}
