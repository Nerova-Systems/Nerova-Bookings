using System.Net;
using System.Net.Http.Json;
using Account.Features.SupportTickets.BackOffice.Commands;
using Account.Features.SupportTickets.BackOffice.Queries;
using Account.Features.SupportTickets.Domain;
using Account.Features.Users.Domain;
using Account.Tests.BackOffice;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;
using SharedKernel.Authentication.MockEasyAuth;
using SharedKernel.Domain;
using SharedKernel.Emails;
using SharedKernel.Integrations.Email;
using SharedKernel.Tests.Persistence;
using Xunit;

namespace Account.Tests.SupportTickets.BackOffice;

// A dedicated factory that swaps the per-request transient IEmailClient for a single shared
// substitute so tests can verify Send(...) call counts. The default BackOfficeWebApplicationFactory
// builds a new substitute on every resolution, which makes verification impossible.
public sealed class SupportTicketBackOfficeWebApplicationFactory : BackOfficeWebApplicationFactory
{
    public IEmailClient EmailClient { get; } = Substitute.For<IEmailClient>();

    protected override void ConfigureAdditionalTestServices(IServiceCollection services)
    {
        services.RemoveAll(typeof(IEmailClient));
        services.AddSingleton(EmailClient);

        // The email TSX templates are compiled to dist/ as part of the email build, which doesn't run
        // before the test host starts in CI. Substitute the renderer so handlers don't hit disk.
        services.RemoveAll(typeof(IEmailRenderer));
        var renderer = Substitute.For<IEmailRenderer>();
        renderer.RenderEmail(Arg.Any<EmailTemplateBase>()).Returns(new EmailRenderResult("Subject", "<html />", "Plain"));
        services.AddSingleton(renderer);
    }
}

public sealed class BackOfficeSupportTicketTests(SupportTicketBackOfficeWebApplicationFactory factory)
    : BackOfficeEndpointBaseTest(factory), IClassFixture<SupportTicketBackOfficeWebApplicationFactory>
{
    static BackOfficeSupportTicketTests()
    {
        Environment.SetEnvironmentVariable("BLOB_STORAGE_URL", "https://test.blob.core.windows.net");
    }

    [Fact]
    public async Task GetAllTickets_WhenCalled_ShouldReturnTicketsAcrossTenants()
    {
        // Arrange
        var ticketId = SeedTicket(DatabaseSeeder.Tenant1.Id, DatabaseSeeder.Tenant1Owner.Id, DatabaseSeeder.Tenant1Owner.Email, SupportTicketStatus.New);
        var identity = MockEasyAuthIdentities.Default.Single(i => i.Id == "user");
        using var client = CreateBackOfficeClientForIdentity(identity);

        // Act
        var response = await client.GetAsync("/api/back-office/support-tickets");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<AllTicketsResponse>();
        payload.Should().NotBeNull();
        payload.Tickets.Should().Contain(t => t.Id.Value == ticketId.Value);
        payload.Counts.New.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task GetAllTickets_WhenAnonymous_ShouldReturnUnauthorized()
    {
        // Arrange
        using var client = CreateBackOfficeClient();

        // Act
        var response = await client.GetAsync("/api/back-office/support-tickets");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // The back-office route group uses BackOfficeIdentityDefaults.PolicyName, which admits any
    // authenticated back-office identity (no group claim required). Per BackOfficeIdentityDefaults
    // the only differentiated outcome between "tenant user" and "back-office staff" is whether the
    // Easy Auth headers are present at all — anonymous requests get 401. So the PRD's required
    // "non-back-office identity should be rejected" test reduces to verifying every mutation
    // returns 401 on the back-office host without principal headers.
    [Fact]
    public async Task ReplyToTicketAsStaff_WhenAnonymous_ShouldReturnUnauthorized()
    {
        // Arrange
        var ticketId = SeedTicket(DatabaseSeeder.Tenant1.Id, DatabaseSeeder.Tenant1Owner.Id, DatabaseSeeder.Tenant1Owner.Email, SupportTicketStatus.AwaitingAgent);
        using var client = CreateBackOfficeClient();
        var form = new MultipartFormDataContent { { new StringContent("body"), "body" }, { new StringContent("false"), "markAsResolved" } };

        // Act
        var response = await client.PostAsync($"/api/back-office/support-tickets/{ticketId}/reply", form);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PostInternalNote_WhenAnonymous_ShouldReturnUnauthorized()
    {
        // Arrange
        var ticketId = SeedTicket(DatabaseSeeder.Tenant1.Id, DatabaseSeeder.Tenant1Owner.Id, DatabaseSeeder.Tenant1Owner.Email, SupportTicketStatus.AwaitingAgent);
        using var client = CreateBackOfficeClient();
        var form = new MultipartFormDataContent { { new StringContent("internal"), "body" } };

        // Act
        var response = await client.PostAsync($"/api/back-office/support-tickets/{ticketId}/internal-note", form);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ChangeTicketStatus_WhenAnonymous_ShouldReturnUnauthorized()
    {
        // Arrange
        var ticketId = SeedTicket(DatabaseSeeder.Tenant1.Id, DatabaseSeeder.Tenant1Owner.Id, DatabaseSeeder.Tenant1Owner.Email, SupportTicketStatus.AwaitingAgent);
        using var client = CreateBackOfficeClient();
        var command = new ChangeTicketStatusCommand(SupportTicketStatus.AwaitingInternal);

        // Act
        var response = await client.PutAsJsonAsync($"/api/back-office/support-tickets/{ticketId}/status", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AssignTicket_WhenAnonymous_ShouldReturnUnauthorized()
    {
        // Arrange
        var ticketId = SeedTicket(DatabaseSeeder.Tenant1.Id, DatabaseSeeder.Tenant1Owner.Id, DatabaseSeeder.Tenant1Owner.Email, SupportTicketStatus.New);
        using var client = CreateBackOfficeClient();
        var command = new AssignTicketCommand { AssigneeObjectId = "anyone", AssigneeDisplayName = "Anyone" };

        // Act
        var response = await client.PutAsJsonAsync($"/api/back-office/support-tickets/{ticketId}/assignee", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task MarkResolvedByStaff_WhenAnonymous_ShouldReturnUnauthorized()
    {
        // Arrange
        var ticketId = SeedTicket(DatabaseSeeder.Tenant1.Id, DatabaseSeeder.Tenant1Owner.Id, DatabaseSeeder.Tenant1Owner.Email, SupportTicketStatus.AwaitingAgent);
        using var client = CreateBackOfficeClient();

        // Act
        var response = await client.PostAsync($"/api/back-office/support-tickets/{ticketId}/mark-resolved", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetTicketDetailForStaff_WhenAnonymous_ShouldReturnUnauthorized()
    {
        // Arrange
        var ticketId = SeedTicket(DatabaseSeeder.Tenant1.Id, DatabaseSeeder.Tenant1Owner.Id, DatabaseSeeder.Tenant1Owner.Email, SupportTicketStatus.New);
        using var client = CreateBackOfficeClient();

        // Act
        var response = await client.GetAsync($"/api/back-office/support-tickets/{ticketId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ReplyToTicketAsStaff_WhenPosted_ShouldTransitionToAwaitingUserAndEnqueueExactlyOneEmail()
    {
        // Arrange
        var ticketId = SeedTicket(DatabaseSeeder.Tenant1.Id, DatabaseSeeder.Tenant1Owner.Id, DatabaseSeeder.Tenant1Owner.Email, SupportTicketStatus.AwaitingAgent);
        var identity = MockEasyAuthIdentities.Default.Single(i => i.Id == "user");
        using var client = CreateBackOfficeClientForIdentity(identity);
        var form = new MultipartFormDataContent
        {
            { new StringContent("Hi — we are looking into this now."), "body" },
            { new StringContent("false"), "markAsResolved" }
        };
        factory.EmailClient.ClearReceivedCalls();
        TelemetryEventsCollectorSpy.Reset();

        // Act
        var response = await client.PostAsync($"/api/back-office/support-tickets/{ticketId}/reply", form);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var status = Connection.ExecuteScalar<string>("SELECT status FROM support_tickets WHERE id = @id", [new { id = ticketId.ToString() }]);
        status.Should().Be(nameof(SupportTicketStatus.AwaitingUser));
        await factory.EmailClient.Received(1).SendAsync(Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>());
        TelemetryEventsCollectorSpy.CollectedEvents.Should().Contain(e => e.GetType().Name == "SupportTicketReplyPosted");
    }

    [Fact]
    public async Task PostInternalNote_WhenPosted_ShouldNotChangeStatusAndNotEnqueueEmail()
    {
        // Arrange
        var ticketId = SeedTicket(DatabaseSeeder.Tenant1.Id, DatabaseSeeder.Tenant1Owner.Id, DatabaseSeeder.Tenant1Owner.Email, SupportTicketStatus.AwaitingAgent);
        var identity = MockEasyAuthIdentities.Default.Single(i => i.Id == "user");
        using var client = CreateBackOfficeClientForIdentity(identity);
        var form = new MultipartFormDataContent
        {
            { new StringContent("Looking into the upstream provider — do not share."), "body" }
        };
        factory.EmailClient.ClearReceivedCalls();

        // Act
        var response = await client.PostAsync($"/api/back-office/support-tickets/{ticketId}/internal-note", form);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var status = Connection.ExecuteScalar<string>("SELECT status FROM support_tickets WHERE id = @id", [new { id = ticketId.ToString() }]);
        status.Should().Be(nameof(SupportTicketStatus.AwaitingAgent));
        await factory.EmailClient.DidNotReceiveWithAnyArgs().SendAsync(null!, CancellationToken.None);
    }

    [Fact]
    public async Task AssignTicket_WhenStaffAssignsToSelf_ShouldPersistAssignee()
    {
        // Arrange
        var ticketId = SeedTicket(DatabaseSeeder.Tenant1.Id, DatabaseSeeder.Tenant1Owner.Id, DatabaseSeeder.Tenant1Owner.Email, SupportTicketStatus.New);
        var identity = MockEasyAuthIdentities.Default.Single(i => i.Id == "user");
        using var client = CreateBackOfficeClientForIdentity(identity);
        var command = new AssignTicketCommand { AssigneeObjectId = identity.ObjectId, AssigneeDisplayName = identity.Name };

        // Act
        var response = await client.PutAsJsonAsync($"/api/back-office/support-tickets/{ticketId}/assignee", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var assigneeJson = Connection.ExecuteScalar<string>("SELECT assignee FROM support_tickets WHERE id = @id", [new { id = ticketId.ToString() }]);
        assigneeJson.Should().Contain(identity.ObjectId);
    }

    [Fact]
    public async Task ChangeTicketStatus_WhenClosingViaStaff_ShouldReturnBadRequest()
    {
        // Arrange
        var ticketId = SeedTicket(DatabaseSeeder.Tenant1.Id, DatabaseSeeder.Tenant1Owner.Id, DatabaseSeeder.Tenant1Owner.Email, SupportTicketStatus.AwaitingAgent);
        var identity = MockEasyAuthIdentities.Default.Single(i => i.Id == "user");
        using var client = CreateBackOfficeClientForIdentity(identity);
        var command = new ChangeTicketStatusCommand(SupportTicketStatus.Closed);

        // Act
        var response = await client.PutAsJsonAsync($"/api/back-office/support-tickets/{ticketId}/status", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private SupportTicketId SeedTicket(TenantId tenantId, UserId reporterId, string reporterEmail, SupportTicketStatus status)
    {
        var id = SupportTicketId.NewId();
        var now = DateTimeOffset.UtcNow;
        Connection.Insert("support_tickets", [
                ("tenant_id", tenantId.Value),
                ("id", id.ToString()),
                ("created_at", now.AddMinutes(-30)),
                ("modified_at", null),
                ("short_display_id", RandomShortDisplayId()),
                ("reporter_id", reporterId.ToString()),
                ("reporter_role_snapshot", nameof(UserRole.Owner)),
                ("reporter_email_snapshot", reporterEmail),
                ("subject", "Seeded support ticket"),
                ("category", nameof(SupportTicketCategory.Account)),
                ("status", status.ToString()),
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

    private static string RandomShortDisplayId()
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        return new string(Enumerable.Range(0, 6).Select(_ => alphabet[Random.Shared.Next(alphabet.Length)]).ToArray());
    }
}
