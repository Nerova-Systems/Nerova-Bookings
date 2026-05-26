using System.Net;
using FluentAssertions;
using Main.Database;
using Main.Features.Apps.Domain;
using Main.Features.Apps.Shared;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel.Tests;
using Xunit;

namespace Main.Tests.Apps;

public sealed class AppEndpointsTests : EndpointBaseTest<MainDbContext>
{
    private static readonly AppSlug ActiveSlug = new("test-app");
    private static readonly AppSlug InactiveSlug = new("inactive-app");

    public AppEndpointsTests()
    {
        using var scope = Provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MainDbContext>();
        db.Set<App>().Add(App.Create(ActiveSlug, "Test App", AppCategory.Calendar, "desc", "https://logo"));
        db.Set<App>().Add(App.Create(InactiveSlug, "Inactive", AppCategory.Other, "", "", false));
        db.SaveChanges();
    }

    [Fact]
    public async Task ListApps_AsMember_ShouldReturnSeededApps()
    {
        var response = await AuthenticatedMemberHttpClient.GetAsync("/api/apps");

        response.ShouldBeSuccessfulGetRequest();
        var payload = await response.DeserializeResponse<AppsResponse>();
        payload!.Apps.Should().HaveCount(2);
        payload.Apps.Should().Contain(app => app.Slug == ActiveSlug && !app.IsInstalledForTenant && !app.IsConnectedForUser);
    }

    [Fact]
    public async Task InstallApp_FullLifecycle_ShouldConnectAndDisconnect()
    {
        // Install: returns a stub authorize URL containing the state token
        var installResponse = await AuthenticatedMemberHttpClient.PostAsync($"/api/apps/{ActiveSlug.Value}/install", null);
        installResponse.EnsureSuccessStatusCode();
        var install = await installResponse.DeserializeResponse<InstallAppResponse>();
        install!.State.Should().NotBeNullOrWhiteSpace();
        install.AuthorizeUrl.Should().Contain(install.State);

        // Callback: exchanges state for a credential
        var callbackResponse = await AuthenticatedMemberHttpClient.GetAsync(
            $"/api/apps/{ActiveSlug.Value}/callback?code=auth-code&state={install.State}"
        );
        callbackResponse.ShouldBeSuccessfulGetRequest();
        var callback = await callbackResponse.DeserializeResponse<AppCallbackResponse>();
        callback!.Connected.Should().BeTrue();

        // List now reflects the new state
        var listResponse = await AuthenticatedMemberHttpClient.GetAsync("/api/apps");
        var listed = (await listResponse.DeserializeResponse<AppsResponse>())!.Apps.Single(app => app.Slug == ActiveSlug);
        listed.IsConnectedForUser.Should().BeTrue();
        listed.IsInstalledForTenant.Should().BeTrue();

        // Uninstall
        var uninstallResponse = await AuthenticatedMemberHttpClient.DeleteAsync($"/api/apps/{ActiveSlug.Value}/uninstall");
        uninstallResponse.EnsureSuccessStatusCode();

        var afterUninstall = (await (await AuthenticatedMemberHttpClient.GetAsync("/api/apps")).DeserializeResponse<AppsResponse>())!
            .Apps.Single(app => app.Slug == ActiveSlug);
        afterUninstall.IsConnectedForUser.Should().BeFalse();
        afterUninstall.IsInstalledForTenant.Should().BeFalse();
    }

    [Fact]
    public async Task InstallApp_WhenSlugIsUnknown_ShouldReturnNotFound()
    {
        var response = await AuthenticatedMemberHttpClient.PostAsync("/api/apps/missing/install", null);
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task InstallApp_WhenAppIsInactive_ShouldReturnBadRequest()
    {
        var response = await AuthenticatedMemberHttpClient.PostAsync($"/api/apps/{InactiveSlug.Value}/install", null);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Callback_WhenStateIsInvalid_ShouldReturnBadRequest()
    {
        var response = await AuthenticatedMemberHttpClient.GetAsync(
            $"/api/apps/{ActiveSlug.Value}/callback?code=auth-code&state=bogus-state"
        );
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Callback_WhenStateBelongsToDifferentSlug_ShouldReturnBadRequest()
    {
        // Issue a state for ActiveSlug, then attempt to use it on InactiveSlug
        var install = await (await AuthenticatedMemberHttpClient.PostAsync($"/api/apps/{ActiveSlug.Value}/install", null))
            .DeserializeResponse<InstallAppResponse>();

        var response = await AuthenticatedMemberHttpClient.GetAsync(
            $"/api/apps/{InactiveSlug.Value}/callback?code=auth-code&state={install!.State}"
        );
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
