using Account.Features.ExternalAuthentication.Domain;
using Account.Features.ExternalAuthentication.Shared;
using Account.Features.Users.Domain;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Http;
using SharedKernel.Cqrs;
using SharedKernel.OpenIdConnect;
using SharedKernel.Telemetry;

namespace Account.Features.ExternalAuthentication.Commands;

[PublicAPI]
public sealed record CompleteExternalLinkCommand(string? Code, string? State, string? Error, string? ErrorDescription)
    : ICommand, IRequest<Result<string>>
{
    [JsonIgnore] // Removes from API contract
    public string Provider { get; init; } = null!;
}

public sealed class CompleteExternalLinkHandler(
    IExternalLoginRepository externalLoginRepository,
    IUserRepository userRepository,
    ExternalAuthenticationHelper externalAuthenticationHelper,
    ExternalAuthenticationService externalAuthenticationService,
    IHttpContextAccessor httpContextAccessor,
    ITelemetryEventsCollector events,
    TimeProvider timeProvider,
    ILogger<CompleteExternalLinkHandler> logger
) : IRequestHandler<CompleteExternalLinkCommand, Result<string>>
{
    public async Task<Result<string>> Handle(CompleteExternalLinkCommand command, CancellationToken cancellationToken)
    {
        try
        {
            var validationResult = await externalAuthenticationHelper.ValidateCallback(
                command.Code, command.State, command.Error, command.ErrorDescription, ExternalLoginType.Link, cancellationToken
            );

            if (!validationResult.IsSuccess) return validationResult.ErrorResult!;

            var externalLogin = validationResult.ExternalLogin;
            var userProfile = validationResult.UserProfile!;

            var user = await userRepository.GetLoggedInUserAsync(cancellationToken);
            var userWithIdentity = await userRepository.GetByExternalIdentityUnfilteredAsync(externalLogin.ProviderType, userProfile.ProviderUserId, cancellationToken);
            if (userWithIdentity is not null && userWithIdentity.Id != user.Id)
            {
                logger.LogWarning("External identity already linked for provider '{ProviderType}'", externalLogin.ProviderType);
                return LinkFailedRedirect(externalLogin, ExternalLoginResult.AccountAlreadyExists);
            }

            var existingIdentity = user.GetExternalIdentity(externalLogin.ProviderType);
            if (existingIdentity is not null && existingIdentity.ProviderUserId != userProfile.ProviderUserId)
            {
                logger.LogWarning("Identity mismatch while linking provider '{ProviderType}' for user '{UserId}'", externalLogin.ProviderType, user.Id);
                return LinkFailedRedirect(externalLogin, ExternalLoginResult.IdentityMismatch);
            }

            if (existingIdentity is null)
            {
                user.AddExternalIdentity(externalLogin.ProviderType, userProfile.ProviderUserId);
                userRepository.Update(user);
            }

            externalLogin.MarkCompleted(userProfile.Email);
            externalLoginRepository.Update(externalLogin);

            var linkTimeInSeconds = (int)(timeProvider.GetUtcNow() - externalLogin.CreatedAt).TotalSeconds;
            events.CollectEvent(new ExternalAccountLinked(user.Id, externalLogin.ProviderType, linkTimeInSeconds));

            var httpContext = httpContextAccessor.HttpContext!;
            var returnPath = ReturnPathHelper.GetReturnPathCookie(httpContext) ?? "/user/preferences";
            ReturnPathHelper.ClearReturnPathCookie(httpContext);

            return Result<string>.Redirect(returnPath);
        }
        finally
        {
            externalAuthenticationService.ClearExternalLoginCookie();
            externalAuthenticationService.ClearLocaleCookie();
        }
    }

    private Result<string> LinkFailedRedirect(ExternalLogin externalLogin, ExternalLoginResult loginResult)
    {
        var timeInSeconds = (int)(timeProvider.GetUtcNow() - externalLogin.CreatedAt).TotalSeconds;
        if (!externalLogin.IsConsumed)
        {
            externalLogin.MarkFailed(loginResult);
            externalLoginRepository.Update(externalLogin);
        }

        events.CollectEvent(new ExternalAccountLinkFailed(externalLogin.Id, loginResult, timeInSeconds));

        var oidcError = ExternalAuthenticationService.MapToOidcError(loginResult);
        return Result<string>.Redirect($"/error?error={oidcError}&id={externalLogin.Id}");
    }
}
