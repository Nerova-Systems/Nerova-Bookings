using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Main.Database;
using Main.Features.Clients.Domain;
using SharedKernel.Tests.Persistence;
using Xunit;

namespace Main.Tests.DataImport;

/// <summary>
///     End-to-end tests for the AI data import (docs/agentic-system-spec.md §5.3): upload → parse →
///     column inference (deterministic heuristic path in tests) → normalization (SA phone formats) →
///     duplicate detection → review gate → idempotent commit. Nothing reaches the clients table before
///     approval (spec R19 AC).
/// </summary>
public sealed class ImportJobTests : EndpointBaseTest<MainDbContext>
{
    private const string RoutesPrefix = "/api/main/import-jobs";

    [Fact]
    public async Task StartImportJob_WithMixedPhoneFormats_ShouldNormalizeAndReachReviewWithoutWriting()
    {
        // Arrange
        const string csv = """
                           First Name,Surname,Cell,Email,Notes
                           Thandi,Mokoena,082 123 4567,thandi@example.com,Prefers Saturdays
                           Lerato,Khumalo,+27831234567,lerato@example.com,
                           Sipho,Dlamini,27841234567,,
                           Naledi,Nkosi,0851234567,naledi@example.com,Allergic to acrylics
                           Broken,Row,12345,,
                           """;

        // Act
        var response = await AuthenticatedOwnerHttpClient.PostAsJsonAsync(RoutesPrefix, new { fileName = "clients.csv", fileContent = csv });

        // Assert
        response.EnsureSuccessStatusCode();
        var jobId = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString();

        var details = await AuthenticatedOwnerHttpClient.GetFromJsonAsync<JsonElement>($"{RoutesPrefix}/{jobId}");
        details.GetProperty("status").GetString().Should().Be("ReadyForReview");
        details.GetProperty("rowsTotal").GetInt32().Should().Be(5);
        details.GetProperty("rowsValid").GetInt32().Should().Be(4);
        details.GetProperty("rowsInvalid").GetInt32().Should().Be(1);

        var rows = details.GetProperty("rows").EnumerateArray().ToArray();
        rows[0].GetProperty("phoneNumber").GetString().Should().Be("+27821234567");
        rows[1].GetProperty("phoneNumber").GetString().Should().Be("+27831234567");
        rows[2].GetProperty("phoneNumber").GetString().Should().Be("+27841234567");
        rows[3].GetProperty("phoneNumber").GetString().Should().Be("+27851234567");
        rows[4].GetProperty("status").GetString().Should().Be("Invalid");

        Connection.ExecuteScalar<long>("SELECT COUNT(*) FROM clients", []).Should().Be(0, "no rows may be written before approval");
    }

    [Fact]
    public async Task StartImportJob_WhenClientAlreadyExists_ShouldFlagDuplicate()
    {
        // Arrange
        InsertClient("+27821234567", "Thandi", "Mokoena", "thandi@example.com");
        const string csv = """
                           Name,Phone
                           Thandi Mokoena,0821234567
                           Zanele Ndlovu,0837654321
                           """;

        // Act
        var response = await AuthenticatedOwnerHttpClient.PostAsJsonAsync(RoutesPrefix, new { fileName = "clients.csv", fileContent = csv });

        // Assert
        response.EnsureSuccessStatusCode();
        var jobId = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString();
        var details = await AuthenticatedOwnerHttpClient.GetFromJsonAsync<JsonElement>($"{RoutesPrefix}/{jobId}");

        details.GetProperty("rowsDuplicate").GetInt32().Should().Be(1);
        details.GetProperty("rowsValid").GetInt32().Should().Be(1);
        var duplicateRow = details.GetProperty("rows").EnumerateArray().First(row => row.GetProperty("status").GetString() == "Duplicate");
        duplicateRow.GetProperty("firstName").GetString().Should().Be("Thandi");
    }

    [Fact]
    public async Task ApproveImportJob_ShouldCommitRowsAndBeIdempotent()
    {
        // Arrange
        InsertClient("+27821234567", "Thandi", "Old-Surname", null);
        const string csv = """
                           First Name,Last Name,Mobile,Email
                           Thandi,Mokoena,082 123 4567,thandi@example.com
                           Zanele,Ndlovu,083 765 4321,zanele@example.com
                           Bongi,Sithole,084 555 1234,
                           """;
        var startResponse = await AuthenticatedOwnerHttpClient.PostAsJsonAsync(RoutesPrefix, new { fileName = "clients.csv", fileContent = csv });
        startResponse.EnsureSuccessStatusCode();
        var jobId = (await startResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString();
        TelemetryEventsCollectorSpy.Reset();

        // Act
        var approveResponse = await AuthenticatedOwnerHttpClient.PostAsJsonAsync($"{RoutesPrefix}/{jobId}/approve", new { excludeRowNumbers = Array.Empty<int>() });

        // Assert
        approveResponse.EnsureSuccessStatusCode();
        Connection.ExecuteScalar<long>("SELECT COUNT(*) FROM clients", []).Should().Be(3, "two new clients plus the merged existing one");
        Connection.ExecuteScalar<string>("SELECT email FROM clients WHERE phone_number = '+27821234567'", []).Should().Be("thandi@example.com", "merge fills blanks on the existing client");
        Connection.ExecuteScalar<string>("SELECT last_name FROM clients WHERE phone_number = '+27821234567'", []).Should().Be("Old-Surname", "merge never overwrites existing values");
        Connection.ExecuteScalar<string>($"SELECT status FROM import_jobs WHERE id = '{jobId}'", []).Should().Be("Completed");
        TelemetryEventsCollectorSpy.CollectedEvents.Should().Contain(telemetryEvent => telemetryEvent.GetType().Name == "ImportJobCompleted");

        // Approving again is rejected and creates no duplicates.
        var secondApprove = await AuthenticatedOwnerHttpClient.PostAsJsonAsync($"{RoutesPrefix}/{jobId}/approve", new { excludeRowNumbers = Array.Empty<int>() });
        secondApprove.StatusCode.Should().Be(System.Net.HttpStatusCode.BadRequest);
        Connection.ExecuteScalar<long>("SELECT COUNT(*) FROM clients", []).Should().Be(3);
    }

    [Fact]
    public async Task ApproveImportJob_WithExcludedRows_ShouldSkipThem()
    {
        // Arrange
        const string csv = """
                           Name,WhatsApp
                           Keep Me,082 111 1111
                           Skip Me,083 222 2222
                           """;
        var startResponse = await AuthenticatedOwnerHttpClient.PostAsJsonAsync(RoutesPrefix, new { fileName = "clients.csv", fileContent = csv });
        var jobId = (await startResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString();

        // Act
        var approveResponse = await AuthenticatedOwnerHttpClient.PostAsJsonAsync($"{RoutesPrefix}/{jobId}/approve", new { excludeRowNumbers = new[] { 2 } });

        // Assert
        approveResponse.EnsureSuccessStatusCode();
        Connection.ExecuteScalar<long>("SELECT COUNT(*) FROM clients", []).Should().Be(1);
        Connection.ExecuteScalar<string>("SELECT first_name FROM clients", []).Should().Be("Keep");
    }

    [Fact]
    public async Task RejectImportJob_ShouldCloseJobWithoutWriting()
    {
        // Arrange
        const string csv = """
                           Name,Phone
                           Thandi Mokoena,0821234567
                           """;
        var startResponse = await AuthenticatedOwnerHttpClient.PostAsJsonAsync(RoutesPrefix, new { fileName = "clients.csv", fileContent = csv });
        var jobId = (await startResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString();

        // Act
        var rejectResponse = await AuthenticatedOwnerHttpClient.PostAsJsonAsync($"{RoutesPrefix}/{jobId}/reject", new { });

        // Assert
        rejectResponse.EnsureSuccessStatusCode();
        Connection.ExecuteScalar<string>($"SELECT status FROM import_jobs WHERE id = '{jobId}'", []).Should().Be("Rejected");
        Connection.ExecuteScalar<long>("SELECT COUNT(*) FROM clients", []).Should().Be(0);
    }

    [Fact]
    public async Task StartImportJob_WhenFileHasNoNameColumn_ShouldFailGracefully()
    {
        // Arrange
        const string csv = """
                           Quantity,Price
                           3,100
                           """;

        // Act
        var response = await AuthenticatedOwnerHttpClient.PostAsJsonAsync(RoutesPrefix, new { fileName = "stock.csv", fileContent = csv });

        // Assert
        response.EnsureSuccessStatusCode();
        var jobId = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString();
        var details = await AuthenticatedOwnerHttpClient.GetFromJsonAsync<JsonElement>($"{RoutesPrefix}/{jobId}");
        details.GetProperty("status").GetString().Should().Be("Failed");
        details.GetProperty("errorMessage").GetString().Should().Contain("name column");
    }

    [Fact]
    public async Task GetImportJobs_WhenAnonymous_ShouldReturnUnauthorized()
    {
        // Act
        var response = await AnonymousHttpClient.GetAsync(RoutesPrefix);

        // Assert
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.Unauthorized);
    }

    private void InsertClient(string phoneNumber, string firstName, string lastName, string? email)
    {
        Connection.Insert("clients", [
                ("tenant_id", DatabaseSeeder.TenantId.Value),
                ("id", ClientId.NewId().ToString()),
                ("created_at", TimeProvider.GetUtcNow().AddDays(-30)),
                ("modified_at", null),
                ("first_name", firstName),
                ("last_name", lastName),
                ("email", email),
                ("phone_number", phoneNumber),
                ("avatar_url", null),
                ("notes", null),
                ("last_visit_at", null)
            ]
        );
    }
}
