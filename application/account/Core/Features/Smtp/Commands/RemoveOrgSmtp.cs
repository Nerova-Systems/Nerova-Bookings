using Account.Features.Permissions.Domain;
using Account.Features.Permissions.Pipeline;
using Account.Features.Smtp.Domain;
using JetBrains.Annotations;
using SharedKernel.Cqrs;
using SharedKernel.ExecutionContext;
using FeatureFlagDefinitions = SharedKernel.FeatureFlags.FeatureFlags;

namespace Account.Features.Smtp.Commands;

/// <summary>
///     Permanently removes the SMTP configuration for the active organization.
///     After deletion, the platform email client is used for all outbound emails.
///     Requires <c>Smtp.Manage</c> permission at organization scope.
/// </summary>
[PublicAPI]
[RequirePermission(PermissionResource.Smtp, PermissionAction.Manage, PermissionScope.Organization)]
public sealed record RemoveOrgSmtpCommand : ICommand, IRequest<Result>;

public sealed class RemoveOrgSmtpHandler(
    IOrgSmtpConfigRepository configRepository,
    IExecutionContext executionContext) : IRequestHandler<RemoveOrgSmtpCommand, Result>
{
    public async Task<Result> Handle(RemoveOrgSmtpCommand command, CancellationToken cancellationToken)
    {
        if (!executionContext.UserInfo.IsFeatureFlagEnabled(FeatureFlagDefinitions.CapCustomSmtp.Key))
            return Result.Forbidden("The custom SMTP feature is not enabled for this organization.");

        var orgId = executionContext.ActiveOrgId!;
        var config = await configRepository.GetByOrgIdAsync(orgId, cancellationToken);
        if (config is null)
            return Result.NotFound("No SMTP configuration found for this organization.");

        configRepository.Remove(config);
        return Result.Success();
    }
}
