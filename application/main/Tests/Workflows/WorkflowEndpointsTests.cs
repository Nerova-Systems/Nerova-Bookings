using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using JetBrains.Annotations;
using Main.Database;
using Main.Features.Workflows.Domain;
using Main.Features.Workflows.Shared;
using SharedKernel.Authentication;
using SharedKernel.Tests;
using Xunit;

namespace Main.Tests.Workflows;

public sealed class WorkflowEndpointsTests : EndpointBaseTest<MainDbContext>
{
    private readonly HttpClient _workflowsClient;

    public WorkflowEndpointsTests()
    {
        // Create a client for an owner with the cap-workflows feature flag enabled
        var ownerWithFlag = new UserInfo
        {
            Email = DatabaseSeeder.Tenant1Owner.Email,
            FirstName = DatabaseSeeder.Tenant1Owner.FirstName,
            LastName = DatabaseSeeder.Tenant1Owner.LastName,
            Id = DatabaseSeeder.Tenant1Owner.Id,
            IsAuthenticated = true,
            Locale = DatabaseSeeder.Tenant1Owner.Locale,
            Role = DatabaseSeeder.Tenant1Owner.Role,
            TenantId = DatabaseSeeder.Tenant1Owner.TenantId,
            FeatureFlags = new HashSet<string> { "cap-workflows" }
        };
        _workflowsClient = CreateAuthenticatedHttpClient(ownerWithFlag);
    }

    // ── Workflows CRUD ───────────────────────────────────────────────────────

    [Fact]
    public async Task CreateWorkflow_WhenOwnerWithFlagCreatesWorkflow_ShouldPersistAndReturnWorkflow()
    {
        var command = NewWorkflowRequest("Follow-up sequence", "NewEvent");

        var createResponse = await _workflowsClient.PostAsJsonAsync("/api/workflows", command);
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.DeserializeResponse<WorkflowResponse>();

        created.Should().NotBeNull();
        created!.Name.Should().Be("Follow-up sequence");
        created.Trigger.Should().Be(WorkflowTrigger.NewEvent);
        created.Steps.Should().BeEmpty();

        var getResponse = await _workflowsClient.GetAsync($"/api/workflows/{created.Id}");
        getResponse.ShouldBeSuccessfulGetRequest();
        var fetched = await getResponse.DeserializeResponse<WorkflowResponse>();
        fetched!.Id.Should().Be(created.Id);
        fetched.Name.Should().Be("Follow-up sequence");
    }

    [Fact]
    public async Task CreateWorkflow_WhenAnonymous_ShouldReturnUnauthorized()
    {
        var response = await AnonymousHttpClient.PostAsJsonAsync("/api/workflows", NewWorkflowRequest("Test", "NewBooking"));
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateWorkflow_WhenMember_ShouldReturnForbidden()
    {
        var memberWithFlag = new UserInfo
        {
            Email = DatabaseSeeder.Tenant1Member.Email,
            FirstName = DatabaseSeeder.Tenant1Member.FirstName,
            LastName = DatabaseSeeder.Tenant1Member.LastName,
            Id = DatabaseSeeder.Tenant1Member.Id,
            IsAuthenticated = true,
            Locale = DatabaseSeeder.Tenant1Member.Locale,
            Role = DatabaseSeeder.Tenant1Member.Role,
            TenantId = DatabaseSeeder.Tenant1Member.TenantId,
            FeatureFlags = new HashSet<string> { "cap-workflows" }
        };
        var memberClient = CreateAuthenticatedHttpClient(memberWithFlag);

        var response = await memberClient.PostAsJsonAsync("/api/workflows", NewWorkflowRequest("Test", "NewEvent"));
        await response.ShouldHaveErrorStatusCode(HttpStatusCode.Forbidden, "Only owners and admins can manage workflows.");
    }

    [Fact]
    public async Task CreateWorkflow_WhenFeatureFlagDisabled_ShouldReturnForbidden()
    {
        // AuthenticatedOwnerHttpClient has no feature flags
        var response = await AuthenticatedOwnerHttpClient.PostAsJsonAsync("/api/workflows", NewWorkflowRequest("Test", "NewEvent"));
        await response.ShouldHaveErrorStatusCode(HttpStatusCode.Forbidden, "The workflows feature is not enabled for your account.");
    }

    [Fact]
    public async Task CreateWorkflow_WhenDuplicateName_ShouldReturnBadRequest()
    {
        await CreateWorkflowAsync("Duplicate Workflow", "NewEvent");

        var response = await _workflowsClient.PostAsJsonAsync("/api/workflows", NewWorkflowRequest("Duplicate Workflow", "NewEvent"));
        await response.ShouldHaveErrorStatusCode(HttpStatusCode.BadRequest, "A workflow named 'Duplicate Workflow' already exists.");
    }

    [Fact]
    public async Task GetWorkflows_WhenOwnerHasWorkflows_ShouldReturnAll()
    {
        await CreateWorkflowAsync("Workflow A", "NewEvent");
        await CreateWorkflowAsync("Workflow B", "EventCancelled");

        var response = await _workflowsClient.GetAsync("/api/workflows");
        response.ShouldBeSuccessfulGetRequest();
        var result = await response.DeserializeResponse<WorkflowsResponse>();

        result!.Workflows.Should().HaveCountGreaterThanOrEqualTo(2);
        result.Workflows.Select(w => w.Name).Should().Contain(["Workflow A", "Workflow B"]);
    }

    [Fact]
    public async Task UpdateWorkflow_WhenOwnerRenamesWorkflow_ShouldPersistChange()
    {
        var created = await CreateWorkflowAsync("Original Name", "NewEvent");

        var updateResponse = await _workflowsClient.PutAsJsonAsync($"/api/workflows/{created.Id}", NewWorkflowRequest("Renamed Workflow", "NewEvent"));
        updateResponse.EnsureSuccessStatusCode();
        var updated = await updateResponse.DeserializeResponse<WorkflowResponse>();

        updated!.Name.Should().Be("Renamed Workflow");

        var getResponse = await _workflowsClient.GetAsync($"/api/workflows/{created.Id}");
        var fetched = await getResponse.DeserializeResponse<WorkflowResponse>();
        fetched!.Name.Should().Be("Renamed Workflow");
    }

    [Fact]
    public async Task DeleteWorkflow_WhenOwnerDeletesWorkflow_ShouldRemoveIt()
    {
        var created = await CreateWorkflowAsync("To be deleted", "NewEvent");

        var deleteResponse = await _workflowsClient.DeleteAsync($"/api/workflows/{created.Id}");
        deleteResponse.ShouldHaveEmptyHeaderAndLocationOnSuccess();

        var getResponse = await _workflowsClient.GetAsync($"/api/workflows/{created.Id}");
        await getResponse.ShouldHaveErrorStatusCode(HttpStatusCode.NotFound, $"Workflow '{created.Id}' was not found.");
    }

    // ── Steps ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AddWorkflowStep_WhenOwnerAddsStep_ShouldPersistAndReturnStep()
    {
        var workflow = await CreateWorkflowAsync("Email sequence", "NewEvent");
        var stepRequest = NewStepRequest("EmailAttendee", "Custom", emailSubject: "Hello", emailBody: "Welcome!");

        var addResponse = await _workflowsClient.PostAsJsonAsync($"/api/workflows/{workflow.Id}/steps", stepRequest);
        addResponse.EnsureSuccessStatusCode();
        var step = await addResponse.DeserializeResponse<WorkflowStepResponse>();

        step.Should().NotBeNull();
        step!.Action.Should().Be(WorkflowAction.EmailAttendee);
        step.Template.Should().Be(WorkflowReminderTemplate.Custom);
        step.EmailSubject.Should().Be("Hello");
        step.EmailBody.Should().Be("Welcome!");

        var getResponse = await _workflowsClient.GetAsync($"/api/workflows/{workflow.Id}");
        var fetched = await getResponse.DeserializeResponse<WorkflowResponse>();
        fetched!.Steps.Should().ContainSingle(s => s.Id == step.Id);
    }

    [Fact]
    public async Task UpdateWorkflowStep_WhenOwnerUpdatesStep_ShouldPersistChange()
    {
        var workflow = await CreateWorkflowAsync("SMS sequence", "NewEvent");
        var step = await AddStepAsync(workflow.Id, "SmsAttendee", "Reminder");

        var updateRequest = NewStepRequest("EmailAttendee", "Custom", emailSubject: "Updated subject");
        var updateResponse = await _workflowsClient.PutAsJsonAsync($"/api/workflows/{workflow.Id}/steps/{step.Id}", updateRequest);
        updateResponse.EnsureSuccessStatusCode();
        var updated = await updateResponse.DeserializeResponse<WorkflowStepResponse>();

        updated!.Action.Should().Be(WorkflowAction.EmailAttendee);
        updated.EmailSubject.Should().Be("Updated subject");
    }

    [Fact]
    public async Task DeleteWorkflowStep_WhenOwnerDeletesStep_ShouldRemoveStep()
    {
        var workflow = await CreateWorkflowAsync("Step deletion test", "NewEvent");
        var step = await AddStepAsync(workflow.Id, "SmsAttendee", "Reminder");

        var deleteResponse = await _workflowsClient.DeleteAsync($"/api/workflows/{workflow.Id}/steps/{step.Id}");
        deleteResponse.ShouldHaveEmptyHeaderAndLocationOnSuccess();

        var getResponse = await _workflowsClient.GetAsync($"/api/workflows/{workflow.Id}");
        var fetched = await getResponse.DeserializeResponse<WorkflowResponse>();
        fetched!.Steps.Should().BeEmpty();
    }

    // ── Bindings ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task BindWorkflowToEventType_WhenOwnerBindsWorkflow_ShouldReturnBinding()
    {
        var workflow = await CreateWorkflowAsync("Reminder workflow", "NewEvent");
        var eventType = await CreateEventTypeAsync();

        var bindResponse = await _workflowsClient.PostAsJsonAsync(
            $"/api/workflows/{workflow.Id}/bindings",
            new { eventTypeId = eventType.Id }
        );
        bindResponse.EnsureSuccessStatusCode();
        var binding = await bindResponse.DeserializeResponse<WorkflowEventTypeBindingResponse>();

        binding.Should().NotBeNull();
        binding!.WorkflowId.Should().Be(workflow.Id);
        binding.EventTypeId.Should().Be(eventType.Id);
    }

    [Fact]
    public async Task BindWorkflowToEventType_WhenAlreadyBound_ShouldReturnBadRequest()
    {
        var workflow = await CreateWorkflowAsync("Duplicate binding", "NewEvent");
        var eventType = await CreateEventTypeAsync();

        await _workflowsClient.PostAsJsonAsync(
            $"/api/workflows/{workflow.Id}/bindings",
            new { eventTypeId = eventType.Id }
        );

        var secondResponse = await _workflowsClient.PostAsJsonAsync(
            $"/api/workflows/{workflow.Id}/bindings",
            new { eventTypeId = eventType.Id }
        );
        await secondResponse.ShouldHaveErrorStatusCode(HttpStatusCode.BadRequest, "This workflow is already bound to the event type.");
    }

    [Fact]
    public async Task UnbindWorkflowFromEventType_WhenOwnerUnbinds_ShouldRemoveBinding()
    {
        var workflow = await CreateWorkflowAsync("Unbind test", "NewEvent");
        var eventType = await CreateEventTypeAsync();

        var bindResponse = await _workflowsClient.PostAsJsonAsync(
            $"/api/workflows/{workflow.Id}/bindings",
            new { eventTypeId = eventType.Id }
        );
        bindResponse.EnsureSuccessStatusCode();

        var unbindResponse = await _workflowsClient.DeleteAsync($"/api/workflows/{workflow.Id}/bindings/{eventType.Id}");
        unbindResponse.ShouldHaveEmptyHeaderAndLocationOnSuccess();
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private async Task<WorkflowResponse> CreateWorkflowAsync(string name, string trigger)
    {
        var response = await _workflowsClient.PostAsJsonAsync("/api/workflows", NewWorkflowRequest(name, trigger));
        response.EnsureSuccessStatusCode();
        return (await response.DeserializeResponse<WorkflowResponse>())!;
    }

    private async Task<WorkflowStepResponse> AddStepAsync(string workflowId, string action, string template)
    {
        var response = await _workflowsClient.PostAsJsonAsync($"/api/workflows/{workflowId}/steps", NewStepRequest(action, template));
        response.EnsureSuccessStatusCode();
        return (await response.DeserializeResponse<WorkflowStepResponse>())!;
    }

    private async Task<EventTypeResponse> CreateEventTypeAsync()
    {
        var scheduleResponse = await AuthenticatedOwnerHttpClient.PostAsJsonAsync("/api/schedules", new
        {
            name = "Default Schedule",
            timeZone = "America/New_York",
            isDefault = true,
            availabilityWindows = new[] { new { days = new[] { 1, 2, 3, 4, 5 }, startMinute = 540, endMinute = 1020 } }
        });
        scheduleResponse.EnsureSuccessStatusCode();
        var schedule = (await scheduleResponse.DeserializeResponse<ScheduleResponse>())!;

        var eventTypeResponse = await AuthenticatedOwnerHttpClient.PostAsJsonAsync("/api/event-types", new
        {
            title = $"Event Type {Guid.NewGuid():N}",
            slug = $"et-{Guid.NewGuid():N}",
            durationMinutes = 30,
            slotIntervalMinutes = 30,
            scheduleId = schedule.Id,
            locationType = "link",
            locationValue = "https://example.com/meet"
        });
        eventTypeResponse.EnsureSuccessStatusCode();
        return (await eventTypeResponse.DeserializeResponse<EventTypeResponse>())!;
    }

    private static object NewWorkflowRequest(string name, string trigger)
        => new { name, trigger };

    private static object NewStepRequest(
        string action,
        string template,
        int? reminderTime = null,
        string? timeUnit = null,
        string? sendTo = null,
        string? emailSubject = null,
        string? emailBody = null
    ) => new { action, template, reminderTime, timeUnit, sendTo, emailSubject, emailBody };

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed record WorkflowResponse(string Id, string Name, WorkflowTrigger Trigger, WorkflowStepResponse[] Steps);

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed record WorkflowsResponse(WorkflowResponse[] Workflows);

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed record WorkflowStepResponse(string Id, WorkflowAction Action, WorkflowReminderTemplate Template,
        int? ReminderTime, string? TimeUnit, string? SendTo, string? EmailSubject, string? EmailBody);

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed record WorkflowEventTypeBindingResponse(string Id, string WorkflowId, string EventTypeId);

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed record ScheduleResponse(string Id);

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private sealed record EventTypeResponse(string Id);
}
