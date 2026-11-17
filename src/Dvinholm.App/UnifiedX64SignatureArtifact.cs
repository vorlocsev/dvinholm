using System.Security.Cryptography;
using System.Text;

namespace Dvinholm.App;

/// <summary>
/// Strict binary-safe transport format for the future detached x64 signature:
/// 64-byte ECDSA P-256 IEEE P1363 signature encoded as exactly one canonical
/// 88-character ASCII Base64 line (optional single trailing LF).
/// </summary>
internal static class UnifiedX64SignatureArtifact
{
    internal const int SignatureBytes = 64;
    internal const int EncodedChars = 88;

    internal static byte[] Parse(ReadOnlySpan<byte> encoded)
    {
        if (encoded.Length == EncodedChars + 1 && encoded[^1] == (byte)'\n')
            encoded = encoded[..^1];

        if (encoded.Length != EncodedChars)
            throw new InvalidDataException("Invalid detached signature length.");

        // Reject BOM, CRLF, whitespace, alternative Base64 alphabets and any
        // non-ASCII bytes. The canonical serializer uses ordinary RFC 4648
        // Base64, not URL-safe or PEM armor.
        foreach (var b in encoded)
            if (!((b >= 'A' && b <= 'Z') ||
                  (b >= 'a' && b <= 'z') ||
                  (b >= '0' && b <= '9') || b == '+' || b == '/' || b == '='))
                throw new InvalidDataException("Non-canonical signature encoding.");
        if (encoded[^1] != '=' || encoded[^2] != '=')
            throw new InvalidDataException("Invalid P-256 signature padding.");
        if (encoded[..^2].Contains((byte)'='))
            throw new InvalidDataException("Embedded Base64 padding is forbidden.");

        var text = Encoding.ASCII.GetString(encoded);
        Span<byte> signature = stackalloc byte[SignatureBytes];
        if (!Convert.TryFromBase64String(text, signature, out var written) ||
            written != SignatureBytes ||
            !Convert.ToBase64String(signature).Equals(text, StringComparison.Ordinal))
            throw new InvalidDataException("Invalid or non-canonical P-256 signature.");
        return signature.ToArray();
    }

    internal static byte[] Encode(ReadOnlySpan<byte> rawSignature)
    {
        if (rawSignature.Length != SignatureBytes)
            throw new ArgumentException("P-256 P1363 signature must contain exactly 64 bytes.");
        return Encoding.ASCII.GetBytes(Convert.ToBase64String(rawSignature) + "\n");
    }

    /// <summary>
    /// Identifies the exact public key independently pinned by the publisher.
    /// This fingerprint never causes a new key to become trusted.
    /// </summary>
    internal static string FingerprintPublicKey(string publicKeyPem)
    {
        using var key = ECDsa.Create();
        key.ImportFromPem(publicKeyPem);
        if (key.KeySize != 256)
            throw new InvalidDataException("Publisher signing key must use P-256.");
        var der = key.ExportSubjectPublicKeyInfo();
        return Convert.ToHexString(SHA256.HashData(der)).ToLowerInvariant();
    }

    internal static bool VerifyWithExplicitPublicKey(
        string publicKeyPem, string tag, string zipSha256,
        ReadOnlySpan<byte> encodedSignature)
    {
        try
        {
            return UnifiedX64PublisherTrust.VerifyUsingTrustedPublicKey(
                publicKeyPem, tag, zipSha256, Parse(encodedSignature));
        }
        catch (Exception ex) when (ex is InvalidDataException or
            CryptographicException or ArgumentException)
        {
            return false;
        }
    }

    /// <summary>
    /// Production entry point always uses the compiled, vetted publisher key.
    /// That key is not provisioned yet, so this continues to fail closed.
    /// </summary>
    internal static bool VerifyPinned(string tag, string zipSha256,
        ReadOnlySpan<byte> encodedSignature)
    {
        if (!UnifiedX64PublisherTrust.HasTrustedPublisherKey)
            return false;
        try
        {
            return UnifiedX64PublisherTrust.Verify(
                tag, zipSha256, Parse(encodedSignature));
        }
        catch (InvalidDataException)
        {
            return false;
        }
    }
}
