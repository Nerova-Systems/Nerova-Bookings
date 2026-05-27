using System.Security.Cryptography;
using System.Text;

namespace Account.Features.WhatsApp.Infrastructure;

/// <summary>
///     Result of a freshly generated RSA key pair.
/// </summary>
/// <param name="PublicKeyPem">PEM-encoded PKCS#8 SubjectPublicKeyInfo.</param>
/// <param name="EncryptedPrivateKeyBase64">Base64-encoded AES-256-GCM ciphertext of the PKCS#8 private key PEM.</param>
/// <param name="IvBase64">Base64-encoded 12-byte GCM nonce.</param>
/// <param name="Fingerprint">Lowercase hex SHA-256 fingerprint of the public key PEM bytes.</param>
public sealed record WabaKeyPairResult(
    string PublicKeyPem,
    string EncryptedPrivateKeyBase64,
    string IvBase64,
    string Fingerprint
);

public interface IWabaEncryptionService
{
    /// <summary>
    ///     Generates a fresh RSA-2048 key pair. Returns the PEM-encoded public key and the AES-256-GCM
    ///     encrypted private key suitable for storage.
    /// </summary>
    WabaKeyPairResult GenerateKeyPair(string encryptionPassphrase);

    /// <summary>
    ///     Decrypts the stored private key PEM from its AES-256-GCM ciphertext back to plaintext PEM.
    /// </summary>
    string DecryptPrivateKey(string encryptedPrivateKey, string iv, string encryptionPassphrase);

    /// <summary>
    ///     Computes the SHA-256 fingerprint of the public key PEM as a lowercase hex string.
    /// </summary>
    string ComputePublicKeyFingerprint(string publicKeyPem);
}

public sealed class WabaEncryptionService : IWabaEncryptionService
{
    private const int RsaKeySize = 2048;
    private const int AesKeyBytes = 32; // 256-bit
    private const int GcmNonceBytes = 12;
    private const int GcmTagBytes = 16;
    private const int Pbkdf2Iterations = 100_000;
    private const int SaltBytes = 16;

    public WabaKeyPairResult GenerateKeyPair(string encryptionPassphrase)
    {
        using var rsa = RSA.Create(RsaKeySize);

        var publicKeyPem = rsa.ExportSubjectPublicKeyInfoPem();
        var privateKeyPem = rsa.ExportPkcs8PrivateKeyPem();

        var fingerprint = ComputePublicKeyFingerprint(publicKeyPem);

        var (encryptedBase64, ivBase64) = EncryptWithAesGcm(privateKeyPem, encryptionPassphrase);

        return new WabaKeyPairResult(publicKeyPem, encryptedBase64, ivBase64, fingerprint);
    }

    public string DecryptPrivateKey(string encryptedPrivateKey, string iv, string encryptionPassphrase)
    {
        var encryptedBytes = Convert.FromBase64String(encryptedPrivateKey);
        var nonce = Convert.FromBase64String(iv);

        // The first SaltBytes of encryptedBytes are the PBKDF2 salt.
        var salt = encryptedBytes[..SaltBytes];
        var ciphertextWithTag = encryptedBytes[SaltBytes..];

        var aesKey = DeriveKey(encryptionPassphrase, salt);

        var tag = ciphertextWithTag[^GcmTagBytes..];
        var ciphertext = ciphertextWithTag[..^GcmTagBytes];

        var plaintext = new byte[ciphertext.Length];

        using var aesGcm = new AesGcm(aesKey, GcmTagBytes);
        aesGcm.Decrypt(nonce, ciphertext, tag, plaintext);

        return Encoding.UTF8.GetString(plaintext);
    }

    public string ComputePublicKeyFingerprint(string publicKeyPem)
    {
        var pemBytes = Encoding.UTF8.GetBytes(publicKeyPem);
        var hashBytes = SHA256.HashData(pemBytes);
        return Convert.ToHexStringLower(hashBytes);
    }

    private static (string EncryptedBase64, string IvBase64) EncryptWithAesGcm(string plaintext, string passphrase)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltBytes);
        var nonce = RandomNumberGenerator.GetBytes(GcmNonceBytes);
        var aesKey = DeriveKey(passphrase, salt);

        var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
        var ciphertext = new byte[plaintextBytes.Length];
        var tag = new byte[GcmTagBytes];

        using var aesGcm = new AesGcm(aesKey, GcmTagBytes);
        aesGcm.Encrypt(nonce, plaintextBytes, ciphertext, tag);

        // Prepend salt, then ciphertext, then authentication tag — all in one base64 blob.
        var combined = new byte[SaltBytes + ciphertext.Length + GcmTagBytes];
        salt.CopyTo(combined, 0);
        ciphertext.CopyTo(combined, SaltBytes);
        tag.CopyTo(combined, SaltBytes + ciphertext.Length);

        return (Convert.ToBase64String(combined), Convert.ToBase64String(nonce));
    }

    private static byte[] DeriveKey(string passphrase, byte[] salt)
    {
        return Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(passphrase),
            salt,
            Pbkdf2Iterations,
            HashAlgorithmName.SHA256,
            AesKeyBytes
        );
    }
}
