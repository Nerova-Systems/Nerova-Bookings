using System.Net;
using FluentAssertions;
using Main.Features.WhatsAppFlows.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Main.Tests.WhatsAppFlows;

public sealed class MetaFlowsApiClientTests
{
    [Fact]
    public async Task CreateFlowAsync_OnSuccess_ReturnsFlowId()
    {
        HttpRequestMessage? captured = null;
        var handler = new RecordingHandler(request =>
            {
                captured = request;
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""{ "id": "1234567890" }""")
                };
            }
        );
        var client = BuildClient(handler);

        var result = await client.CreateFlowAsync("waba_abc", "MyFlow", "TOKEN", CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.FlowId.Should().Be("1234567890");
        captured.Should().NotBeNull();
        captured!.Method.Should().Be(HttpMethod.Post);
        captured.RequestUri!.AbsoluteUri.Should().Be("https://graph.test/v21.0/waba_abc/flows");
        captured.Headers.Authorization!.Scheme.Should().Be("Bearer");
        captured.Headers.Authorization.Parameter.Should().Be("TOKEN");
    }

    [Fact]
    public async Task CreateFlowAsync_OnHttpError_ReturnsBadRequest()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent("{\"error\":\"bad\"}")
            }
        );
        var client = BuildClient(handler);

        var result = await client.CreateFlowAsync("waba_abc", "MyFlow", "TOKEN", CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task UploadFlowAssetAsync_PostsMultipartToAssetsEndpoint()
    {
        HttpRequestMessage? captured = null;
        var handler = new RecordingHandler(request =>
            {
                captured = request;
                return new HttpResponseMessage(HttpStatusCode.OK);
            }
        );
        var client = BuildClient(handler);

        var result = await client.UploadFlowAssetAsync("flow_1", "{\"version\":\"7.0\"}", "TOKEN", CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        captured.Should().NotBeNull();
        captured!.RequestUri!.AbsoluteUri.Should().Be("https://graph.test/v21.0/flow_1/assets");
        captured.Content.Should().BeOfType<MultipartFormDataContent>();
    }

    [Fact]
    public async Task PublishFlowAsync_PostsToPublishEndpoint()
    {
        HttpRequestMessage? captured = null;
        var handler = new RecordingHandler(request =>
            {
                captured = request;
                return new HttpResponseMessage(HttpStatusCode.OK);
            }
        );
        var client = BuildClient(handler);

        var result = await client.PublishFlowAsync("flow_1", "TOKEN", CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        captured!.RequestUri!.AbsoluteUri.Should().Be("https://graph.test/v21.0/flow_1/publish");
    }

    [Fact]
    public async Task GetFlowPreviewUrlAsync_OnSuccess_ParsesPreviewUrl()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""
                                                {
                                                    "preview": {
                                                        "preview_url": "https://business.facebook.com/wa/manage/flows/preview/abc",
                                                        "expires_at": "2030-01-01T00:00:00Z"
                                                    }
                                                }
                                            """
                )
            }
        );
        var client = BuildClient(handler);

        var result = await client.GetFlowPreviewUrlAsync("flow_1", "TOKEN", CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.PreviewUrl.Should().StartWith("https://business.facebook.com/");
    }

    private static MetaFlowsApiClient BuildClient(HttpMessageHandler handler)
    {
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(MetaFlowsApiClient.HttpClientName).Returns(_ => new HttpClient(handler));
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["WhatsApp:GraphApiBaseUrl"] = "https://graph.test/v21.0/"
                }
            )
            .Build();
        return new MetaFlowsApiClient(factory, configuration, NullLogger<MetaFlowsApiClient>.Instance);
    }

    private sealed class RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(respond(request));
        }
    }
}
