using System.Net;
using System.Text.Json;
using FluentAssertions;
using Main.Features.WhatsAppOnboarding.Domain;
using Main.Features.WhatsAppOnboarding.Shared;
using Main.Features.Workflows.Infrastructure;
using Main.Features.Workflows.Senders;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using SharedKernel.Domain;
using Xunit;

namespace Main.Tests.Workflows;

public sealed class MetaWhatsAppProviderTests
{
    private static readonly TenantId TestTenantId = new(1);

    [Fact]
    public async Task SendAsync_WhenNotConfigured_ShouldReturnNotConfiguredAndNotCallHttp()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var wabaRepository = Substitute.For<IWhatsAppBusinessAccountRepository>();
        wabaRepository.GetByTenantIdUnfilteredAsync(Arg.Any<TenantId>(), Arg.Any<CancellationToken>()).Returns((WhatsAppBusinessAccount?)null);
        var provider = BuildProvider(handler, new MetaWhatsAppOptions(), wabaRepository, CreateProtector());

        var result = await provider.SendAsync(
            TestTenantId,
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
                                                """
                    )
                };
            }
        );
        var options = new MetaWhatsAppOptions
        {
            ApiBaseUrl = "https://graph.test/v18.0",
            DefaultLanguageCode = "en"
        };
        var protector = CreateProtector();
        var wabaRepository = CreateWabaRepository(protector);
        var provider = BuildProvider(handler, options, wabaRepository, protector);

        var result = await provider.SendAsync(
            TestTenantId,
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
            }
        );
        var protector = CreateProtector();
        var provider = BuildProvider(handler, ConfiguredOptions(), CreateWabaRepository(protector), protector);

        var result = await provider.SendAsync(
            TestTenantId,
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
            }
        );
        var protector = CreateProtector();
        var provider = BuildProvider(handler, ConfiguredOptions(), CreateWabaRepository(protector), protector);

        var result = await provider.SendAsync(
            TestTenantId,
            "+15551234567",
            "booking_reminder",
            new Dictionary<string, string>(),
            CancellationToken.None
        );

        result.Status.Should().Be(WhatsAppResultStatus.PermanentFailure);
        result.ErrorReason.Should().Contain("401");
    }

    private static MetaWhatsAppOptions ConfiguredOptions()
    {
        return new MetaWhatsAppOptions { ApiBaseUrl = "https://graph.test/v18.0" };
    }

    private static WhatsAppAccessTokenProtector CreateProtector()
    {
        return new WhatsAppAccessTokenProtector(new EphemeralDataProtectionProvider(), NullLogger<WhatsAppAccessTokenProtector>.Instance);
    }

    private static IWhatsAppBusinessAccountRepository CreateWabaRepository(WhatsAppAccessTokenProtector tokenProtector)
    {
        var account = WhatsAppBusinessAccount.Create(
            TestTenantId,
            "WABA_1",
            "Test Business",
            tokenProtector.Protect("TOKEN_xyz"),
            WhatsAppPhoneNumber.CreateRegistered("111222333", "+1 555-0100", "Test Business")
        );
        var repository = Substitute.For<IWhatsAppBusinessAccountRepository>();
        repository.GetByTenantIdUnfilteredAsync(Arg.Any<TenantId>(), Arg.Any<CancellationToken>()).Returns(account);
        return repository;
    }

    private static MetaWhatsAppProvider BuildProvider(HttpMessageHandler handler, MetaWhatsAppOptions options, IWhatsAppBusinessAccountRepository wabaRepository, WhatsAppAccessTokenProtector tokenProtector)
    {
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(MetaWhatsAppProvider.HttpClientName).Returns(_ => new HttpClient(handler));
        return new MetaWhatsAppProvider(factory, Options.Create(options), wabaRepository, tokenProtector, NullLogger<MetaWhatsAppProvider>.Instance);
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
