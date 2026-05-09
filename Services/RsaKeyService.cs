using System.Security.Cryptography;
using System.Text;

namespace RealEstateApi.Services;

public interface IRsaKeyService
{
    /// <summary>Public key in JWK format — safe to ship to browsers.</summary>
    object GetPublicJwk();

    /// <summary>Decrypt a base64-encoded RSA-OAEP-SHA256 ciphertext.</summary>
    string Decrypt(string base64Ciphertext);
}

/// <summary>
/// Generates an RSA-2048 keypair at startup (held in memory for the lifetime
/// of the process) and exposes the public half so browsers can encrypt the
/// password field before it's sent over the wire. Even with DevTools open,
/// the request payload becomes ciphertext instead of "Admin@123".
///
/// Restart-of-process rotates the keys — clients refetch on every login,
/// so this is fine. For a true rotation policy across multiple instances,
/// load keys from a config / KMS instead.
/// </summary>
public class RsaKeyService : IRsaKeyService
{
    private readonly RSA _rsa;
    private readonly object _publicJwk;

    public RsaKeyService()
    {
        _rsa = RSA.Create(2048);

        // Pre-compute the JWK once — it never changes for this instance
        var p = _rsa.ExportParameters(includePrivateParameters: false);
        _publicJwk = new
        {
            kty = "RSA",
            alg = "RSA-OAEP-256",
            use = "enc",
            n = Base64UrlEncode(p.Modulus!),
            e = Base64UrlEncode(p.Exponent!),
        };
    }

    public object GetPublicJwk() => _publicJwk;

    public string Decrypt(string base64Ciphertext)
    {
        var ciphertext = Convert.FromBase64String(base64Ciphertext);
        var plaintext = _rsa.Decrypt(ciphertext, RSAEncryptionPadding.OaepSHA256);
        return Encoding.UTF8.GetString(plaintext);
    }

    private static string Base64UrlEncode(byte[] data)
    {
        return Convert.ToBase64String(data)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }
}
