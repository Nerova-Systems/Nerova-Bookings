using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Main.Api.Endpoints;
using Main.Database;
using Main.Features.Webhooks.Domain;
using Main.Features.Webhooks.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel.Authentication;
using SharedKernel.Tests;
using Xunit;

namespace Main.Tests.Webhooks;

/// <summary>
///     Integration tests for the webhook CRUD + test-fire HTTP surface. Exercises the auth gate
///     (Owner role + <c>cap-webhooks</c> flag) and verifies that the test-fire endpoint enqueues a
///     <see cref="WebhookDeliveryStatus.Pending" /> delivery without invoking the TickerQ worker.
/// </summary>
public sealed class WebhookEndpointsTests : EndpointBaseTest<MainDbContext>
{
    private readonly HttpClient _ownerWithFlag;

    public WebhookEndpointsTests()
    {
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
            FeatureFlags = new HashSet<string> { WebhookAuthorization.WebhooksFeatureFlagKey }
        };
        _ownerWithFlag = CreateAuthenticatedHttpClient(ownerWithFlag);
    }

    [Fact]
    public async Task FullLifecycle_ShouldCreateListUpdateTestAndDelete()
    {
        // CREATE — POST returns the cleartext secret exactly once (cal.com fidelity).
        var createBody = new
        {
            TargetUrl = "https://example.test/hooks/bookings",
            EventSubscriptions = new[] { WebhookEventType.BookingCreated, WebhookEventType.BookingCancelled },
            Active = true,
            EventTypeId = (string?)null
        };
        var createResponse = await _ownerWithFlag.PostAsJsonAsync("/api/webhooks", createBody);
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.DeserializeResponse<WebhookResponse>();
        created!.TargetUrl.Should().Be(createBody.TargetUrl);
        created.EventSubscriptions.Should().BeEquivalentTo(createBody.EventSubscriptions);
        // Generator emits 64 hex chars with no project prefix; cleartext must not be the masked form.
        created.Secret.Should().HaveLength(64).And.MatchRegex("^[0-9a-f]{64}$");
        created.Secret.Should().NotContain(WebhookResponse.SecretMaskPrefix);
        created.Active.Should().BeTrue();
        var originalLast4 = created.Secret[^4..];

        // LIST — secret is masked.
        var listResponse = await _ownerWithFlag.GetAsync("/api/webhooks");
        listResponse.ShouldBeSuccessfulGetRequest();
        var list = await listResponse.DeserializeResponse<WebhooksResponse>();
        var listed = list!.Webhooks.Should().ContainSingle(webhook => webhook.Id == created.Id).Subject;
        listed.Secret.Should().StartWith(WebhookResponse.SecretMaskPrefix).And.EndWith($"-{originalLast4}");

        // GET single — secret is masked.
        var getResponse = await _ownerWithFlag.GetAsync($"/api/webhooks/{created.Id}");
        getResponse.ShouldBeSuccessfulGetRequest();
        var fetched = await getResponse.DeserializeResponse<WebhookResponse>();
        fetched!.Secret.Should().StartWith(WebhookResponse.SecretMaskPrefix).And.EndWith($"-{originalLast4}");
        fetched.Secret.Should().NotContain(created.Secret);

        // UPDATE without regenerateSecret — masked response, DB row's secret unchanged.
        var updateBody = new UpdateWebhookRequest(
            "https://example.test/hooks/v2",
            [WebhookEventType.BookingCreated],
            false
        );
        var updateResponse = await _ownerWithFlag.PutAsJsonAsync($"/api/webhooks/{created.Id}", updateBody);
        updateResponse.EnsureSuccessStatusCode();
        var updated = await updateResponse.DeserializeResponse<WebhookResponse>();
        updated!.TargetUrl.Should().Be(updateBody.TargetUrl);
        updated.Active.Should().BeFalse();
        updated.EventSubscriptions.Should().BeEquivalentTo(updateBody.EventSubscriptions);
        updated.Secret.Should().StartWith(WebhookResponse.SecretMaskPrefix).And.EndWith($"-{originalLast4}");

        using (var scope = Provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MainDbContext>();
            var persisted = await db.Set<Webhook>().IgnoreQueryFilters().SingleAsync(w => w.Id == created.Id);
            persisted.Secret.Should().Be(created.Secret);
        }

        // UPDATE with regenerateSecret=true — fresh cleartext, different last4.
        var rotateResponse = await _ownerWithFlag.PutAsJsonAsync($"/api/webhooks/{created.Id}?regenerateSecret=true", updateBody);
        rotateResponse.EnsureSuccessStatusCode();
        var rotated = await rotateResponse.DeserializeResponse<WebhookResponse>();
        rotated!.Secret.Should().HaveLength(64).And.MatchRegex("^[0-9a-f]{64}$");
        rotated.Secret.Should().NotBe(created.Secret);
        var newLast4 = rotated.Secret[^4..];
        newLast4.Should().NotBe(originalLast4);

        // Follow-up GET — masked with the new last-4.
        var afterRotateGet = await _ownerWithFlag.GetAsync($"/api/webhooks/{created.Id}");
        afterRotateGet.ShouldBeSuccessfulGetRequest();
        var afterRotated = await afterRotateGet.DeserializeResponse<WebhookResponse>();
        afterRotated!.Secret.Should().StartWith(WebhookResponse.SecretMaskPrefix).And.EndWith($"-{newLast4}");

        // TEST-FIRE — should enqueue exactly one Pending delivery
        var testResponse = await _ownerWithFlag.PostAsync($"/api/webhooks/{created.Id}/test", null);
        testResponse.EnsureSuccessStatusCode();
        var test = await testResponse.DeserializeResponse<TestWebhookResponse>();
        test!.DeliveryId.Should().NotBeNull();

        using (var scope = Provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MainDbContext>();
            var delivery = await db.Set<WebhookDelivery>()
                .IgnoreQueryFilters()
                .SingleAsync(d => d.Id == test.DeliveryId);

            delivery.Status.Should().Be(WebhookDeliveryStatus.Pending);
            delivery.EventType.Should().Be(WebhookEventType.Ping);
            delivery.AttemptCount.Should().Be(0);
            delivery.NextAttemptAt.Should().NotBeNull();
        }

        // DELETE
        var deleteResponse = await _ownerWithFlag.DeleteAsync($"/api/webhooks/{created.Id}");
        deleteResponse.EnsureSuccessStatusCode();

        var afterDelete = await (await _ownerWithFlag.GetAsync("/api/webhooks")).DeserializeResponse<WebhooksResponse>();
        afterDelete!.Webhooks.Should().NotContain(webhook => webhook.Id == created.Id);
    }

    [Fact]
    public async Task CreateWebhook_WhenAnonymous_ShouldReturnUnauthorized()
    {
        var response = await AnonymousHttpClient.PostAsJsonAsync("/api/webhooks", NewMinimalCreateBody());
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateWebhook_WhenOwnerWithoutFeatureFlag_ShouldReturnForbidden()
    {
        // AuthenticatedOwnerHttpClient has no feature flags set.
        var response = await AuthenticatedOwnerHttpClient.PostAsJsonAsync("/api/webhooks", NewMinimalCreateBody());

        await response.ShouldHaveErrorStatusCode(HttpStatusCode.Forbidden, WebhookAuthorization.WebhooksFeatureDisabledMessage);
    }

    [Fact]
    public async Task CreateWebhook_WhenMemberWithFlag_ShouldReturnForbidden()
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
            FeatureFlags = new HashSet<string> { WebhookAuthorization.WebhooksFeatureFlagKey }
        };
        var memberClient = CreateAuthenticatedHttpClient(memberWithFlag);

        var response = await memberClient.PostAsJsonAsync("/api/webhooks", NewMinimalCreateBody());

        await response.ShouldHaveErrorStatusCode(HttpStatusCode.Forbidden, WebhookAuthorization.ManageWebhooksForbiddenMessage);
    }

    [Fact]
    public async Task CreateWebhook_WhenTargetUrlIsNotAbsoluteHttpUrl_ShouldReturnBadRequest()
    {
        var body = new
        {
            TargetUrl = "not-a-url",
            EventSubscriptions = new[] { WebhookEventType.BookingCreated },
            Active = true,
            EventTypeId = (string?)null
        };

        var response = await _ownerWithFlag.PostAsJsonAsync("/api/webhooks", body);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private static object NewMinimalCreateBody()
    {
        return new
        {
            TargetUrl = "https://example.test/hook",
            EventSubscriptions = new[] { WebhookEventType.BookingCreated },
            Active = true,
            EventTypeId = (string?)null
        };
    }
}
