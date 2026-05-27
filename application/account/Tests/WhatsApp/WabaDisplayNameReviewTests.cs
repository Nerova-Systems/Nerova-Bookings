using Account.Features.WhatsApp.Domain;
using FluentAssertions;
using SharedKernel.Domain;
using Xunit;

namespace Account.Tests.WhatsApp;

/// <summary>
///     Unit coverage for the Phase 7c display-name review state transitions on
///     <see cref="WabaConfiguration" />. The state machine is small but every transition is
///     observable from the UI (pending banner, approved toast, declined error), so each rule gets
///     its own row to localise regressions.
/// </summary>
public sealed class WabaDisplayNameReviewTests
{
    private static readonly TenantId TenantId = new(7700);
    private static readonly DateTimeOffset Now = new(2026, 6, 1, 12, 0, 0, TimeSpan.Zero);

    private static WabaConfiguration NewLinkedConfig()
    {
        var config = WabaConfiguration.Create(TenantId, "waba_1", "phone_1", "+27 11 000 0000");
        config.SetWabaAccessToken("token_1");
        return config;
    }

    [Fact]
    public void RequestDisplayNameChange_FromNone_TransitionsToPendingReview()
    {
        var config = NewLinkedConfig();

        config.RequestDisplayNameChange("Acme Studio", Now);

        config.DisplayNameStatus.Should().Be(WabaDisplayNameStatus.PendingReview);
        config.RequestedDisplayName.Should().Be("Acme Studio");
        config.DisplayNameReviewRequestedAt.Should().Be(Now);
    }

    [Fact]
    public void RequestDisplayNameChange_WhilePendingReview_Throws()
    {
        var config = NewLinkedConfig();
        config.RequestDisplayNameChange("Acme Studio", Now);

        var act = () => config.RequestDisplayNameChange("Acme Studios", Now.AddHours(1));

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void RequestDisplayNameChange_AfterDeclined_AllowedAgain()
    {
        var config = NewLinkedConfig();
        config.RequestDisplayNameChange("Acme", Now);
        config.MarkDisplayNameReviewResult(MetaNameStatus.DECLINED, verifiedName: null, Now.AddDays(2));

        config.RequestDisplayNameChange("Acme Studio", Now.AddDays(3));

        config.DisplayNameStatus.Should().Be(WabaDisplayNameStatus.PendingReview);
        config.RequestedDisplayName.Should().Be("Acme Studio");
    }

    [Theory]
    [InlineData(MetaNameStatus.APPROVED, WabaDisplayNameStatus.Approved)]
    [InlineData(MetaNameStatus.AVAILABLE_WITHOUT_REVIEW, WabaDisplayNameStatus.Approved)]
    [InlineData(MetaNameStatus.DECLINED, WabaDisplayNameStatus.Declined)]
    [InlineData(MetaNameStatus.EXPIRED, WabaDisplayNameStatus.Expired)]
    [InlineData(MetaNameStatus.PENDING_REVIEW, WabaDisplayNameStatus.PendingReview)]
    [InlineData(MetaNameStatus.NONE, WabaDisplayNameStatus.None)]
    public void MarkDisplayNameReviewResult_MapsMetaStatusToLocalStatus(
        MetaNameStatus metaStatus, WabaDisplayNameStatus expected)
    {
        var config = NewLinkedConfig();
        config.RequestDisplayNameChange("Acme Studio", Now);

        config.MarkDisplayNameReviewResult(metaStatus, verifiedName: "Acme Studio", Now.AddDays(2));

        config.DisplayNameStatus.Should().Be(expected);
        config.VerifiedName.Should().Be("Acme Studio");
        config.DisplayNameLastCheckedAt.Should().Be(Now.AddDays(2));
    }

    [Fact]
    public void RequestDisplayNameChange_EmptyName_Throws()
    {
        var config = NewLinkedConfig();

        var act = () => config.RequestDisplayNameChange("   ", Now);

        act.Should().Throw<ArgumentException>();
    }
}
