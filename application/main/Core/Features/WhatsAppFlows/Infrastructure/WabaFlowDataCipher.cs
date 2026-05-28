using System.Security.Cryptography;
using System.Text;
using JetBrains.Annotations;
using Microsoft.Extensions.Configuration;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Parameters;
using SharedKernel.Cqrs;

namespace Main.Features.WhatsAppFlows.Infrastructure;

/// <summary>
///     Plaintext of a decrypted Meta WhatsApp Flow request together with the AES key + IV
///     needed to encrypt the matching response.
/// </summary>
[PublicAPI]
public sealed record DecryptedFlowRequest(byte[] AesKey, byte[] InitialVector, string PlaintextJson);

public interface IWabaFlowDataCipher
{
    /// <summary>
    ///     Decrypts a Meta-encrypted WhatsApp Flow request. The encrypted private key blob + IV come
    ///     from the tenant's WABA profile; the three base64 fields come from the Meta request body.
    /// </summary>
    Result<DecryptedFlowRequest> Decrypt(
        string encryptedAesKeyBase64,
        string encryptedFlowDataBase64,
        string initialVectorBase64,
        string encryptedPrivateKeyBase64,
        string privateKeyIvBase64
    );

    /// <summary>
    ///     Encrypts a Meta response. Per the protocol the AES key is reused and the IV is bitwise
    ///     inverted before encryption. Returns base64(ciphertext || GCM tag).
    /// </summary>
    string Encrypt(string plaintextJson, byte[] aesKey, byte[] requestInitialVector);
}

/// <summary>
///     Implements Meta's WhatsApp Flow encrypted-endpoint protocol. Layered on top of the
///     at-rest key encryption used in the account SCS:
///     <list type="number">
///         <item>Unwrap the tenant's RSA private key from its AES-256-GCM-with-PBKDF2 envelope.</item>
///         <item>RSA-OAEP-SHA256 decrypt the per-request AES-128 key.</item>
///         <item>AES-128-GCM decrypt the request body using the supplied IV.</item>
///         <item>For the response: AES-128-GCM encrypt with the same key and a bit-inverted IV.</item>
///     </list>
/// </summary>
public sealed class WabaFlowDataCipher(IConfiguration configuration) : IWabaFlowDataCipher
{
    private const int AesKeyBytes = 32; // private-key wrapper uses AES-256
    private const int GcmTagBytes = 16;
    private const int SaltBytes = 16;
    private const int Pbkdf2Iterations = 100_000;

    public Result<DecryptedFlowRequest> Decrypt(
        string encryptedAesKeyBase64,
        string encryptedFlowDataBase64,
        string initialVectorBase64,
        string encryptedPrivateKeyBase64,
        string privateKeyIvBase64
    )
    {
        byte[] encryptedAesKey;
        byte[] encryptedFlowData;
        byte[] initialVector;
        try
        {
            encryptedAesKey = Convert.FromBase64String(encryptedAesKeyBase64);
            encryptedFlowData = Convert.FromBase64String(encryptedFlowDataBase64);
            initialVector = Convert.FromBase64String(initialVectorBase64);
        }
        catch (FormatException)
        {
            return Result<DecryptedFlowRequest>.BadRequest("Request payload contains invalid base64.");
        }

        string privateKeyPem;
        try
        {
            privateKeyPem = UnwrapPrivateKeyPem(encryptedPrivateKeyBase64, privateKeyIvBase64);
        }
        catch (Exception)
        {
            return Result<DecryptedFlowRequest>.BadRequest("Failed to unwrap tenant private key.");
        }

        byte[] aesKey;
        try
        {
            using var rsa = RSA.Create();
            rsa.ImportFromPem(privateKeyPem);
            aesKey = rsa.Decrypt(encryptedAesKey, RSAEncryptionPadding.OaepSHA256);
        }
        catch (Exception)
        {
            return Result<DecryptedFlowRequest>.BadRequest("Failed to RSA-decrypt the AES key.");
        }

        if (encryptedFlowData.Length < GcmTagBytes)
        {
            return Result<DecryptedFlowRequest>.BadRequest("Encrypted flow data is too short.");
        }

        var ciphertext = encryptedFlowData[..^GcmTagBytes];
        var tag = encryptedFlowData[^GcmTagBytes..];
        byte[] plaintext;

        try
        {
            // .NET's AesGcm only supports 12-byte nonces, but Meta uses a 16-byte IV. We use
            // BouncyCastle here so we can pass the IV through verbatim.
            plaintext = AesGcmDecrypt(aesKey, initialVector, ciphertext, tag);
        }
        catch (Exception)
        {
            return Result<DecryptedFlowRequest>.BadRequest("AES-GCM decryption failed.");
        }

        return Result<DecryptedFlowRequest>.Success(
            new DecryptedFlowRequest(aesKey, initialVector, Encoding.UTF8.GetString(plaintext))
        );
    }

    public string Encrypt(string plaintextJson, byte[] aesKey, byte[] requestInitialVector)
    {
        var flippedIv = new byte[requestInitialVector.Length];
        for (var i = 0; i < requestInitialVector.Length; i++)
        {
            flippedIv[i] = (byte)~requestInitialVector[i];
        }

        var plaintextBytes = Encoding.UTF8.GetBytes(plaintextJson);
        var encrypted = AesGcmEncrypt(aesKey, flippedIv, plaintextBytes);
        return Convert.ToBase64String(encrypted);
    }

    private static byte[] AesGcmEncrypt(byte[] key, byte[] iv, byte[] plaintext)
    {
        var cipher = new GcmBlockCipher(new AesEngine());
        cipher.Init(true, new AeadParameters(new KeyParameter(key), GcmTagBytes * 8, iv));
        var output = new byte[cipher.GetOutputSize(plaintext.Length)];
        var offset = cipher.ProcessBytes(plaintext, 0, plaintext.Length, output, 0);
        cipher.DoFinal(output, offset);
        return output;
    }

    private static byte[] AesGcmDecrypt(byte[] key, byte[] iv, byte[] ciphertext, byte[] tag)
    {
        var cipher = new GcmBlockCipher(new AesEngine());
        cipher.Init(false, new AeadParameters(new KeyParameter(key), GcmTagBytes * 8, iv));
        var combined = new byte[ciphertext.Length + tag.Length];
        ciphertext.CopyTo(combined, 0);
        tag.CopyTo(combined, ciphertext.Length);
        var output = new byte[cipher.GetOutputSize(combined.Length)];
        var offset = cipher.ProcessBytes(combined, 0, combined.Length, output, 0);
        cipher.DoFinal(output, offset);
        return output;
    }

    private string UnwrapPrivateKeyPem(string encryptedPrivateKeyBase64, string ivBase64)
    {
        var passphrase = configuration["WhatsApp:EncryptionPassphrase"] ?? string.Empty;
        var encryptedBytes = Convert.FromBase64String(encryptedPrivateKeyBase64);
        var nonce = Convert.FromBase64String(ivBase64);

        // Account SCS layout: [salt(16) || ciphertext || gcm-tag(16)] in encryptedBytes, IV separate.
        var salt = encryptedBytes[..SaltBytes];
        var ciphertextWithTag = encryptedBytes[SaltBytes..];
        var tag = ciphertextWithTag[^GcmTagBytes..];
        var ciphertext = ciphertextWithTag[..^GcmTagBytes];

        var aesKey = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(passphrase),
            salt,
            Pbkdf2Iterations,
            HashAlgorithmName.SHA256,
            AesKeyBytes
        );

        var plaintext = new byte[ciphertext.Length];
        using var aesGcm = new AesGcm(aesKey, GcmTagBytes);
        aesGcm.Decrypt(nonce, ciphertext, tag, plaintext);

        return Encoding.UTF8.GetString(plaintext);
    }
}
