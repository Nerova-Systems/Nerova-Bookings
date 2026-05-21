using Account.Features.AuditLog.Domain;
using Account.Features.DelegationCredentials.Domain;
using Account.Features.DelegationCredentials.Infrastructure;
using Account.Features.Permissions.Domain;
using Account.Features.Permissions.Pipeline;
using Account.Features.Tenants.Domain;
using FluentValidation;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Http;
using SharedKernel.AuditLog;
using SharedKernel.Cqrs;
using SharedKernel.DelegationCredentials;
using SharedKernel.ExecutionContext;
using FeatureFlagDefinitions = SharedKernel.FeatureFlags.FeatureFlags;

namespace Account.Features.DelegationCredentials.Commands.TestDelegationCredential;

/// <summary>
///     Tests the stored delegation credential for <see cref="Platform" /> by attempting a real API call
///     on behalf of <see cref="MemberEmail" />.
///     Records the test outcome on the credential aggregate.
///     Requires <c>OrgSettings.Manage</c> permission at organization scope.
/// </summary>
[PublicAPI]
[RequirePermission(PermissionResource.OrgSettings, PermissionAction.Manage, PermissionScope.Organization)]
public sealed record TestDelegationCredentialCommand : ICommand, IRequest<Result<TestDelegationCredentialResult>>
{
    public required WorkspacePlatform Platform { get; init; }

    /// <summary>An org member email used as the impersonation target during the test.</summary>
    public required string MemberEmail { get; init; }
}

[PublicAPI]
public sealed record TestDelegationCredentialResult(bool Success, string? ErrorMessage);

public sealed class TestDelegationCredentialValidator : AbstractValidator<TestDelegationCredentialCommand>
{
    public TestDelegationCredentialValidator()
    {
        RuleFor(x => x.MemberEmail)
            .NotEmpty()
            .EmailAddress()
            .WithMessage("A valid member email address is required.");
    }
}

public sealed class TestDelegationCredentialHandler(
    IDelegationCredentialRepository credentialRepository,
    DelegationCredentialEncryption encryption,
    IDelegationCredentialTester tester,
    IAuditLogEmitter auditLogEmitter,
    IHttpContextAccessor httpContextAccessor,
    IExecutionContext executionContext,
    TimeProvider timeProvider) : IRequestHandler<TestDelegationCredentialCommand, Result<TestDelegationCredentialResult>>
{
    public async Task<Result<TestDelegationCredentialResult>> Handle(
        TestDelegationCredentialCommand command,
        CancellationToken cancellationToken)
    {
        if (!executionContext.UserInfo.IsFeatureFlagEnabled(FeatureFlagDefinitions.CapDelegationCredentials.Key))
            return Result<TestDelegationCredentialResult>.Forbidden("The delegation credentials feature is not enabled for this organization.");

        var orgId = executionContext.ActiveOrgId!;
        var credential = await credentialRepository.GetByOrgAndPlatformAsync(orgId, command.Platform, cancellationToken);
        if (credential is null)
            return Result<TestDelegationCredentialResult>.NotFound($"No {command.Platform} delegation credential found for this organization.");

        var keyBlob = encryption.Unprotect(credential.EncryptedKeyBlob);
        var testResult = await tester.TestAsync(keyBlob, command.Platform, command.MemberEmail, cancellationToken);

        credential.MarkTestResult(testResult.Success, testResult.Error, timeProvider.GetUtcNow());
        credentialRepository.Update(credential);

        await auditLogEmitter.EmitAsync(new AuditLogEvent(
            TenantId: orgId,
            ActorId: executionContext.UserInfo.Id!,
            ActorEmail: executionContext.UserInfo.Email ?? string.Empty,
            Resource: AuditResource.DelegationCredential.ToString(),
            Action: AuditAction.Tested.ToString(),
            ResourceId: credential.Id.ToString(),
            IpAddress: httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString(),
            UserAgent: httpContextAccessor.HttpContext?.Request.Headers.UserAgent.ToString()
        ), cancellationToken);

        return new TestDelegationCredentialResult(testResult.Success, testResult.Error);
    }
}
