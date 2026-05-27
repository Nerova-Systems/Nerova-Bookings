using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Main.Features.WhatsAppFlows.Infrastructure;
using Microsoft.Extensions.Configuration;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Parameters;
using Xunit;

namespace Main.Tests.WhatsAppFlows;

public sealed class WabaFlowDataCipherTests
{
    private const string Passphrase = "test-passphrase-please-change";

    [Fact]
    public void Decrypt_WithMetaProtocolPayload_RoundTripsBackToOriginalJson()
    {
        var fixture = TenantKeyFixture.Generate(Passphrase);
        const string requestJson = "{\"action\":\"INIT\",\"screen\":\"WELCOME\"}";
        var (encryptedAesKey, encryptedFlowData, iv) = MetaClientEncrypt(fixture.PublicKeyPem, requestJson);

        var cipher = BuildCipher(Passphrase);

        var result = cipher.Decrypt(encryptedAesKey, encryptedFlowData, iv, fixture.EncryptedPrivateKey, fixture.PrivateKeyIv);

        result.IsSuccess.Should().BeTrue();
        result.Value!.PlaintextJson.Should().Be(requestJson);
        result.Value.AesKey.Length.Should().Be(16);
    }

    [Fact]
    public void Encrypt_UsesBitFlippedIv_SoMetaCanDecryptResponse()
    {
        var fixture = TenantKeyFixture.Generate(Passphrase);
        var (encryptedAesKey, encryptedFlowData, iv) = MetaClientEncrypt(fixture.PublicKeyPem, "{\"action\":\"ping\"}");
        var cipher = BuildCipher(Passphrase);

        var decrypted = cipher.Decrypt(encryptedAesKey, encryptedFlowData, iv, fixture.EncryptedPrivateKey, fixture.PrivateKeyIv).Value!;

        const string responseJson = "{\"data\":{\"status\":\"active\"}}";
        var encryptedResponse = cipher.Encrypt(responseJson, decrypted.AesKey, decrypted.InitialVector);

        // Client decrypts with bitwise-flipped IV (this matches Meta's documented behavior).
        var flippedIv = decrypted.InitialVector.Select(b => (byte)~b).ToArray();
        var responseBytes = Convert.FromBase64String(encryptedResponse);
        var plaintext = BcDecrypt(decrypted.AesKey, flippedIv, responseBytes);

        Encoding.UTF8.GetString(plaintext).Should().Be(responseJson);
    }

    [Fact]
    public void Decrypt_WithWrongPassphrase_FailsCleanly()
    {
        var fixture = TenantKeyFixture.Generate(Passphrase);
        var (encryptedAesKey, encryptedFlowData, iv) = MetaClientEncrypt(fixture.PublicKeyPem, "{}");
        var cipher = BuildCipher("entirely-wrong-passphrase");

        var result = cipher.Decrypt(encryptedAesKey, encryptedFlowData, iv, fixture.EncryptedPrivateKey, fixture.PrivateKeyIv);

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public void Decrypt_WithInvalidBase64_ReturnsBadRequest()
    {
        var fixture = TenantKeyFixture.Generate(Passphrase);
        var cipher = BuildCipher(Passphrase);

        var result = cipher.Decrypt("!!!not-base64!!!", "aGVsbG8=", "aGVsbG8=", fixture.EncryptedPrivateKey, fixture.PrivateKeyIv);

        result.IsSuccess.Should().BeFalse();
    }

    private static WabaFlowDataCipher BuildCipher(string passphrase)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["WhatsApp:EncryptionPassphrase"] = passphrase })
            .Build();
        return new WabaFlowDataCipher(configuration);
    }

    private static (string EncryptedAesKey, string EncryptedFlowData, string Iv) MetaClientEncrypt(string publicKeyPem, string json)
    {
        // Simulates what Meta does before calling our endpoint:
        // 1. Generate a fresh AES-128 key + 16-byte IV.
        // 2. RSA-OAEP-SHA256 encrypt the AES key with the tenant's public key.
        // 3. AES-128-GCM encrypt the JSON body. Concat ciphertext || tag, base64.
        var aesKey = RandomNumberGenerator.GetBytes(16);
        var iv = RandomNumberGenerator.GetBytes(16);

        using var rsa = RSA.Create();
        rsa.ImportFromPem(publicKeyPem);
        var encryptedAesKey = rsa.Encrypt(aesKey, RSAEncryptionPadding.OaepSHA256);

        var plaintext = Encoding.UTF8.GetBytes(json);
        var combined = BcEncrypt(aesKey, iv, plaintext);

        return (Convert.ToBase64String(encryptedAesKey), Convert.ToBase64String(combined), Convert.ToBase64String(iv));
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

    /// <summary>
    ///     Standalone replica of the account-side key generation so this test does not need the
    ///     Account assembly. Must match <c>WabaEncryptionService.GenerateKeyPair</c> exactly.
    /// </summary>
    private sealed record TenantKeyFixture(string PublicKeyPem, string EncryptedPrivateKey, string PrivateKeyIv)
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

            return new TenantKeyFixture(publicKeyPem, Convert.ToBase64String(combined), Convert.ToBase64String(nonce));
        }
    }
}
