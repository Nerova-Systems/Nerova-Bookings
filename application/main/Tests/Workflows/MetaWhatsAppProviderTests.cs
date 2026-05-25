using System.Net;
using System.Text.Json;
using FluentAssertions;
using Main.Features.Workflows.Infrastructure;
using Main.Features.Workflows.Senders;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Main.Tests.Workflows;

public sealed class MetaWhatsAppProviderTests
{
    [Fact]
    public async Task SendAsync_WhenNotConfigured_ShouldReturnNotConfiguredAndNotCallHttp()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var provider = BuildProvider(handler, new MetaWhatsAppOptions());

        var result = await provider.SendAsync(
            "+15551234567",
            "booking_reminder",
            new Dictionary<string, string> { ["1"] = "x" },
            CancellationToken.None
        );

        result.Status.Should().Be(WhatsAppResultStatus.NotConfigured);
        handler.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task SendAsync_WhenAccepted_ShouldPostExpectedJsonAndReturnMessageId()
    {
        HttpRequestMessage? captured = null;
        string? capturedBody = null;
        var handler = new RecordingHandler(request =>
        {
            captured = request;
            capturedBody = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""
                    { "messaging_product":"whatsapp",
                      "contacts":[{"input":"15551234567","wa_id":"15551234567"}],
                      "messages":[{"id":"wamid.HBgL..."}] }
                """)
            };
        });
        var options = new MetaWhatsAppOptions
        {
            PhoneNumberId = "111222333",
            AccessToken = "TOKEN_xyz",
            ApiBaseUrl = "https://graph.test/v18.0",
            DefaultLanguageCode = "en"
        };
        var provider = BuildProvider(handler, options);

        var result = await provider.SendAsync(
            "+15551234567",
            "booking_reminder",
            new Dictionary<string, string> { ["1"] = "Tomorrow at 10am", ["2"] = "2026-01-02 10:00:00Z" },
            CancellationToken.None
        );

        result.Status.Should().Be(WhatsAppResultStatus.Sent);
        result.MessageId.Should().Be("wamid.HBgL...");

        captured.Should().NotBeNull();
        captured!.Method.Should().Be(HttpMethod.Post);
        captured.RequestUri!.AbsoluteUri.Should().Be("https://graph.test/v18.0/111222333/messages");
        captured.Headers.Authorization!.Scheme.Should().Be("Bearer");
        captured.Headers.Authorization.Parameter.Should().Be("TOKEN_xyz");

        capturedBody.Should().NotBeNull();
        using var doc = JsonDocument.Parse(capturedBody!);
        var root = doc.RootElement;
        root.GetProperty("messaging_product").GetString().Should().Be("whatsapp");
        root.GetProperty("to").GetString().Should().Be("15551234567"); // leading + stripped
        root.GetProperty("type").GetString().Should().Be("template");

        var template = root.GetProperty("template");
        template.GetProperty("name").GetString().Should().Be("booking_reminder");
        template.GetProperty("language").GetProperty("code").GetString().Should().Be("en");

        var components = template.GetProperty("components");
        components.GetArrayLength().Should().Be(1);
        var body = components[0];
        body.GetProperty("type").GetString().Should().Be("body");

        var parameters = body.GetProperty("parameters");
        parameters.GetArrayLength().Should().Be(2);
        // Numeric keys → ordered by integer value (1 then 2).
        parameters[0].GetProperty("text").GetString().Should().Be("Tomorrow at 10am");
        parameters[1].GetProperty("text").GetString().Should().Be("2026-01-02 10:00:00Z");
    }

    [Fact]
    public async Task SendAsync_WhenServerErrors_ShouldReturnTransient()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.BadGateway)
        {
            Content = new StringContent("upstream")
        });
        var provider = BuildProvider(handler, ConfiguredOptions());

        var result = await provider.SendAsync(
            "+15551234567",
            "booking_reminder",
            new Dictionary<string, string>(),
            CancellationToken.None
        );

        result.Status.Should().Be(WhatsAppResultStatus.TransientFailure);
    }

    [Fact]
    public async Task SendAsync_When401_ShouldReturnPermanent()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            Content = new StringContent("{\"error\":{\"message\":\"Invalid OAuth token\"}}")
        });
        var provider = BuildProvider(handler, ConfiguredOptions());

        var result = await provider.SendAsync(
            "+15551234567",
            "booking_reminder",
            new Dictionary<string, string>(),
            CancellationToken.None
        );

        result.Status.Should().Be(WhatsAppResultStatus.PermanentFailure);
        result.ErrorReason.Should().Contain("401");
    }

    private static MetaWhatsAppOptions ConfiguredOptions() => new()
    {
        PhoneNumberId = "111222333",
        AccessToken = "TOKEN_xyz",
        ApiBaseUrl = "https://graph.test/v18.0"
    };

    private static MetaWhatsAppProvider BuildProvider(HttpMessageHandler handler, MetaWhatsAppOptions options)
    {
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(MetaWhatsAppProvider.HttpClientName).Returns(_ => new HttpClient(handler));
        return new MetaWhatsAppProvider(factory, Options.Create(options), NullLogger<MetaWhatsAppProvider>.Instance);
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
}
