using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Dvinholm.App;

/// <summary>
/// Detached ECDSA P-256/SHA-256 release attestation verifier.
/// No production publisher key has been provisioned yet: production trust
/// is deliberately blocked rather than inferred from GitHub or SHA256SUMS.
/// </summary>
internal static class UnifiedX64PublisherTrust
{
    internal const string SignatureAsset = "Dvinholm-Windows-x64.sig";
    // Provision ONLY after independently validating the publisher's signing
    // public key, release signing procedure and emergency key rotation policy.
    private const string PinnedPublisherPublicKeyPem = "";

    internal static bool HasTrustedPublisherKey =>
        !string.IsNullOrWhiteSpace(PinnedPublisherPublicKeyPem);

    internal static bool Verify(string tag, string zipSha256, byte[]? signature)
    {
        if (!HasTrustedPublisherKey || signature is null)
            return false;
        return VerifyUsingTrustedPublicKey(
            PinnedPublisherPublicKeyPem, tag, zipSha256, signature);
    }

    /// <summary>
    /// Key is supplied only for isolated cryptographic tests. The production
    /// entry point Verify() never accepts keys from the downloaded release.
    /// </summary>
    internal static bool VerifyUsingTrustedPublicKey(
        string publicKeyPem, string tag, string zipSha256,
        ReadOnlySpan<byte> signature)
    {
        if (string.IsNullOrWhiteSpace(publicKeyPem) ||
            signature.Length != 64 ||
            !Regex.IsMatch(tag, @"^(v[0-9]+\.[0-9]+\.[0-9]+|ci-[0-9]+)$",
                RegexOptions.CultureInvariant) ||
            !Regex.IsMatch(zipSha256, @"^[0-9a-f]{64}$",
                RegexOptions.CultureInvariant))
            return false;

        var payload = GetCanonicalPayload(tag, zipSha256);
        try
        {
            using var publicKey = ECDsa.Create();
            publicKey.ImportFromPem(publicKeyPem);
            if (publicKey.KeySize != 256)
                return false;
            return publicKey.VerifyData(
                payload, signature, HashAlgorithmName.SHA256,
                DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        }
        catch (CryptographicException)
        {
            return false;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    internal static byte[] GetCanonicalPayload(string tag, string zipSha256) =>
        Encoding.ASCII.GetBytes(
            "Dvinholm x64 release attestation v1\n" +
            "Tag: " + tag + "\n" +
            "Asset: Dvinholm-Windows-x64.zip\n" +
            "SHA256: " + zipSha256 + "\n");
}
