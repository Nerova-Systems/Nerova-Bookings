using FluentAssertions;
using Main.Features.Insights.Shared;
using NSubstitute;
using SharedKernel.Authentication;
using SharedKernel.Domain;
using SharedKernel.ExecutionContext;
using Xunit;

namespace Main.Tests.Insights;

/// <summary>
///     Pure unit tests for <see cref="InsightsScopeResolver" />.
///     No HTTP, no database — IExecutionContext is mocked inline with NSubstitute.
/// </summary>
public sealed class InsightsScopeResolverTests
{
    private readonly IExecutionContext _context = Substitute.For<IExecutionContext>();
    private readonly InsightsScopeResolver _sut;

    public InsightsScopeResolverTests()
    {
        _sut = new InsightsScopeResolver(_context);
    }

    [Fact]
    public void TryResolve_WhenAuthenticatedSoloUser_ShouldReturnScopeWithNullTeamId()
    {
        var tenantId = new TenantId(42);
        var userId = UserId.NewId();
        _context.UserInfo.Returns(new UserInfo { TenantId = tenantId, Id = userId, IsAuthenticated = true });
        _context.ActiveTeamId.Returns((TenantId?)null);

        var scope = _sut.TryResolve();

        scope.Should().NotBeNull();
        scope.TenantId.Should().Be(tenantId);
        scope.UserId.Should().Be(userId);
        scope.TeamId.Should().BeNull();
    }

    [Fact]
    public void TryResolve_WhenTeamSessionActive_ShouldReturnScopeWithTeamId()
    {
        var tenantId = new TenantId(42);
        var teamId = new TenantId(99);
        var userId = UserId.NewId();
        _context.UserInfo.Returns(new UserInfo { TenantId = tenantId, Id = userId, IsAuthenticated = true });
        _context.ActiveTeamId.Returns(teamId);

        var scope = _sut.TryResolve();

        scope!.TeamId.Should().Be(teamId);
    }

    [Fact]
    public void TryResolve_WhenUserIdIsNull_ShouldReturnNull()
    {
        _context.UserInfo.Returns(new UserInfo { TenantId = new TenantId(42), Id = null, IsAuthenticated = false });

        var scope = _sut.TryResolve();

        scope.Should().BeNull();
    }

    [Fact]
    public void TryResolve_WhenTenantIdIsNull_ShouldReturnNull()
    {
        _context.UserInfo.Returns(new UserInfo { TenantId = null, Id = UserId.NewId(), IsAuthenticated = true });

        var scope = _sut.TryResolve();

        scope.Should().BeNull();
    }

    [Fact]
    public void HasInsightsAccess_WhenFlagEnabled_ShouldReturnTrue()
    {
        _context.UserInfo.Returns(new UserInfo
            {
                IsAuthenticated = true,
                FeatureFlags = new HashSet<string> { InsightsAuthorization.InsightsFeatureFlagKey }
            }
        );

        _sut.HasInsightsAccess().Should().BeTrue();
    }

    [Fact]
    public void HasInsightsAccess_WhenFlagAbsent_ShouldReturnFalse()
    {
        _context.UserInfo.Returns(new UserInfo { IsAuthenticated = true, FeatureFlags = new HashSet<string>() });

        _sut.HasInsightsAccess().Should().BeFalse();
    }
}
