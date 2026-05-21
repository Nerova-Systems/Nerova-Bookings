using Account.Features.Permissions.Domain;
using Account.Features.Permissions.Pipeline;
using Account.Features.Smtp.Domain;
using Account.Features.Smtp.Infrastructure;
using JetBrains.Annotations;
using SharedKernel.Cqrs;
using SharedKernel.ExecutionContext;
using FeatureFlagDefinitions = SharedKernel.FeatureFlags.FeatureFlags;

namespace Account.Features.Smtp.Queries;

/// <summary>
///     Returns the SMTP configuration for the active organization.
///     The password field is omitted from the response.
///     Requires <c>Smtp.Read</c> permission at organization scope.
/// </summary>
[PublicAPI]
[RequirePermission(PermissionResource.Smtp, PermissionAction.Read, PermissionScope.Organization)]
public sealed record GetOrgSmtpConfigQuery : IRequest<Result<OrgSmtpConfigResponse>>;

[PublicAPI]
public sealed record OrgSmtpConfigResponse(
    string Id,
    string Host,
    int Port,
    bool UseSsl,
    string Username,
    string FromEmail,
    string? FromName,
    string? ReplyToEmail,
    bool IsEnabled);

public sealed class GetOrgSmtpConfigHandler(
    IOrgSmtpConfigRepository configRepository,
    IExecutionContext executionContext) : IRequestHandler<GetOrgSmtpConfigQuery, Result<OrgSmtpConfigResponse>>
{
    public async Task<Result<OrgSmtpConfigResponse>> Handle(GetOrgSmtpConfigQuery query, CancellationToken cancellationToken)
    {
        if (!executionContext.UserInfo.IsFeatureFlagEnabled(FeatureFlagDefinitions.CapCustomSmtp.Key))
            return Result<OrgSmtpConfigResponse>.Forbidden("The custom SMTP feature is not enabled for this organization.");

        var orgId = executionContext.ActiveOrgId!;
        var config = await configRepository.GetByOrgIdAsync(orgId, cancellationToken);
        if (config is null)
            return Result<OrgSmtpConfigResponse>.NotFound("No SMTP configuration found for this organization.");

        return new OrgSmtpConfigResponse(
            config.Id.ToString(),
            config.Host,
            config.Port,
            config.UseSsl,
            config.Username,
            config.FromEmail,
            config.FromName,
            config.ReplyToEmail,
            config.IsEnabled);
    }
}
