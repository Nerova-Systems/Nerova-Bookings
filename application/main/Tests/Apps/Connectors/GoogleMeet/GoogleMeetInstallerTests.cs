using FluentAssertions;
using Main.Features.Apps.Connectors.GoogleCalendar;
using Main.Features.Apps.Connectors.GoogleMeet;
using Main.Features.Apps.Domain;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using SharedKernel.Domain;
using Xunit;

namespace Main.Tests.Apps.Connectors.GoogleMeet;

/// <summary>
///     Verifies the Google Meet installer's piggy-back semantics:
///     <list type="bullet">
///         <item>If the user has not installed Google Calendar, Begin/CompleteInstall throw a
///             <see cref="GoogleMeetPrerequisiteMissingException" /> (to be mapped to HTTP 412
///             once the platform install handler grows that translation).</item>
///         <item>If the calendar credential exists, CompleteInstall returns
///             <c>PersistCredential = false</c> with an empty encrypted key — the platform
///             handler creates the AppInstallation row but skips writing a duplicate
///             <see cref="Credential" />.</item>
///     </list>
///     We mock <see cref="IServiceScopeFactory" /> to hand back a substituted
///     <see cref="ICredentialRepository" /> — the installer is a singleton and resolves the
///     repository through a per-call scope.
/// </summary>
public sealed class GoogleMeetInstallerTests
{
    [Fact]
    public async Task BeginInstallAsync_WhenGoogleCalendarNotInstalled_ShouldThrowPrerequisiteMissing()
    {
        var (installer, _) = BuildInstaller(calendarCredential: null);

        var act = async () => await installer.BeginInstallAsync(
            new AppInstallContext(new TenantId(1), new UserId("usr_1"), "u@x", "https://r/callback", "state-1"),
            CancellationToken.None
        );

        await act.Should().ThrowAsync<GoogleMeetPrerequisiteMissingException>();
    }

    [Fact]
    public async Task BeginInstallAsync_WhenGoogleCalendarInstalled_ShouldReturnStubAuthorizeUrlWithState()
    {
        var calendar = Credential.Create(new TenantId(1), new UserId("usr_1"), GoogleCalendarSlug.Slug, "blob");
        var (installer, _) = BuildInstaller(calendar);

        var result = await installer.BeginInstallAsync(
            new AppInstallContext(new TenantId(1), new UserId("usr_1"), "u@x", "https://r/callback", "state-xyz"),
            CancellationToken.None
        );

        result.State.Should().Be("state-xyz");
        result.AuthorizeUrl.Should().StartWith("https://r/callback?");
        result.AuthorizeUrl.Should().Contain("state=state-xyz");
        result.AuthorizeUrl.Should().Contain("code=reuse-google-calendar");
    }

    [Fact]
    public async Task CompleteInstallAsync_WhenGoogleCalendarNotInstalled_ShouldThrowPrerequisiteMissing()
    {
        var (installer, _) = BuildInstaller(calendarCredential: null);

        var act = async () => await installer.CompleteInstallAsync(
            new AppInstallCallbackContext(new TenantId(1), new UserId("usr_1"), "code", "https://r/callback"),
            CancellationToken.None
        );

        await act.Should().ThrowAsync<GoogleMeetPrerequisiteMissingException>();
    }

    [Fact]
    public async Task CompleteInstallAsync_WhenGoogleCalendarInstalled_ShouldReturnNoPersistResult()
    {
        var calendar = Credential.Create(new TenantId(1), new UserId("usr_1"), GoogleCalendarSlug.Slug, "blob");
        var (installer, _) = BuildInstaller(calendar);

        var result = await installer.CompleteInstallAsync(
            new AppInstallCallbackContext(new TenantId(1), new UserId("usr_1"), "code", "https://r/callback"),
            CancellationToken.None
        );

        result.PersistCredential.Should().BeFalse();
        result.EncryptedKey.Should().BeEmpty();
    }

    [Fact]
    public async Task UninstallAsync_ShouldBeNoOp()
    {
        var (installer, repo) = BuildInstaller(calendarCredential: null);

        var act = async () => await installer.UninstallAsync(
            new TenantId(1), new UserId("usr_1"), encryptedKey: "anything", CancellationToken.None
        );

        await act.Should().NotThrowAsync();
        // Crucially: uninstall must NEVER touch the google-calendar credential — Calendar may
        // still be in active use independently.
        repo.DidNotReceiveWithAnyArgs().Remove(default!);
    }

    private static (GoogleMeetInstaller Installer, ICredentialRepository Repository) BuildInstaller(Credential? calendarCredential)
    {
        var repository = Substitute.For<ICredentialRepository>();
        repository.GetForUserAsync(Arg.Any<UserId>(), GoogleCalendarSlug.Slug, Arg.Any<CancellationToken>())
            .Returns(calendarCredential);

        // Build a tiny real container so IServiceScopeFactory hands back a scope whose
        // provider resolves the substituted repository. Cheaper and clearer than mocking the
        // full scope→provider→GetRequiredService chain by hand.
        var services = new ServiceCollection();
        services.AddSingleton(repository);
        var provider = services.BuildServiceProvider();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

        return (new GoogleMeetInstaller(scopeFactory), repository);
    }
}
