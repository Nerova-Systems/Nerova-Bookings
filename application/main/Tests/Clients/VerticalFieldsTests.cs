using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using JetBrains.Annotations;
using Main.Database;
using SharedKernel.Tests;
using SharedKernel.Tests.Persistence;
using Xunit;

namespace Main.Tests.Clients;

/// <summary>
///     Vertical template fields end-to-end (docs/vertical-template-fields-spec.md): vertical selection
///     on the scheduling profile, catalog-validated field writes, the Sensitive-class encryption and
///     role gate, and the masked review surface.
/// </summary>
public sealed class VerticalFieldsTests : EndpointBaseTest<MainDbContext>
{
    [Fact]
    public async Task SetVertical_WhenOwner_ShouldPersistAndReturnOnProfile()
    {
        // Arrange — the profile is lazily created by the GET
        (await AuthenticatedOwnerHttpClient.GetAsync("/api/scheduling/profile")).EnsureSuccessStatusCode();

        // Act
        var response = await AuthenticatedOwnerHttpClient.PutAsJsonAsync("/api/scheduling/profile/vertical", new { vertical = "Salon" });

        // Assert
        response.EnsureSuccessStatusCode();
        var profile = await (await AuthenticatedOwnerHttpClient.GetAsync("/api/scheduling/profile")).DeserializeResponse<ProfileResponse>();
        profile!.Vertical.Should().Be("Salon");
    }

    [Fact]
    public async Task UpdateVerticalFields_WhenValid_ShouldPersistAndReturnOnDetailsCard()
    {
        // Arrange
        var clientId = await SetUpClientAsync("Salon");

        // Act
        var response = await AuthenticatedOwnerHttpClient.PutAsJsonAsync($"/api/main/clients/{clientId}/vertical-fields", new
            {
                fields = new Dictionary<string, string?>
                {
                    ["hair_type"] = "Curly",
                    ["allergies_sensitivities"] = "PPD in hair dye",
                    ["birthday"] = "1992-03-14"
                }
            }
        );

        // Assert
        response.EnsureSuccessStatusCode();
        var details = await (await AuthenticatedOwnerHttpClient.GetAsync($"/api/main/clients/{clientId}/vertical-fields")).DeserializeResponse<FieldsResponse>();
        details!.Vertical.Should().Be("Salon");
        details.Fields.Single(field => field.Key == "hair_type").Value.Should().Be("Curly");
        details.Fields.Single(field => field.Key == "allergies_sensitivities").Sensitivity.Should().Be("Constraint");
        details.Fields.Single(field => field.Key == "allergies_sensitivities").Value.Should().Be("PPD in hair dye");
    }

    [Fact]
    public async Task UpdateVerticalFields_WhenUnknownKeyOrBadChoice_ShouldReturnBadRequest()
    {
        // Arrange
        var clientId = await SetUpClientAsync("Salon");

        // Act & Assert — unknown key (barber field, not salon)
        var unknown = await AuthenticatedOwnerHttpClient.PutAsJsonAsync($"/api/main/clients/{clientId}/vertical-fields", new
            {
                fields = new Dictionary<string, string?> { ["clipper_guard"] = "2" }
            }
        );
        unknown.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // Act & Assert — invalid choice option
        var badChoice = await AuthenticatedOwnerHttpClient.PutAsJsonAsync($"/api/main/clients/{clientId}/vertical-fields", new
            {
                fields = new Dictionary<string, string?> { ["hair_type"] = "Spiky" }
            }
        );
        badChoice.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task SensitiveFields_ShouldBeEncryptedAtRestRoleGatedAndAudited()
    {
        // Arrange — clinic vertical carries Sensitive fields
        var clientId = await SetUpClientAsync("Clinic");

        // Act — sensitive write travels only through the sensitive endpoint
        var rejected = await AuthenticatedOwnerHttpClient.PutAsJsonAsync($"/api/main/clients/{clientId}/vertical-fields", new
            {
                fields = new Dictionary<string, string?> { ["medical_aid_number"] = "123456789" }
            }
        );
        var written = await AuthenticatedOwnerHttpClient.PutAsJsonAsync($"/api/main/clients/{clientId}/sensitive-fields", new
            {
                fields = new Dictionary<string, string?> { ["medical_aid_number"] = "123456789" }
            }
        );

        // Assert
        rejected.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        written.EnsureSuccessStatusCode();

        // Encrypted at rest: the raw column never contains the plaintext value
        var storedPayload = Connection.ExecuteScalar<string>($"SELECT sensitive_fields FROM clients WHERE id = '{clientId}'", []);
        storedPayload.Should().NotBeNullOrEmpty();
        storedPayload.Should().NotContain("123456789");

        // Role gate: members cannot read; owners can and the read is audited
        var memberRead = await AuthenticatedMemberHttpClient.GetAsync($"/api/main/clients/{clientId}/sensitive-fields");
        memberRead.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var ownerRead = await AuthenticatedOwnerHttpClient.GetAsync($"/api/main/clients/{clientId}/sensitive-fields");
        ownerRead.EnsureSuccessStatusCode();
        var payload = await ownerRead.DeserializeResponse<SensitiveResponse>();
        payload!.Fields["medical_aid_number"].Should().Be("123456789");
        TelemetryEventsCollectorSpy.CollectedEvents.Should().Contain(telemetryEvent => telemetryEvent.GetType().Name == "SensitiveFieldAccessed");

        // The keys (never values) surface on the details card for the role-gated section
        var details = await (await AuthenticatedOwnerHttpClient.GetAsync($"/api/main/clients/{clientId}/vertical-fields")).DeserializeResponse<FieldsResponse>();
        details!.SensitiveFieldKeysWithValues.Should().Contain("medical_aid_number");
        details.Fields.Should().NotContain(field => field.Key == "medical_aid_number");
    }

    [Fact]
    public async Task ImportPipeline_WhenVerticalColumnsPresent_ShouldMapAndCommitThem()
    {
        // Arrange — salon vertical; CSV carries a synonym-matched vertical column ("Allergieë" → allergies)
        await SetUpVerticalAsync("Salon");
        var csv = "Name,Cell,Allergie\u00eb,Hair Type\nNaledi Dlamini,0825550001,Acrylic allergy,Curly\n";
        var startResponse = await AuthenticatedOwnerHttpClient.PostAsJsonAsync("/api/main/import-jobs", new { fileName = "klante.csv", fileContent = csv });
        startResponse.EnsureSuccessStatusCode();
        var jobId = (await startResponse.DeserializeResponse<StartImportResponse>())!.Id;

        // Act
        (await AuthenticatedOwnerHttpClient.PostAsync($"/api/main/import-jobs/{jobId}/run", null)).EnsureSuccessStatusCode();
        var job = await (await AuthenticatedOwnerHttpClient.GetAsync($"/api/main/import-jobs/{jobId}")).DeserializeResponse<ImportJobResponse>();

        // Assert mapping + review surface
        job!.ColumnMapping!.VerticalFieldColumns.Should().ContainKey("allergies_sensitivities").WhoseValue.Should().Be("Allergie\u00eb");
        job.ColumnMapping.VerticalFieldColumns.Should().ContainKey("hair_type").WhoseValue.Should().Be("Hair Type");
        job.ColumnMapping.ConstraintFieldKeys.Should().Contain("allergies_sensitivities");
        job.Rows[0].VerticalFields!["hair_type"].Should().Be("Curly");

        // Act — approve commits the vertical values
        (await AuthenticatedOwnerHttpClient.PostAsJsonAsync($"/api/main/import-jobs/{jobId}/approve", new { })).EnsureSuccessStatusCode();

        // Assert
        var storedFields = Connection.ExecuteScalar<string>("SELECT vertical_fields FROM clients WHERE phone_number = '+27825550001'", []);
        storedFields.Should().Contain("Curly").And.Contain("Acrylic allergy");
    }

    [Fact]
    public async Task ImportPipeline_WhenSensitiveColumns_ShouldMaskInReviewAndRequireConfirmation()
    {
        // Arrange — clinic vertical: medical aid fields are Sensitive
        await SetUpVerticalAsync("Clinic");
        var csv = "Name,Cell,Medical Aid,Member Number\nSipho Khumalo,0825550002,Discovery,98765\n";
        var startResponse = await AuthenticatedOwnerHttpClient.PostAsJsonAsync("/api/main/import-jobs", new { fileName = "patients.csv", fileContent = csv });
        var jobId = (await startResponse.DeserializeResponse<StartImportResponse>())!.Id;
        (await AuthenticatedOwnerHttpClient.PostAsync($"/api/main/import-jobs/{jobId}/run", null)).EnsureSuccessStatusCode();

        // Assert — masked in review, flagged as sensitive
        var job = await (await AuthenticatedOwnerHttpClient.GetAsync($"/api/main/import-jobs/{jobId}")).DeserializeResponse<ImportJobResponse>();
        job!.ColumnMapping!.SensitiveFieldKeys.Should().Contain("medical_aid_scheme").And.Contain("medical_aid_number");
        job.Rows[0].SensitiveFields!.Values.Should().OnlyContain(value => value == "•••");

        // Act — approve WITHOUT confirming: sensitive values must be dropped
        (await AuthenticatedOwnerHttpClient.PostAsJsonAsync($"/api/main/import-jobs/{jobId}/approve", new { })).EnsureSuccessStatusCode();

        // Assert
        var stored = Connection.ExecuteScalar<string>("SELECT sensitive_fields FROM clients WHERE phone_number = '+27825550002'", []);
        stored.Should().BeNull();
    }

    // ─── Helpers ─────────────────────────────────────────────────────

    private async Task SetUpVerticalAsync(string vertical)
    {
        (await AuthenticatedOwnerHttpClient.GetAsync("/api/scheduling/profile")).EnsureSuccessStatusCode();
        (await AuthenticatedOwnerHttpClient.PutAsJsonAsync("/api/scheduling/profile/vertical", new { vertical })).EnsureSuccessStatusCode();
    }

    private async Task<string> SetUpClientAsync(string vertical)
    {
        await SetUpVerticalAsync(vertical);
        var csv = "Name,Cell\nThandi Mokoena,0825559999\n";
        var startResponse = await AuthenticatedOwnerHttpClient.PostAsJsonAsync("/api/main/import-jobs", new { fileName = "client.csv", fileContent = csv });
        var jobId = (await startResponse.DeserializeResponse<StartImportResponse>())!.Id;
        (await AuthenticatedOwnerHttpClient.PostAsync($"/api/main/import-jobs/{jobId}/run", null)).EnsureSuccessStatusCode();
        (await AuthenticatedOwnerHttpClient.PostAsJsonAsync($"/api/main/import-jobs/{jobId}/approve", new { })).EnsureSuccessStatusCode();

        var clients = await (await AuthenticatedOwnerHttpClient.GetAsync("/api/main/clients/")).DeserializeResponse<JsonElement>();
        return clients.GetProperty("clients").EnumerateArray().First().GetProperty("id").GetString()!;
    }

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed record ProfileResponse(string Handle, string DisplayName, string? AvatarUrl, string? Vertical);

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed record FieldsResponse(string? Vertical, FieldEntry[] Fields, string[] SensitiveFieldKeysWithValues);

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed record FieldEntry(string Key, string Label, string Kind, string Sensitivity, string[] Options, string? Value);

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed record SensitiveResponse(Dictionary<string, string> Fields);

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed record StartImportResponse(string Id);

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed record ImportJobResponse(string Id, string Status, MappingEntry? ColumnMapping, RowEntry[] Rows);

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed record MappingEntry(Dictionary<string, string>? VerticalFieldColumns, string[] SensitiveFieldKeys, string[] ConstraintFieldKeys);

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed record RowEntry(int RowNumber, Dictionary<string, string>? VerticalFields, Dictionary<string, string>? SensitiveFields);
}
