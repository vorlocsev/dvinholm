using System.Security.Cryptography;

namespace Dvinholm.App.Tests;

/// <summary>
/// Eight independent offline key-review cases. Ephemeral test keys are never
/// installed, pinned, embedded, uploaded or used for a real Dvinholm release.
/// </summary>
public sealed class UnifiedX64PublisherKeyReviewTests
{
    private static (string Pem, string Fingerprint) CreateP256()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var pem = key.ExportSubjectPublicKeyInfoPem();
        var fingerprint = Convert.ToHexString(SHA256.HashData(
            key.ExportSubjectPublicKeyInfo())).ToLowerInvariant();
        return (pem, fingerprint);
    }

    [Fact]
    public void IndependentlyMatchingFingerprintIsForReviewNotProductionTrust()
    {
        var (pem, approved) = CreateP256();
        var result = UnifiedX64PublisherKeyReview.Inspect(pem, approved);
        Assert.True(result.MatchesExpected);
        Assert.Equal(approved, result.SpkiSha256);
        Assert.False(result.TrustedForProduction);
        Assert.False(UnifiedX64PublisherTrust.HasTrustedPublisherKey);
    }

    [Fact]
    public void MismatchedFingerprintCannotBeAutoEnrolled()
    {
        var (pem, approved) = CreateP256();
        var other = approved[0] == '0' ? "1" + approved[1..] : "0" + approved[1..];
        var result = UnifiedX64PublisherKeyReview.Inspect(pem, other);
        Assert.False(result.MatchesExpected);
        Assert.False(result.TrustedForProduction);
    }

    [Fact]
    public void InvalidOrMissingApprovedFingerprintFailsClosed()
    {
        var (pem, approved) = CreateP256();
        foreach (var digest in new[] { "", "abc", approved.ToUpperInvariant(),
            approved + "00", " " + approved, new string('g', 64) })
            Assert.Throws<InvalidDataException>(() =>
                UnifiedX64PublisherKeyReview.Inspect(pem, digest));
    }

    [Fact]
    public void ExportedPrivateKeyIsRejectedEvenWithCorrectPublicFingerprint()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var hash = Convert.ToHexString(SHA256.HashData(
            key.ExportSubjectPublicKeyInfo())).ToLowerInvariant();
        var privateKey = key.ExportPkcs8PrivateKeyPem();
        Assert.Throws<InvalidDataException>(() =>
            UnifiedX64PublisherKeyReview.Inspect(privateKey, hash));
    }

    [Fact]
    public void WrongCurveAndLargerEcdsaKeyAreRejected()
    {
        using var p384 = ECDsa.Create(ECCurve.NamedCurves.nistP384);
        var pem = p384.ExportSubjectPublicKeyInfoPem();
        var digest = Convert.ToHexString(SHA256.HashData(
            p384.ExportSubjectPublicKeyInfo())).ToLowerInvariant();
        Assert.Throws<InvalidDataException>(() =>
            UnifiedX64PublisherKeyReview.Inspect(pem, digest));
    }

    [Fact]
    public void RsaOrGarbagePemIsRejected()
    {
        using var rsa = RSA.Create(2048);
        var rsaPem = rsa.ExportSubjectPublicKeyInfoPem();
        var digest = Convert.ToHexString(SHA256.HashData(
            rsa.ExportSubjectPublicKeyInfo())).ToLowerInvariant();
        Assert.Throws<InvalidDataException>(() =>
            UnifiedX64PublisherKeyReview.Inspect(rsaPem, digest));
        Assert.Throws<InvalidDataException>(() =>
            UnifiedX64PublisherKeyReview.Inspect("not a PEM key", new string('a', 64)));
    }

    [Fact]
    public void PemWhitespaceDoesNotChangeSpkiFingerprint()
    {
        var (pem, digest) = CreateP256();
        var padded = "\n" + pem.Trim() + "\n";
        var result = UnifiedX64PublisherKeyReview.Inspect(padded, digest);
        Assert.True(result.MatchesExpected);
        Assert.Equal(digest, result.SpkiSha256);
        Assert.False(result.TrustedForProduction);
    }

    [Fact]
    public void SameExactFingerprintDoesNotBypassPublisherInstallBlockers()
    {
        var (pem, digest) = CreateP256();
        Assert.True(UnifiedX64PublisherKeyReview.Inspect(pem, digest).MatchesExpected);
        Assert.False(UnifiedX64PublisherTrust.HasTrustedPublisherKey);
        Assert.False(UnifiedX64PublisherTrust.Verify("v0.3.0",
            new string('a', 64), new byte[64]));
        Assert.False(new UnifiedX64InstallReadiness("v0.3.0",
            Array.Empty<string>()).CanInstall);
    }
}
