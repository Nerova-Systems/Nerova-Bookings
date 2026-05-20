using System.Net;
using Account.Database;
using Account.Features.Permissions.Domain;
using Account.Features.Permissions.Pipeline;
using Account.Features.Permissions.Services;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using SharedKernel.Authentication;
using SharedKernel.Cqrs;
using SharedKernel.Domain;
using SharedKernel.ExecutionContext;
using Xunit;

namespace Account.Tests.Permissions;

/// <summary>
///     Unit tests for <see cref="PermissionCheckBehavior{TRequest,TResponse}" />.
///     All dependencies are substituted so these tests run without a database.
/// </summary>
public sealed class PermissionCheckBehaviorTests(AccountWebApplicationFactory factory)
    : EndpointBaseTest<AccountDbContext>(factory), IClassFixture<AccountWebApplicationFactory>
{
    // ── Test request types ────────────────────────────────────────────────────

    // No permissions required — open command
    private sealed record OpenCommand : IRequest<Result>;

    // Requires a single permission
    [RequirePermission(PermissionResource.Billing, PermissionAction.Manage)]
    private sealed record BillingManageCommand : IRequest<Result>;

    // Requires two permissions (AND semantics)
    [RequirePermission(PermissionResource.EventType, PermissionAction.Create)]
    [RequirePermission(PermissionResource.Team, PermissionAction.Read)]
    private sealed record TwoPermissionsCommand : IRequest<Result>;

    // Requires Team scope
    [RequirePermission(PermissionResource.Team, PermissionAction.Manage, PermissionScope.Team)]
    private sealed record TeamScopedCommand : IRequest<Result>;

    // Requires Organization scope
    [RequirePermission(PermissionResource.Team, PermissionAction.Manage, PermissionScope.Organization)]
    private sealed record OrgScopedCommand : IRequest<Result>;

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static (IPermissionCheckService permissionService, IExecutionContext executionContext) BuildMocks(
        UserId? userId = null,
        TenantId? tenantId = null,
        TenantId? activeTeamId = null,
        TenantId? activeOrgId = null)
    {
        var permissionService = Substitute.For<IPermissionCheckService>();
        var executionContext = Substitute.For<IExecutionContext>();

        executionContext.UserInfo.Returns(new UserInfo { Id = userId });
        executionContext.TenantId.Returns(tenantId);
        executionContext.ActiveTeamId.Returns(activeTeamId);
        executionContext.ActiveOrgId.Returns(activeOrgId);

        return (permissionService, executionContext);
    }

    private static PermissionCheckBehavior<TRequest, Result> BuildBehavior<TRequest>(
        IPermissionCheckService permissionService,
        IExecutionContext executionContext)
        where TRequest : class
    {
        return new PermissionCheckBehavior<TRequest, Result>(
            permissionService,
            executionContext,
            NullLogger<PermissionCheckBehavior<TRequest, Result>>.Instance);
    }

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenNoPermissionsRequired_ShouldPassThrough()
    {
        // Arrange
        var (permissionService, executionContext) = BuildMocks();
        var behavior = BuildBehavior<OpenCommand>(permissionService, executionContext);

        var handlerCalled = false;
        Task<Result> next(CancellationToken _)
        {
            handlerCalled = true;
            return Task.FromResult(Result.Success());
        }

        // Act
        var result = await behavior.Handle(new OpenCommand(), next, CancellationToken.None);

        // Assert
        handlerCalled.Should().BeTrue();
        result.IsSuccess.Should().BeTrue();
        await permissionService.DidNotReceiveWithAnyArgs().HasPermissionAsync(default!, default!, default!, default);
    }

    [Fact]
    public async Task Handle_WhenUnauthenticated_ShouldReturnForbidden()
    {
        // Arrange — null userId and tenantId to simulate unauthenticated request
        var (permissionService, executionContext) = BuildMocks(userId: null, tenantId: null);
        var behavior = BuildBehavior<BillingManageCommand>(permissionService, executionContext);

        var handlerCalled = false;
        Task<Result> next(CancellationToken _)
        {
            handlerCalled = true;
            return Task.FromResult(Result.Success());
        }

        // Act
        var result = await behavior.Handle(new BillingManageCommand(), next, CancellationToken.None);

        // Assert
        handlerCalled.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        await permissionService.DidNotReceiveWithAnyArgs().HasPermissionAsync(default!, default!, default!, default);
    }

    [Fact]
    public async Task Handle_WhenPermissionGranted_ShouldPassThrough()
    {
        // Arrange
        var userId = UserId.NewId();
        var tenantId = new TenantId(1000L);
        var (permissionService, executionContext) = BuildMocks(userId, tenantId);

        permissionService
            .HasPermissionAsync(userId, tenantId, new Permission(PermissionResource.Billing, PermissionAction.Manage), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(true));

        var behavior = BuildBehavior<BillingManageCommand>(permissionService, executionContext);

        var handlerCalled = false;
        Task<Result> next(CancellationToken _)
        {
            handlerCalled = true;
            return Task.FromResult(Result.Success());
        }

        // Act
        var result = await behavior.Handle(new BillingManageCommand(), next, CancellationToken.None);

        // Assert
        handlerCalled.Should().BeTrue();
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WhenPermissionDenied_ShouldReturnForbidden()
    {
        // Arrange
        var userId = UserId.NewId();
        var tenantId = new TenantId(1001L);
        var (permissionService, executionContext) = BuildMocks(userId, tenantId);

        permissionService
            .HasPermissionAsync(userId, tenantId, new Permission(PermissionResource.Billing, PermissionAction.Manage), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(false));

        var behavior = BuildBehavior<BillingManageCommand>(permissionService, executionContext);

        var handlerCalled = false;
        Task<Result> next(CancellationToken _)
        {
            handlerCalled = true;
            return Task.FromResult(Result.Success());
        }

        // Act
        var result = await behavior.Handle(new BillingManageCommand(), next, CancellationToken.None);

        // Assert
        handlerCalled.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Handle_WhenOneOfTwoPermissionsDenied_ShouldReturnForbidden()
    {
        // Arrange — second permission (Team.Read) denied; EventType.Create granted
        var userId = UserId.NewId();
        var tenantId = new TenantId(1002L);
        var (permissionService, executionContext) = BuildMocks(userId, tenantId);

        permissionService
            .HasPermissionAsync(userId, tenantId, new Permission(PermissionResource.EventType, PermissionAction.Create), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(true));

        permissionService
            .HasPermissionAsync(userId, tenantId, new Permission(PermissionResource.Team, PermissionAction.Read), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(false));

        var behavior = BuildBehavior<TwoPermissionsCommand>(permissionService, executionContext);

        var handlerCalled = false;
        Task<Result> next(CancellationToken _)
        {
            handlerCalled = true;
            return Task.FromResult(Result.Success());
        }

        // Act
        var result = await behavior.Handle(new TwoPermissionsCommand(), next, CancellationToken.None);

        // Assert
        handlerCalled.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── Scope-aware permission tests ──────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenTeamScopeAndActiveTeamIdSet_ShouldCheckAgainstTeamTenant()
    {
        // Arrange
        var userId = UserId.NewId();
        var tenantId = new TenantId(2000L);
        var teamId = new TenantId(2001L);
        var (permissionService, executionContext) = BuildMocks(userId, tenantId, activeTeamId: teamId);

        permissionService
            .HasPermissionAsync(userId, teamId, new Permission(PermissionResource.Team, PermissionAction.Manage), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(true));

        var behavior = BuildBehavior<TeamScopedCommand>(permissionService, executionContext);

        var handlerCalled = false;
        Task<Result> next(CancellationToken _)
        {
            handlerCalled = true;
            return Task.FromResult(Result.Success());
        }

        // Act
        var result = await behavior.Handle(new TeamScopedCommand(), next, CancellationToken.None);

        // Assert
        handlerCalled.Should().BeTrue();
        result.IsSuccess.Should().BeTrue();
        await permissionService.DidNotReceive().HasPermissionAsync(userId, tenantId, Arg.Any<Permission>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenTeamScopeAndActiveTeamIdIsNull_ShouldReturnForbidden()
    {
        // Arrange — no active team scope in context
        var userId = UserId.NewId();
        var tenantId = new TenantId(2002L);
        var (permissionService, executionContext) = BuildMocks(userId, tenantId, activeTeamId: null);

        var behavior = BuildBehavior<TeamScopedCommand>(permissionService, executionContext);

        var handlerCalled = false;
        Task<Result> next(CancellationToken _)
        {
            handlerCalled = true;
            return Task.FromResult(Result.Success());
        }

        // Act
        var result = await behavior.Handle(new TeamScopedCommand(), next, CancellationToken.None);

        // Assert
        handlerCalled.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        await permissionService.DidNotReceiveWithAnyArgs().HasPermissionAsync(default!, default!, default!, default);
    }

    [Fact]
    public async Task Handle_WhenOrgScopeAndActiveOrgIdSet_ShouldCheckAgainstOrgTenant()
    {
        // Arrange
        var userId = UserId.NewId();
        var tenantId = new TenantId(3000L);
        var orgId = new TenantId(3001L);
        var (permissionService, executionContext) = BuildMocks(userId, tenantId, activeOrgId: orgId);

        permissionService
            .HasPermissionAsync(userId, orgId, new Permission(PermissionResource.Team, PermissionAction.Manage), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(true));

        var behavior = BuildBehavior<OrgScopedCommand>(permissionService, executionContext);

        var handlerCalled = false;
        Task<Result> next(CancellationToken _)
        {
            handlerCalled = true;
            return Task.FromResult(Result.Success());
        }

        // Act
        var result = await behavior.Handle(new OrgScopedCommand(), next, CancellationToken.None);

        // Assert
        handlerCalled.Should().BeTrue();
        result.IsSuccess.Should().BeTrue();
        await permissionService.DidNotReceive().HasPermissionAsync(userId, tenantId, Arg.Any<Permission>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenOrgScopeAndActiveOrgIdIsNull_ShouldReturnForbidden()
    {
        // Arrange — no active org scope in context
        var userId = UserId.NewId();
        var tenantId = new TenantId(3002L);
        var (permissionService, executionContext) = BuildMocks(userId, tenantId, activeOrgId: null);

        var behavior = BuildBehavior<OrgScopedCommand>(permissionService, executionContext);

        var handlerCalled = false;
        Task<Result> next(CancellationToken _)
        {
            handlerCalled = true;
            return Task.FromResult(Result.Success());
        }

        // Act
        var result = await behavior.Handle(new OrgScopedCommand(), next, CancellationToken.None);

        // Assert
        handlerCalled.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        await permissionService.DidNotReceiveWithAnyArgs().HasPermissionAsync(default!, default!, default!, default);
    }
}
