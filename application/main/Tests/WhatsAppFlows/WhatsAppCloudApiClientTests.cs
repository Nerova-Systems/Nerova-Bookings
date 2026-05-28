using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Main.Features.WhatsAppFlows.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Main.Tests.WhatsAppFlows;

public sealed class WhatsAppCloudApiClientTests
{
    [Fact]
    public async Task SendTextMessageAsync_PostsToCorrectEndpoint_WithBearerToken()
    {
        HttpRequestMessage? captured = null;
        string? body = null;
        var handler = new RecordingHandler(async request =>
            {
                captured = request;
                body = request.Content is null ? null : await request.Content.ReadAsStringAsync();
                return new HttpResponseMessage(HttpStatusCode.OK);
            }
        );
        var client = BuildClient(handler);

        await client.SendTextMessageAsync("phone_123", "TOKEN", "+15551234567", "Hello", CancellationToken.None);

        captured.Should().NotBeNull();
        captured!.Method.Should().Be(HttpMethod.Post);
        captured.RequestUri!.AbsoluteUri.Should().Be("https://graph.test/v21.0/phone_123/messages");
        captured.Headers.Authorization!.Scheme.Should().Be("Bearer");
        captured.Headers.Authorization.Parameter.Should().Be("TOKEN");

        body.Should().NotBeNull();
        using var doc = JsonDocument.Parse(body!);
        doc.RootElement.GetProperty("messaging_product").GetString().Should().Be("whatsapp");
        doc.RootElement.GetProperty("to").GetString().Should().Be("+15551234567");
        doc.RootElement.GetProperty("type").GetString().Should().Be("text");
        doc.RootElement.GetProperty("text").GetProperty("body").GetString().Should().Be("Hello");
    }

    [Fact]
    public async Task SendTemplateMessageAsync_BuildsTemplatePayloadWithParameters()
    {
        string? body = null;
        var handler = new RecordingHandler(async request =>
            {
                body = await request.Content!.ReadAsStringAsync();
                return new HttpResponseMessage(HttpStatusCode.OK);
            }
        );
        var client = BuildClient(handler);

        await client.SendTemplateMessageAsync(
            "phone_123",
            "TOKEN",
            "+15551234567",
            "booking_confirmed",
            "en_US",
            new[] { "Alice", "Tomorrow 10am" },
            CancellationToken.None
        );

        using var doc = JsonDocument.Parse(body!);
        doc.RootElement.GetProperty("type").GetString().Should().Be("template");
        var template = doc.RootElement.GetProperty("template");
        template.GetProperty("name").GetString().Should().Be("booking_confirmed");
        template.GetProperty("language").GetProperty("code").GetString().Should().Be("en_US");
        var firstParam = template.GetProperty("components")[0].GetProperty("parameters")[0];
        firstParam.GetProperty("type").GetString().Should().Be("text");
        firstParam.GetProperty("text").GetString().Should().Be("Alice");
    }

    [Fact]
    public async Task SendTextMessageAsync_OnHttpError_ThrowsWhatsAppCloudApiException()
    {
        var handler = new RecordingHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent("{\"error\":\"invalid_phone\"}", Encoding.UTF8, "application/json")
            }
        ));
        var client = BuildClient(handler);

        var act = async () => await client.SendTextMessageAsync("p", "T", "+1", "x", CancellationToken.None);

        var ex = await act.Should().ThrowAsync<WhatsAppCloudApiException>();
        ex.Which.StatusCode.Should().Be(400);
        ex.Which.ResponseBody.Should().Contain("invalid_phone");
    }

    private static WhatsAppCloudApiClient BuildClient(HttpMessageHandler handler)
    {
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(WhatsAppCloudApiClient.HttpClientName).Returns(_ => new HttpClient(handler));
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["WhatsApp:GraphApiBaseUrl"] = "https://graph.test/v21.0/"
                }
            )
            .Build();
        return new WhatsAppCloudApiClient(factory, configuration, NullLogger<WhatsAppCloudApiClient>.Instance);
    }

    private sealed class RecordingHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => respond(request);
    }
}
