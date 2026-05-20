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

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static (IPermissionCheckService permissionService, IExecutionContext executionContext) BuildMocks(
        UserId? userId = null,
        TenantId? tenantId = null)
    {
        var permissionService = Substitute.For<IPermissionCheckService>();
        var executionContext = Substitute.For<IExecutionContext>();

        executionContext.UserInfo.Returns(new UserInfo { Id = userId });
        executionContext.TenantId.Returns(tenantId);

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
}
