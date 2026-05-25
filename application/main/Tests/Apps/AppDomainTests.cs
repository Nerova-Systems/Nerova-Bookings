using FluentAssertions;
using Main.Features.Apps.Domain;
using SharedKernel.Domain;
using Xunit;

namespace Main.Tests.Apps;

public sealed class AppDomainTests
{
    private static readonly TenantId TenantId = TenantId.NewId();
    private static readonly UserId UserId = UserId.NewId();
    private static readonly AppSlug Slug = new("test-app");

    [Fact]
    public void App_Create_WithValidData_ShouldSucceed()
    {
        var app = App.Create(Slug, "Test App", AppCategory.Calendar, "desc", "https://logo");

        app.Id.Should().Be(Slug);
        app.Name.Should().Be("Test App");
        app.Category.Should().Be(AppCategory.Calendar);
        app.IsActive.Should().BeTrue();
    }

    [Fact]
    public void App_Create_WithBlankSlug_ShouldThrow()
    {
        var act = () => App.Create(new AppSlug(" "), "Name", AppCategory.Other, "", "");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void App_Create_WithBlankName_ShouldThrow()
    {
        var act = () => App.Create(Slug, " ", AppCategory.Other, "", "");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void App_Deactivate_ShouldFlipIsActive()
    {
        var app = App.Create(Slug, "Test App", AppCategory.Other, "", "");
        app.Deactivate();
        app.IsActive.Should().BeFalse();
        app.Activate();
        app.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Credential_Create_WithEmptyKey_ShouldThrow()
    {
        var act = () => Credential.Create(TenantId, UserId, Slug, "");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Credential_UpdateKey_WithEmptyKey_ShouldThrow()
    {
        var credential = Credential.Create(TenantId, UserId, Slug, "key");
        var act = () => credential.UpdateKey("");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Credential_UpdateKey_ShouldRotateEncryptedKey()
    {
        var credential = Credential.Create(TenantId, UserId, Slug, "first");
        credential.UpdateKey("second");
        credential.EncryptedKey.Should().Be("second");
    }
}
