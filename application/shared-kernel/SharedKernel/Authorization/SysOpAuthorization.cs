using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using SharedKernel.Platform;

namespace SharedKernel.Authorization;

public static class SysOpAuthorization
{
    public const string PolicyName = "SysOp";

    public static void AddPolicy(AuthorizationOptions options)
    {
        options.AddPolicy(PolicyName, policy => policy.RequireAssertion(context =>
            {
                var email = context.User.FindFirstValue(ClaimTypes.Email);
                return email is not null && email.EndsWith(Settings.Current.Identity.InternalEmailDomain, StringComparison.OrdinalIgnoreCase);
            }
        ));
    }
}
