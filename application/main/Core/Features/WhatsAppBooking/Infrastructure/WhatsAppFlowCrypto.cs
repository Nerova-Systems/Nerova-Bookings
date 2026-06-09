using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace Main.Features.WhatsAppBooking.Infrastructure;

/// <summary>
///     RSA + AES-GCM encryption/decryption for Meta WhatsApp Flows data endpoints.
///     Meta sends requests encrypted with the app's RSA public key; responses are encrypted using
///     the same AES-GCM session key with the last byte of the IV flipped.
///     <para>
///         If <c>Meta:FlowRsaPrivateKeyPem</c> is not configured, a new 2048-bit key pair is generated
///         at startup. The public key is logged as a warning so it can be uploaded to Meta and the
///         private key stored in secrets. Once configured, the key pair persists across restarts.
///     </para>
/// </summary>
public sealed class WhatsAppFlowCrypto : IDisposable
{
    private readonly RSA _rsa;

    public WhatsAppFlowCrypto(IConfiguration configuration, ILogger<WhatsAppFlowCrypto> logger)
    {
        var pem = configuration["Meta:FlowRsaPrivateKeyPem"];
        _rsa = RSA.Create(2048);

        if (string.IsNullOrWhiteSpace(pem))
        {
            logger.LogWarning(
                "Meta:FlowRsaPrivateKeyPem is not configured. Generated a new RSA-2048 key pair. "
                + "Store the private key in user secrets as Meta:FlowRsaPrivateKeyPem and upload the public key to Meta. Then restart.\n"
                + "Private key:\n{PrivateKey}\nPublic key (upload to Meta):\n{PublicKey}",
                _rsa.ExportRSAPrivateKeyPem(),
                _rsa.ExportSubjectPublicKeyInfoPem()
            );
        }
        else
        {
            _rsa.ImportFromPem(pem);
        }
    }

    /// <summary>PEM-encoded RSA public key (SubjectPublicKeyInfo format) for upload to Meta.</summary>
    public string PublicKeyPem => _rsa.ExportSubjectPublicKeyInfoPem();

    public void Dispose()
    {
        _rsa.Dispose();
    }

    /// <summary>
    ///     Decrypts an incoming Meta Flow request. Returns the AES key, original IV, and decrypted JSON body.
    /// </summary>
    public FlowDecryptResult Decrypt(string encryptedAesKeyBase64, string encryptedFlowDataBase64, string initialVectorBase64)
    {
        var aesKey = _rsa.Decrypt(Convert.FromBase64String(encryptedAesKeyBase64), RSAEncryptionPadding.OaepSHA256);
        var iv = Convert.FromBase64String(initialVectorBase64);
        var cipherWithTag = Convert.FromBase64String(encryptedFlowDataBase64);

        var ciphertext = cipherWithTag[..^16];
        var tag = cipherWithTag[^16..];

        using var aes = new AesGcm(aesKey, 16);
        var plaintext = new byte[ciphertext.Length];
        aes.Decrypt(iv, ciphertext, tag, plaintext);

        return new FlowDecryptResult(aesKey, iv, Encoding.UTF8.GetString(plaintext));
    }

    /// <summary>
    ///     Encrypts a JSON response using the same AES key from the request.
    ///     The IV last byte is flipped per Meta's spec.
    /// </summary>
    public string Encrypt(string responseJson, byte[] aesKey, byte[] iv)
    {
        var responseIv = (byte[])iv.Clone();
        responseIv[^1] ^= 0xFF;

        var plaintext = Encoding.UTF8.GetBytes(responseJson);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[16];

        using var aes = new AesGcm(aesKey, 16);
        aes.Encrypt(responseIv, plaintext, ciphertext, tag);

        var result = new byte[ciphertext.Length + 16];
        ciphertext.CopyTo(result, 0);
        tag.CopyTo(result, ciphertext.Length);
        return Convert.ToBase64String(result);
    }
}

public sealed record FlowDecryptResult(byte[] AesKey, byte[] Iv, string Json);
