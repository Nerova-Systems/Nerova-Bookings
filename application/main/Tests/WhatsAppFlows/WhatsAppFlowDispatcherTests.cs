using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using Main.Features.WhatsAppFlows.Domain;
using Main.Features.WhatsAppFlows.Endpoint;
using Main.Features.WhatsAppFlows.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Parameters;
using SharedKernel.Cqrs;
using SharedKernel.Domain;
using Xunit;

namespace Main.Tests.WhatsAppFlows;

/// <summary>
///     Pure-unit tests for the dispatcher's protocol behavior: header validation, profile lookup,
///     ping short-circuit, screen routing. Crypto round-trips through the real cipher so these
///     also exercise the encrypt/decrypt path on every assertion.
/// </summary>
public sealed class WhatsAppFlowDispatcherTests
{
    private const string Passphrase = "test-passphrase-please-change";

    [Fact]
    public async Task Dispatch_WhenPhoneNumberHeaderMissing_Returns400()
    {
        var dispatcher = BuildDispatcher(out _, out _);

        var outcome = await dispatcher.Dispatch(new EncryptedFlowRequest("a", "b", "c"), phoneNumberId: null, CancellationToken.None);

        outcome.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task Dispatch_WhenProfileLookupReturnsNull_Returns404()
    {
        var dispatcher = BuildDispatcher(out var profileSync, out _);
        profileSync.GetByPhoneNumberId(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((WhatsAppFlowProfile?)null);

        var outcome = await dispatcher.Dispatch(new EncryptedFlowRequest("a", "b", "c"), "phone-unknown", CancellationToken.None);

        outcome.StatusCode.Should().Be(404);
    }

    [Fact]
    public async Task Dispatch_WhenPingAction_ReturnsEncryptedActiveStatus()
    {
        var fixture = TenantKeyFixture.Generate(Passphrase);
        var dispatcher = BuildDispatcher(out var profileSync, out _);
        profileSync.GetByPhoneNumberId(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(fixture.Profile);

        var (encryptedAesKey, encryptedFlowData, iv, aesKey, ivBytes) = MetaClientEncrypt(fixture.PublicKeyPem, "{\"action\":\"ping\"}");

        var outcome = await dispatcher.Dispatch(
            new EncryptedFlowRequest(encryptedAesKey, encryptedFlowData, iv),
            "phone-1",
            CancellationToken.None
        );

        outcome.StatusCode.Should().Be(200);

        var responseJson = DecryptResponse(outcome.Body, aesKey, ivBytes);
        using var doc = JsonDocument.Parse(responseJson);
        doc.RootElement.GetProperty("data").GetProperty("status").GetString().Should().Be("active");
    }

    [Fact]
    public async Task Dispatch_WhenWelcomeInitAction_RoutesToWelcomeHandler()
    {
        var fixture = TenantKeyFixture.Generate(Passphrase);
        var config = TenantFlowConfig.Create(fixture.Profile.TenantId, BusinessVertical.HairSalon);
        var configRepository = Substitute.For<ITenantFlowConfigRepository>();
        configRepository.GetByTenantIdAsync(fixture.Profile.TenantId, Arg.Any<CancellationToken>()).Returns(config);

        var dispatcher = BuildDispatcher(out var profileSync, configRepository, new IFlowScreenHandler[] { new WelcomeScreenHandler() });
        profileSync.GetByPhoneNumberId(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(fixture.Profile);

        var requestJson = "{\"action\":\"INIT\",\"screen\":\"WELCOME\",\"data\":{},\"flow_token\":\"tok\"}";
        var (encryptedAesKey, encryptedFlowData, iv, aesKey, ivBytes) = MetaClientEncrypt(fixture.PublicKeyPem, requestJson);

        var outcome = await dispatcher.Dispatch(
            new EncryptedFlowRequest(encryptedAesKey, encryptedFlowData, iv),
            "phone-1",
            CancellationToken.None
        );

        outcome.StatusCode.Should().Be(200);
        var responseJson = DecryptResponse(outcome.Body, aesKey, ivBytes);
        using var doc = JsonDocument.Parse(responseJson);
        doc.RootElement.GetProperty("screen").GetString().Should().Be(FlowScreens.Welcome);
        doc.RootElement.GetProperty("data").GetProperty("intro").GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void WelcomeHandler_OnDataExchange_WithMultipleServices_RoutesToSelectService()
    {
        var handler = new WelcomeScreenHandler();
        var config = TenantFlowConfig.Create(TenantId.NewId(), BusinessVertical.HairSalon);

        var request = new FlowScreenRequest("data_exchange", default, "tok", config.TenantId);
        var result = handler.Handle(request, config, CancellationToken.None).Result;

        result.NextScreen.Should().Be(FlowScreens.SelectService);
    }

    [Fact]
    public void WelcomeHandler_OnDataExchange_WithSingleService_RoutesToSelectDate()
    {
        var handler = new WelcomeScreenHandler();
        var config = TenantFlowConfig.Create(TenantId.NewId(), BusinessVertical.PersonalTrainer);

        var request = new FlowScreenRequest("data_exchange", default, "tok", config.TenantId);
        var result = handler.Handle(request, config, CancellationToken.None).Result;

        result.NextScreen.Should().Be(FlowScreens.SelectDate);
    }

    private static WhatsAppFlowDispatcher BuildDispatcher(out IWhatsAppFlowProfileSync profileSync, out ITenantFlowConfigRepository configRepository)
    {
        profileSync = Substitute.For<IWhatsAppFlowProfileSync>();
        configRepository = Substitute.For<ITenantFlowConfigRepository>();
        var cipher = new WabaFlowDataCipher(
            new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["WhatsApp:EncryptionPassphrase"] = Passphrase })
                .Build()
        );
        return new WhatsAppFlowDispatcher(profileSync, cipher, configRepository, Array.Empty<IFlowScreenHandler>(), NullLogger<WhatsAppFlowDispatcher>.Instance);
    }

    private static WhatsAppFlowDispatcher BuildDispatcher(out IWhatsAppFlowProfileSync profileSync, ITenantFlowConfigRepository configRepository, IEnumerable<IFlowScreenHandler> handlers)
    {
        profileSync = Substitute.For<IWhatsAppFlowProfileSync>();
        var cipher = new WabaFlowDataCipher(
            new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["WhatsApp:EncryptionPassphrase"] = Passphrase })
                .Build()
        );
        return new WhatsAppFlowDispatcher(profileSync, cipher, configRepository, handlers, NullLogger<WhatsAppFlowDispatcher>.Instance);
    }

    private static (string EncryptedAesKey, string EncryptedFlowData, string Iv, byte[] AesKey, byte[] IvBytes) MetaClientEncrypt(string publicKeyPem, string json)
    {
        var aesKey = RandomNumberGenerator.GetBytes(16);
        var iv = RandomNumberGenerator.GetBytes(16);

        using var rsa = RSA.Create();
        rsa.ImportFromPem(publicKeyPem);
        var encryptedAesKey = rsa.Encrypt(aesKey, RSAEncryptionPadding.OaepSHA256);

        var plaintext = Encoding.UTF8.GetBytes(json);
        var combined = BcEncrypt(aesKey, iv, plaintext);
        return (Convert.ToBase64String(encryptedAesKey), Convert.ToBase64String(combined), Convert.ToBase64String(iv), aesKey, iv);
    }

    private static byte[] BcEncrypt(byte[] key, byte[] iv, byte[] plaintext)
    {
        var cipher = new GcmBlockCipher(new AesEngine());
        cipher.Init(true, new AeadParameters(new KeyParameter(key), 128, iv));
        var output = new byte[cipher.GetOutputSize(plaintext.Length)];
        var offset = cipher.ProcessBytes(plaintext, 0, plaintext.Length, output, 0);
        cipher.DoFinal(output, offset);
        return output;
    }

    private static byte[] BcDecrypt(byte[] key, byte[] iv, byte[] ciphertextWithTag)
    {
        var cipher = new GcmBlockCipher(new AesEngine());
        cipher.Init(false, new AeadParameters(new KeyParameter(key), 128, iv));
        var output = new byte[cipher.GetOutputSize(ciphertextWithTag.Length)];
        var offset = cipher.ProcessBytes(ciphertextWithTag, 0, ciphertextWithTag.Length, output, 0);
        cipher.DoFinal(output, offset);
        return output;
    }

    private static string DecryptResponse(string base64, byte[] aesKey, byte[] requestIv)
    {
        var flippedIv = requestIv.Select(b => (byte)~b).ToArray();
        var bytes = Convert.FromBase64String(base64);
        var plaintext = BcDecrypt(aesKey, flippedIv, bytes);
        return Encoding.UTF8.GetString(plaintext);
    }

    private sealed record TenantKeyFixture(string PublicKeyPem, WhatsAppFlowProfile Profile)
    {
        public static TenantKeyFixture Generate(string passphrase)
        {
            using var rsa = RSA.Create(2048);
            var publicKeyPem = rsa.ExportSubjectPublicKeyInfoPem();
            var privateKeyPem = rsa.ExportPkcs8PrivateKeyPem();

            var salt = RandomNumberGenerator.GetBytes(16);
            var nonce = RandomNumberGenerator.GetBytes(12);
            var aesKey = Rfc2898DeriveBytes.Pbkdf2(
                Encoding.UTF8.GetBytes(passphrase),
                salt,
                100_000,
                HashAlgorithmName.SHA256,
                32
            );

            var plaintext = Encoding.UTF8.GetBytes(privateKeyPem);
            var ciphertext = new byte[plaintext.Length];
            var tag = new byte[16];
            using var aesGcm = new AesGcm(aesKey, 16);
            aesGcm.Encrypt(nonce, plaintext, ciphertext, tag);

            var combined = new byte[16 + ciphertext.Length + 16];
            salt.CopyTo(combined, 0);
            ciphertext.CopyTo(combined, 16);
            tag.CopyTo(combined, 16 + ciphertext.Length);

            var profile = new WhatsAppFlowProfile(
                TenantId.NewId(),
                WabaId: "waba_abc",
                PhoneNumberId: "phone-1",
                DisplayPhoneNumber: "+1 555 0100",
                FlowId: "flow_1",
                FlowStatus: "Published",
                OnboardingGateStatus: "Complete",
                WabaAccessToken: "TOKEN",
                EncryptedPrivateKey: Convert.ToBase64String(combined),
                PrivateKeyIv: Convert.ToBase64String(nonce),
                PublicKeyFingerprint: "fp"
            );

            return new TenantKeyFixture(publicKeyPem, profile);
        }
    }
}
