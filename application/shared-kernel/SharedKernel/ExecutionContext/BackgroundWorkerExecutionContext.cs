using System.Net;
using SharedKernel.Authentication;
using SharedKernel.Domain;

namespace SharedKernel.ExecutionContext;

public class BackgroundWorkerExecutionContext(TenantId? tenantId = null, UserInfo? userInfo = null)
    : IExecutionContext
{
    public TenantId? TenantId { get; } = tenantId;

    public TenantId? ActiveTeamId => UserInfo.ActiveTeamId;

    public TenantId? ActiveOrgId => UserInfo.ActiveOrgId;

    public string? ActiveOrgProfileId => UserInfo.ActiveOrgProfileId;

    public UserInfo UserInfo { get; } = userInfo ?? UserInfo.System;

    public IPAddress ClientIpAddress { get; } = IPAddress.None;
}
