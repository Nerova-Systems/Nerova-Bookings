using AppGateway.Middleware;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using SharedKernel.Authentication;
using SharedKernel.Authentication.TokenSigning;
using Xunit;

namespace AppGateway.Tests;

public sealed class AuthenticationCookiePathTests(AppGatewayApplicationFactory factory) : IClassFixture<AppGatewayApplicationFactory>
{
    [Fact]
    public async Task ReplaceAuthenticationHeaderWithCookie_WhenInnerHandlerSetsTokenHeaders_ShouldIssueHostCookiesWithPathSlash()
    {
        // Arrange
        await using var scope = factory.Services.CreateAsyncScope();
        var middleware = scope.ServiceProvider.GetRequiredService<AuthenticationCookieMiddleware>();
        var signingClient = scope.ServiceProvider.GetRequiredService<ITokenSigningClient>();

        var refreshToken = CreateSignedToken(signingClient, 60);
        var accessToken = CreateSignedToken(signingClient, 5);

        var context = new DefaultHttpContext
        {
            Request = { Path = "/api/account/authentication/switch-tenant" },
            Response = { Body = new MemoryStream() }
        };

        Task Next(HttpContext ctx)
        {
            ctx.Response.Headers[AuthenticationTokenHttpKeys.RefreshTokenHttpHeaderKey] = refreshToken;
            ctx.Response.Headers[AuthenticationTokenHttpKeys.AccessTokenHttpHeaderKey] = accessToken;
            return Task.CompletedTask;
        }

        // Act
        await middleware.InvokeAsync(context, Next);

        // Assert
        var setCookieHeaders = context.Response.Headers.SetCookie.ToArray();
        setCookieHeaders.Should().Contain(h => h!.Contains(AuthenticationTokenHttpKeys.RefreshTokenCookieName));
        setCookieHeaders.Should().Contain(h => h!.Contains(AuthenticationTokenHttpKeys.AccessTokenCookieName));
        foreach (var setCookie in setCookieHeaders.Where(h => h!.Contains("__Host-")))
        {
            setCookie.Should().Contain("path=/", $"every __Host- cookie must declare Path=/, but got: {setCookie}");
        }
    }

    [Fact]
    public async Task ExpiredRefreshToken_WhenMiddlewareDeletesCookies_ShouldIssueDeletionWithPathSlash()
    {
        // Arrange
        await using var scope = factory.Services.CreateAsyncScope();
        var middleware = scope.ServiceProvider.GetRequiredService<AuthenticationCookieMiddleware>();
        var signingClient = scope.ServiceProvider.GetRequiredService<ITokenSigningClient>();

        var expiredRefreshToken = CreateSignedToken(signingClient, -10);
        var expiredAccessToken = CreateSignedToken(signingClient, -10);

        var context = new DefaultHttpContext
        {
            Request =
            {
                Path = "/some-spa-path",
                Headers = { Cookie = $"{AuthenticationTokenHttpKeys.RefreshTokenCookieName}={expiredRefreshToken}; {AuthenticationTokenHttpKeys.AccessTokenCookieName}={expiredAccessToken}" }
            },
            Response = { Body = new MemoryStream() }
        };

        // Act
        await middleware.InvokeAsync(context, _ => Task.CompletedTask);

        // Assert
        var setCookieHeaders = context.Response.Headers.SetCookie.ToArray();
        setCookieHeaders.Should().NotBeEmpty("expired cookies must be deleted");
        foreach (var setCookie in setCookieHeaders.Where(h => h!.Contains("__Host-")))
        {
            setCookie.Should().Contain("path=/", $"every __Host- cookie deletion must declare Path=/, but got: {setCookie}");
        }
    }

    private static string CreateSignedToken(ITokenSigningClient signingClient, int validForMinutes)
    {
        var now = DateTimeOffset.UtcNow;
        var notBefore = validForMinutes >= 0 ? now.UtcDateTime : now.AddMinutes(validForMinutes - 1).UtcDateTime;
        var descriptor = new SecurityTokenDescriptor
        {
            NotBefore = notBefore,
            IssuedAt = notBefore,
            Expires = now.AddMinutes(validForMinutes).UtcDateTime,
            Issuer = signingClient.Issuer,
            Audience = signingClient.Audience,
            SigningCredentials = signingClient.GetSigningCredentials()
        };
        return new JsonWebTokenHandler().CreateToken(descriptor);
    }
}
