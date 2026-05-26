using System.Net;
using FluentAssertions;
using Main.Features.Permissions.Domain;
using Main.Features.Permissions.Pipeline;
using Main.Features.Permissions.Services;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using SharedKernel.Authentication;
using SharedKernel.Cqrs;
using SharedKernel.Domain;
using SharedKernel.ExecutionContext;
using Xunit;

namespace Main.Tests.Permissions;

/// <summary>
///     Unit tests for <see cref="PermissionCheckBehavior{TRequest,TResponse}" /> in the main SCS.
///     Mirrors <c>Account.Tests.Permissions.PermissionCheckBehaviorTests</c>. All dependencies are
///     substituted so these tests run without a database.
/// </summary>
public sealed class PermissionCheckBehaviorTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static (IPermissionCheckService permissionService, IExecutionContext executionContext) BuildMocks(
        UserId? userId = null,
        string? role = null,
        TenantId? tenantId = null,
        TenantId? activeTeamId = null,
        TenantId? activeOrgId = null)
    {
        var permissionService = Substitute.For<IPermissionCheckService>();
        var executionContext = Substitute.For<IExecutionContext>();

        executionContext.UserInfo.Returns(new UserInfo { Id = userId, Role = role });
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
            NullLogger<PermissionCheckBehavior<TRequest, Result>>.Instance
        );
    }

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenNoPermissionsRequired_ShouldPassThrough()
    {
        // Arrange — public-booker style command with no [RequirePermission] attributes
        var (permissionService, executionContext) = BuildMocks();
        var behavior = BuildBehavior<OpenCommand>(permissionService, executionContext);

        var handlerCalled = false;

        Task<Result> Next(CancellationToken _)
        {
            handlerCalled = true;
            return Task.FromResult(Result.Success());
        }

        // Act
        var result = await behavior.Handle(new OpenCommand(), Next, CancellationToken.None);

        // Assert
        handlerCalled.Should().BeTrue();
        result.IsSuccess.Should().BeTrue();
        permissionService.DidNotReceiveWithAnyArgs().HasPermission(null!, null!, null!);
    }

    [Fact]
    public async Task Handle_WhenUnauthenticated_ShouldReturnForbidden()
    {
        // Arrange — null userId simulates unauthenticated request
        var (permissionService, executionContext) = BuildMocks();
        var behavior = BuildBehavior<BookingCreateCommand>(permissionService, executionContext);

        var handlerCalled = false;

        Task<Result> Next(CancellationToken _)
        {
            handlerCalled = true;
            return Task.FromResult(Result.Success());
        }

        // Act
        var result = await behavior.Handle(new BookingCreateCommand(), Next, CancellationToken.None);

        // Assert
        handlerCalled.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        permissionService.DidNotReceiveWithAnyArgs().HasPermission(null!, null!, null!);
    }

    [Fact]
    public async Task Handle_WhenPermissionGranted_ShouldPassThrough()
    {
        // Arrange
        var userId = UserId.NewId();
        var tenantId = new TenantId(1000L);
        var (permissionService, executionContext) = BuildMocks(userId, SystemRoles.Member, tenantId);

        permissionService
            .HasPermission(Arg.Any<UserInfo>(), tenantId, new Permission(PermissionResource.Booking, PermissionAction.Create))
            .Returns(true);

        var behavior = BuildBehavior<BookingCreateCommand>(permissionService, executionContext);

        var handlerCalled = false;

        Task<Result> Next(CancellationToken _)
        {
            handlerCalled = true;
            return Task.FromResult(Result.Success());
        }

        // Act
        var result = await behavior.Handle(new BookingCreateCommand(), Next, CancellationToken.None);

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
        var (permissionService, executionContext) = BuildMocks(userId, SystemRoles.Member, tenantId);

        permissionService
            .HasPermission(Arg.Any<UserInfo>(), tenantId, new Permission(PermissionResource.Booking, PermissionAction.Create))
            .Returns(false);

        var behavior = BuildBehavior<BookingCreateCommand>(permissionService, executionContext);

        var handlerCalled = false;

        Task<Result> Next(CancellationToken _)
        {
            handlerCalled = true;
            return Task.FromResult(Result.Success());
        }

        // Act
        var result = await behavior.Handle(new BookingCreateCommand(), Next, CancellationToken.None);

        // Assert
        handlerCalled.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Handle_WhenOneOfTwoPermissionsDenied_ShouldReturnForbidden()
    {
        // Arrange — Booking.Create granted; EventType.Read denied (AND semantics)
        var userId = UserId.NewId();
        var tenantId = new TenantId(1002L);
        var (permissionService, executionContext) = BuildMocks(userId, SystemRoles.Member, tenantId);

        permissionService
            .HasPermission(Arg.Any<UserInfo>(), tenantId, new Permission(PermissionResource.Booking, PermissionAction.Create))
            .Returns(true);

        permissionService
            .HasPermission(Arg.Any<UserInfo>(), tenantId, new Permission(PermissionResource.EventType, PermissionAction.Read))
            .Returns(false);

        var behavior = BuildBehavior<TwoPermissionsCommand>(permissionService, executionContext);

        var handlerCalled = false;

        Task<Result> Next(CancellationToken _)
        {
            handlerCalled = true;
            return Task.FromResult(Result.Success());
        }

        // Act
        var result = await behavior.Handle(new TwoPermissionsCommand(), Next, CancellationToken.None);

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
        var (permissionService, executionContext) = BuildMocks(userId, SystemRoles.Admin, tenantId, teamId);

        permissionService
            .HasPermission(Arg.Any<UserInfo>(), teamId, new Permission(PermissionResource.EventType, PermissionAction.Manage))
            .Returns(true);

        var behavior = BuildBehavior<TeamScopedCommand>(permissionService, executionContext);

        var handlerCalled = false;

        Task<Result> Next(CancellationToken _)
        {
            handlerCalled = true;
            return Task.FromResult(Result.Success());
        }

        // Act
        var result = await behavior.Handle(new TeamScopedCommand(), Next, CancellationToken.None);

        // Assert
        handlerCalled.Should().BeTrue();
        result.IsSuccess.Should().BeTrue();
        permissionService.DidNotReceive().HasPermission(Arg.Any<UserInfo>(), tenantId, Arg.Any<Permission>());
    }

    [Fact]
    public async Task Handle_WhenTeamScopeAndActiveTeamIdIsNull_ShouldReturnForbidden()
    {
        // Arrange — no active team scope in context
        var userId = UserId.NewId();
        var tenantId = new TenantId(2002L);
        var (permissionService, executionContext) = BuildMocks(userId, SystemRoles.Admin, tenantId);

        var behavior = BuildBehavior<TeamScopedCommand>(permissionService, executionContext);

        var handlerCalled = false;

        Task<Result> Next(CancellationToken _)
        {
            handlerCalled = true;
            return Task.FromResult(Result.Success());
        }

        // Act
        var result = await behavior.Handle(new TeamScopedCommand(), Next, CancellationToken.None);

        // Assert
        handlerCalled.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        permissionService.DidNotReceiveWithAnyArgs().HasPermission(null!, null!, null!);
    }

    [Fact]
    public async Task Handle_WhenOrgScopeAndActiveOrgIdSet_ShouldCheckAgainstOrgTenant()
    {
        // Arrange
        var userId = UserId.NewId();
        var tenantId = new TenantId(3000L);
        var orgId = new TenantId(3001L);
        var (permissionService, executionContext) = BuildMocks(userId, SystemRoles.Admin, tenantId, activeOrgId: orgId);

        permissionService
            .HasPermission(Arg.Any<UserInfo>(), orgId, new Permission(PermissionResource.Schedule, PermissionAction.Manage))
            .Returns(true);

        var behavior = BuildBehavior<OrgScopedCommand>(permissionService, executionContext);

        var handlerCalled = false;

        Task<Result> Next(CancellationToken _)
        {
            handlerCalled = true;
            return Task.FromResult(Result.Success());
        }

        // Act
        var result = await behavior.Handle(new OrgScopedCommand(), Next, CancellationToken.None);

        // Assert
        handlerCalled.Should().BeTrue();
        result.IsSuccess.Should().BeTrue();
        permissionService.DidNotReceive().HasPermission(Arg.Any<UserInfo>(), tenantId, Arg.Any<Permission>());
    }

    [Fact]
    public async Task Handle_WhenOrgScopeAndActiveOrgIdIsNull_ShouldReturnForbidden()
    {
        // Arrange — no active org scope in context
        var userId = UserId.NewId();
        var tenantId = new TenantId(3002L);
        var (permissionService, executionContext) = BuildMocks(userId, SystemRoles.Admin, tenantId, activeOrgId: null);

        var behavior = BuildBehavior<OrgScopedCommand>(permissionService, executionContext);

        var handlerCalled = false;

        Task<Result> Next(CancellationToken _)
        {
            handlerCalled = true;
            return Task.FromResult(Result.Success());
        }

        // Act
        var result = await behavior.Handle(new OrgScopedCommand(), Next, CancellationToken.None);

        // Assert
        handlerCalled.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        permissionService.DidNotReceiveWithAnyArgs().HasPermission(null!, null!, null!);
    }

    [Fact]
    public async Task Handle_WhenPublicEndpointWithoutAttribute_ShouldBypassEvenWhenUnauthenticated()
    {
        // Arrange — public booker flow: no [RequirePermission] AND no authenticated user.
        // The behaviour must not gate the request; public booking endpoints rely on this.
        var (permissionService, executionContext) = BuildMocks();
        var behavior = BuildBehavior<PublicBookerCommand>(permissionService, executionContext);

        var handlerCalled = false;

        Task<Result> Next(CancellationToken _)
        {
            handlerCalled = true;
            return Task.FromResult(Result.Success());
        }

        // Act
        var result = await behavior.Handle(new PublicBookerCommand(), Next, CancellationToken.None);

        // Assert
        handlerCalled.Should().BeTrue();
        result.IsSuccess.Should().BeTrue();
        permissionService.DidNotReceiveWithAnyArgs().HasPermission(null!, null!, null!);
    }

    // ── Test request types ────────────────────────────────────────────────────

    // No permissions required — open command
    private sealed record OpenCommand : IRequest<Result>;

    // Represents a public-booker command (no attribute, unauthenticated allowed)
    private sealed record PublicBookerCommand : IRequest<Result>;

    // Requires a single booking-create permission
    [RequirePermission(PermissionResource.Booking, PermissionAction.Create)]
    private sealed record BookingCreateCommand : IRequest<Result>;

    // Requires two permissions (AND semantics)
    [RequirePermission(PermissionResource.Booking, PermissionAction.Create)]
    [RequirePermission(PermissionResource.EventType, PermissionAction.Read)]
    private sealed record TwoPermissionsCommand : IRequest<Result>;

    // Requires Team scope
    [RequirePermission(PermissionResource.EventType, PermissionAction.Manage, PermissionScope.Team)]
    private sealed record TeamScopedCommand : IRequest<Result>;

    // Requires Organization scope
    [RequirePermission(PermissionResource.Schedule, PermissionAction.Manage, PermissionScope.Organization)]
    private sealed record OrgScopedCommand : IRequest<Result>;
}
