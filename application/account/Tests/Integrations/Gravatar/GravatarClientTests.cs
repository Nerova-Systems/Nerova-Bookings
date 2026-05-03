using System.Net;
using Account.Integrations.Gravatar;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Polly.CircuitBreaker;
using Polly.Timeout;
using SharedKernel.Domain;
using Xunit;

namespace Account.Tests.Integrations.Gravatar;

public sealed class GravatarClientTests
{
    [Fact]
    public async Task GetGravatar_WhenHandlerThrowsHttpRequestException_ShouldReturnNull()
    {
        // Arrange
        var client = CreateClient(new ThrowingHandler(new HttpRequestException("Simulated upstream failure")));

        // Act
        var result = await client.GetGravatar(UserId.NewId(), "user@example.com", CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetGravatar_WhenHandlerThrowsBrokenCircuitException_ShouldReturnNull()
    {
        // Arrange
        var client = CreateClient(new ThrowingHandler(new BrokenCircuitException("Simulated open circuit")));

        // Act
        var result = await client.GetGravatar(UserId.NewId(), "user@example.com", CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetGravatar_WhenHandlerThrowsTaskCanceledException_ShouldReturnNull()
    {
        // Arrange
        var client = CreateClient(new ThrowingHandler(new TaskCanceledException("Simulated timeout")));

        // Act
        var result = await client.GetGravatar(UserId.NewId(), "user@example.com", CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetGravatar_WhenHandlerThrowsTimeoutRejectedException_ShouldReturnNull()
    {
        // Arrange
        var client = CreateClient(new ThrowingHandler(new TimeoutRejectedException("Simulated Polly timeout")));

        // Act
        var result = await client.GetGravatar(UserId.NewId(), "user@example.com", CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetGravatar_WhenHandlerReturns503_ShouldReturnNull()
    {
        // Arrange
        var client = CreateClient(new StatusCodeHandler(HttpStatusCode.ServiceUnavailable));

        // Act
        var result = await client.GetGravatar(UserId.NewId(), "user@example.com", CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetGravatar_WhenHandlerHangsThroughResiliencePipeline_ShouldReturnNullViaTimeoutRejected()
    {
        // Arrange
        // Wires the real AddStandardResilienceHandler() pipeline around a hung handler so the production
        // exception type (TimeoutRejectedException after AttemptTimeout fires) actually surfaces. Timeouts
        // are shrunk to keep the test fast (~1s), but the exception-shape contract is identical to prod.
        var services = new ServiceCollection();
        services.AddHttpClient<GravatarClient>(httpClient =>
                {
                    httpClient.BaseAddress = new Uri("https://gravatar.com/");
                    httpClient.Timeout = TimeSpan.FromSeconds(30);
                }
            ).ConfigurePrimaryHttpMessageHandler(() => new HangingHandler())
            .AddStandardResilienceHandler(options =>
                {
                    options.AttemptTimeout.Timeout = TimeSpan.FromMilliseconds(500);
                    options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(1);
                    options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(5);
                    options.Retry.MaxRetryAttempts = 1;
                    options.Retry.Delay = TimeSpan.FromMilliseconds(50);
                }
            );
        services.AddSingleton(NullLogger<GravatarClient>.Instance);

        await using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<GravatarClient>();

        // Act
        var result = await client.GetGravatar(UserId.NewId(), "user@example.com", CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    private static GravatarClient CreateClient(HttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://gravatar.com/") };
        return new GravatarClient(httpClient, NullLogger<GravatarClient>.Instance);
    }

    private sealed class ThrowingHandler(Exception exception) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            throw exception;
        }
    }

    private sealed class StatusCodeHandler(HttpStatusCode statusCode) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(statusCode));
        }
    }

    private sealed class HangingHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            await Task.Delay(TimeSpan.FromMinutes(1), cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }
}
