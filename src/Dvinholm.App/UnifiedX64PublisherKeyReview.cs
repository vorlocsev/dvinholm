using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace Dvinholm.App;

/// <summary>
/// Offline review helper for an independently authenticated publisher key.
/// It does not enroll the key into the application or authorize installation.
/// The expected SPKI SHA-256 must be supplied from a separate, trusted channel,
/// never extracted from the downloaded release or its signature.
/// </summary>
internal static class UnifiedX64PublisherKeyReview
{
    internal sealed record Result(
        string SpkiSha256, bool MatchesExpected, bool TrustedForProduction);

    internal static Result Inspect(string publicKeyPem, string independentlyApprovedSpkiSha256)
    {
        if (string.IsNullOrWhiteSpace(publicKeyPem) ||
            !Regex.IsMatch(independentlyApprovedSpkiSha256 ?? "",
                @"^[0-9a-f]{64}$", RegexOptions.CultureInvariant))
            throw new InvalidDataException("An independently approved lowercase SPKI SHA-256 is required.");

        // Explicitly refuse PEM private keys, including an accidentally
        // exported EC PRIVATE KEY, regardless of whether they contain
        // enough public data for a cryptographic verification operation.
        if (publicKeyPem.Contains("PRIVATE KEY", StringComparison.Ordinal) ||
            !publicKeyPem.Contains("-----BEGIN PUBLIC KEY-----", StringComparison.Ordinal) ||
            !publicKeyPem.Contains("-----END PUBLIC KEY-----", StringComparison.Ordinal) ||
            publicKeyPem.Length > 4096)
            throw new InvalidDataException("Only a bounded SPKI PUBLIC KEY PEM is accepted.");

        byte[] fingerprint;
        try
        {
            using var key = ECDsa.Create();
            key.ImportFromPem(publicKeyPem);
            var parameters = key.ExportParameters(includePrivateParameters: false);
            if (key.KeySize != 256 || parameters.Curve.Oid.Value !=
                ECCurve.NamedCurves.nistP256.Oid.Value)
                throw new InvalidDataException("ECDSA NIST P-256 is required.");

            fingerprint = SHA256.HashData(key.ExportSubjectPublicKeyInfo());
        }
        catch (Exception ex) when (ex is CryptographicException or ArgumentException)
        {
            throw new InvalidDataException("Invalid ECDSA public key.", ex);
        }

        var expected = Convert.FromHexString(independentlyApprovedSpkiSha256);
        var match = CryptographicOperations.FixedTimeEquals(fingerprint, expected);
        return new Result(Convert.ToHexString(fingerprint).ToLowerInvariant(),
            match, TrustedForProduction: false);
    }
}
