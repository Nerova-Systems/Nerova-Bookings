using System.Net;
using FluentAssertions;
using Main.Features.Workflows.Infrastructure;
using Main.Features.Workflows.Senders;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Main.Tests.Workflows;

public sealed class TwilioSmsProviderTests
{
    [Fact]
    public async Task SendAsync_WhenNotConfigured_ShouldReturnNotConfiguredAndNotCallHttp()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var provider = BuildProvider(handler, new TwilioOptions());

        var result = await provider.SendAsync("+15551234567", "hi", CancellationToken.None);

        result.Status.Should().Be(SmsResultStatus.NotConfigured);
        handler.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task SendAsync_WhenAccepted_ShouldFormEncodeAndReturnSid()
    {
        HttpRequestMessage? captured = null;
        string? capturedBody = null;
        var handler = new RecordingHandler(request =>
            {
                captured = request;
                capturedBody = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
                return new HttpResponseMessage(HttpStatusCode.Created)
                {
                    Content = new StringContent("{\"sid\":\"SM_abc123\",\"status\":\"queued\"}")
                };
            }
        );
        var options = new TwilioOptions
        {
            AccountSid = "AC_test",
            AuthToken = "secret",
            FromNumber = "+15550000000",
            ApiBaseUrl = "https://twilio.test/2010-04-01"
        };
        var provider = BuildProvider(handler, options);

        var result = await provider.SendAsync("+15551234567", "your appointment is soon", CancellationToken.None);

        result.Status.Should().Be(SmsResultStatus.Sent);
        result.MessageId.Should().Be("SM_abc123");

        captured.Should().NotBeNull();
        captured!.Method.Should().Be(HttpMethod.Post);
        captured.RequestUri!.AbsoluteUri.Should().Be("https://twilio.test/2010-04-01/Accounts/AC_test/Messages.json");
        captured.Headers.Authorization!.Scheme.Should().Be("Basic");
        var expectedBasic = Convert.ToBase64String("AC_test:secret"u8);
        captured.Headers.Authorization.Parameter.Should().Be(expectedBasic);
        captured.Content!.Headers.ContentType!.MediaType.Should().Be("application/x-www-form-urlencoded");

        capturedBody.Should().Contain("To=%2B15551234567");
        capturedBody.Should().Contain("From=%2B15550000000");
        capturedBody.Should().Contain("Body=your+appointment+is+soon");
    }

    [Fact]
    public async Task SendAsync_When5xx_ShouldReturnTransient()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            {
                Content = new StringContent("temporarily down")
            }
        );
        var provider = BuildProvider(handler, ConfiguredOptions());

        var result = await provider.SendAsync("+15551234567", "body", CancellationToken.None);

        result.Status.Should().Be(SmsResultStatus.TransientFailure);
        result.ErrorReason.Should().Contain("503");
    }

    [Fact]
    public async Task SendAsync_When429_ShouldReturnTransient()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage((HttpStatusCode)429)
            {
                Content = new StringContent("rate limited")
            }
        );
        var provider = BuildProvider(handler, ConfiguredOptions());

        var result = await provider.SendAsync("+15551234567", "body", CancellationToken.None);

        result.Status.Should().Be(SmsResultStatus.TransientFailure);
    }

    [Fact]
    public async Task SendAsync_When400_ShouldReturnPermanent()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent("{\"message\":\"Invalid 'To' Phone Number\"}")
            }
        );
        var provider = BuildProvider(handler, ConfiguredOptions());

        var result = await provider.SendAsync("+0", "body", CancellationToken.None);

        result.Status.Should().Be(SmsResultStatus.PermanentFailure);
        result.ErrorReason.Should().Contain("400");
    }

    [Fact]
    public async Task SendAsync_WhenTransportThrows_ShouldReturnTransient()
    {
        var handler = new ThrowingHandler(new HttpRequestException("connection reset"));
        var provider = BuildProvider(handler, ConfiguredOptions());

        var result = await provider.SendAsync("+15551234567", "body", CancellationToken.None);

        result.Status.Should().Be(SmsResultStatus.TransientFailure);
        result.ErrorReason.Should().Contain("connection reset");
    }

    private static TwilioOptions ConfiguredOptions()
    {
        return new TwilioOptions
        {
            AccountSid = "AC_test",
            AuthToken = "secret",
            FromNumber = "+15550000000",
            ApiBaseUrl = "https://twilio.test/2010-04-01"
        };
    }

    private static TwilioSmsProvider BuildProvider(HttpMessageHandler handler, TwilioOptions options)
    {
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(TwilioSmsProvider.HttpClientName).Returns(_ => new HttpClient(handler));
        return new TwilioSmsProvider(factory, Options.Create(options), NullLogger<TwilioSmsProvider>.Instance);
    }

    private sealed class RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public int CallCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(respond(request));
        }
    }

    private sealed class ThrowingHandler(Exception ex) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            throw ex;
        }
    }
}
