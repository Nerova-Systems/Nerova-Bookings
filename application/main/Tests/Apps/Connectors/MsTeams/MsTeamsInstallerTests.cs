using FluentAssertions;
using Main.Features.Apps.Connectors.MsTeams;
using Main.Features.Apps.Connectors.Office365Calendar;
using Main.Features.Apps.Domain;
using Main.Features.Apps.Infrastructure;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using SharedKernel.Domain;
using Xunit;

namespace Main.Tests.Apps.Connectors.MsTeams;

/// <summary>
///     Verifies the MS Teams installer's piggy-back semantics on the Office 365 Calendar
///     credential:
///     <list type="bullet">
///         <item>No Office 365 credential ⇒ <see cref="MsTeamsPrerequisiteMissingException" />.</item>
///         <item>Office 365 credential exists but its stored scope blob does NOT include
///             <c>OnlineMeetings.ReadWrite</c> (user installed Calendar before the scope was
///             requested) ⇒ <see cref="MsTeamsPrerequisiteMissingException" /> — surfaces a
///             "reconnect Calendar" hint rather than silently failing meeting creation later.</item>
///         <item>Credential exists with the scope ⇒ <c>BeginInstall</c> returns a stub callback
///             URL; <c>CompleteInstall</c> returns <c>PersistCredential=false</c> with an empty
///             encrypted key — platform handler creates the AppInstallation only.</item>
///         <item>Uninstall is a no-op and never touches the Office 365 credential.</item>
///     </list>
///     Mirrors <c>GoogleMeetInstallerTests</c>: a tiny real <see cref="ServiceCollection" />
///     hands back a scope whose provider resolves the substituted
///     <see cref="ICredentialRepository" /> and a real <see cref="CredentialProtector" /> over
///     an ephemeral data-protection provider.
/// </summary>
public sealed class MsTeamsInstallerTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task BeginInstallAsync_WhenOffice365CalendarNotInstalled_ShouldThrowPrerequisiteMissing()
    {
        var (installer, _) = BuildInstaller(scope: null);

        var act = async () => await installer.BeginInstallAsync(
            new AppInstallContext(new TenantId(1), new UserId("usr_1"), "u@x", "https://r/callback", "state-1"),
            CancellationToken.None
        );

        await act.Should().ThrowAsync<MsTeamsPrerequisiteMissingException>();
    }

    [Fact]
    public async Task BeginInstallAsync_WhenStoredScopeMissingOnlineMeetings_ShouldThrowPrerequisiteMissing()
    {
        // User installed Office 365 Calendar BEFORE we started requesting OnlineMeetings.ReadWrite —
        // we can't silently upgrade consent (Microsoft requires a fresh authorize round trip), so
        // we surface a clean "reconnect" error instead of letting meeting creation fail at runtime.
        var (installer, _) = BuildInstaller(scope: "offline_access Calendars.ReadWrite");

        var act = async () => await installer.BeginInstallAsync(
            new AppInstallContext(new TenantId(1), new UserId("usr_1"), "u@x", "https://r/callback", "state-1"),
            CancellationToken.None
        );

        await act.Should().ThrowAsync<MsTeamsPrerequisiteMissingException>();
    }

    [Fact]
    public async Task BeginInstallAsync_WhenOffice365CredentialGrantsOnlineMeetings_ShouldReturnStubCallback()
    {
        var (installer, _) = BuildInstaller(scope: "offline_access Calendars.ReadWrite OnlineMeetings.ReadWrite");

        var result = await installer.BeginInstallAsync(
            new AppInstallContext(new TenantId(1), new UserId("usr_1"), "u@x", "https://r/callback", "state-xyz"),
            CancellationToken.None
        );

        result.State.Should().Be("state-xyz");
        result.AuthorizeUrl.Should().StartWith("https://r/callback?");
        result.AuthorizeUrl.Should().Contain("state=state-xyz");
        result.AuthorizeUrl.Should().Contain("code=reuse-office365-calendar");
    }

    [Fact]
    public async Task CompleteInstallAsync_WhenPrerequisitesSatisfied_ShouldReturnNoPersistResult()
    {
        var (installer, _) = BuildInstaller(scope: "offline_access Calendars.ReadWrite OnlineMeetings.ReadWrite");

        var result = await installer.CompleteInstallAsync(
            new AppInstallCallbackContext(new TenantId(1), new UserId("usr_1"), "code", "https://r/callback"),
            CancellationToken.None
        );

        result.PersistCredential.Should().BeFalse();
        result.EncryptedKey.Should().BeEmpty();
    }

    [Fact]
    public async Task UninstallAsync_ShouldBeNoOpAndNotTouchOffice365Credential()
    {
        var (installer, repo) = BuildInstaller(scope: null);

        var act = async () => await installer.UninstallAsync(
            new TenantId(1), new UserId("usr_1"), encryptedKey: "anything", CancellationToken.None
        );

        await act.Should().NotThrowAsync();
        // Critically: uninstall must NEVER touch the office365-calendar credential — Calendar
        // may still be in active use independently.
        repo.DidNotReceiveWithAnyArgs().Remove(default!);
    }

    [Theory]
    [InlineData("offline_access Calendars.ReadWrite OnlineMeetings.ReadWrite", true)]
    [InlineData("OnlineMeetings.ReadWrite", true)]
    [InlineData("  OnlineMeetings.ReadWrite  Calendars.ReadWrite ", true)]
    [InlineData("onlinemeetings.readwrite", true)] // Microsoft is case-insensitive on scope strings
    [InlineData("offline_access Calendars.ReadWrite", false)]
    [InlineData("OnlineMeetings.Read", false)] // narrower scope — not enough for create
    [InlineData("", false)]
    [InlineData(null, false)]
    public void HasOnlineMeetingsScope_ShouldMatchTokensCaseInsensitively(string? scope, bool expected)
    {
        MsTeamsInstaller.HasOnlineMeetingsScope(scope).Should().Be(expected);
    }

    private static (MsTeamsInstaller Installer, ICredentialRepository Repository) BuildInstaller(string? scope)
    {
        // Build a fresh protector that the installer will resolve from its scope and use to
        // Unprotect→FromJson the credential blob — exercise the real production path.
        var protector = new CredentialProtector(new EphemeralDataProtectionProvider());

        Credential? credential = null;
        if (scope is not null)
        {
            var blob = new Office365CredentialBlob("atk", "rtk", Now.AddHours(1), scope, UserPrincipalName: null);
            credential = Credential.Create(new TenantId(1), new UserId("usr_1"), Office365CalendarSlug.Slug, protector.Protect(blob.ToJson()));
        }

        var repository = Substitute.For<ICredentialRepository>();
        repository.GetForUserAsync(Arg.Any<UserId>(), Office365CalendarSlug.Slug, Arg.Any<CancellationToken>())
            .Returns(credential);

        var services = new ServiceCollection();
        services.AddSingleton(repository);
        services.AddSingleton(protector);
        var provider = services.BuildServiceProvider();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

        return (new MsTeamsInstaller(scopeFactory), repository);
    }
}
