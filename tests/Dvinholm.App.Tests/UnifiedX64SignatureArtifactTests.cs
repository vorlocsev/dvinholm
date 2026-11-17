using System.Security.Cryptography;
using System.Text;

namespace Dvinholm.App.Tests;

/// <summary>
/// Eight offline release-signature transport and cryptographic safety gates.
/// All keys here are ephemeral test fixtures; none is a production trust root.
/// </summary>
public sealed class UnifiedX64SignatureArtifactTests
{
    private const string Tag = "v0.3.0";
    private static readonly string Digest = new string('a', 64);

    private static (string PublicKey, byte[] Signature) Sign(string tag = Tag, string? digest = null)
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var bytes = key.SignData(
            UnifiedX64PublisherTrust.GetCanonicalPayload(tag, digest ?? Digest),
            HashAlgorithmName.SHA256,
            DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        return (key.ExportSubjectPublicKeyInfoPem(), bytes);
    }

    [Fact]
    public void CanonicalBase64SignatureRoundTripsAndVerifies()
    {
        var (pem, signature) = Sign();
        var encoded = UnifiedX64SignatureArtifact.Encode(signature);
        Assert.Equal(89, encoded.Length);
        Assert.Equal((byte)'\n', encoded[^1]);
        Assert.Equal(signature, UnifiedX64SignatureArtifact.Parse(encoded));
        Assert.True(UnifiedX64SignatureArtifact.VerifyWithExplicitPublicKey(
            pem, Tag, Digest, encoded));
    }

    [Fact]
    public void MissingProductionTrustRootRefusesEvenValidSignature()
    {
        var (_, signature) = Sign();
        Assert.False(UnifiedX64PublisherTrust.HasTrustedPublisherKey);
        Assert.False(UnifiedX64SignatureArtifact.VerifyPinned(
            Tag, Digest, UnifiedX64SignatureArtifact.Encode(signature)));
    }

    [Fact]
    public void WrongKeyTagAndDigestAllRejectTheSameSignature()
    {
        var (pem, signature) = Sign();
        using var wrongKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var encoded = UnifiedX64SignatureArtifact.Encode(signature);
        Assert.False(UnifiedX64SignatureArtifact.VerifyWithExplicitPublicKey(
            wrongKey.ExportSubjectPublicKeyInfoPem(), Tag, Digest, encoded));
        Assert.False(UnifiedX64SignatureArtifact.VerifyWithExplicitPublicKey(
            pem, "v0.3.1", Digest, encoded));
        Assert.False(UnifiedX64SignatureArtifact.VerifyWithExplicitPublicKey(
            pem, Tag, new string('b', 64), encoded));
    }

    [Fact]
    public void WrongLengthAndTrailingDataAreRejected()
    {
        var (_, signature) = Sign();
        var encoded = UnifiedX64SignatureArtifact.Encode(signature);
        foreach (byte[] invalid in new byte[][] {
            encoded[..^2], [.. encoded, (byte)'\n'], [.. encoded, (byte)'X']
        })
            Assert.Throws<InvalidDataException>(() =>
                UnifiedX64SignatureArtifact.Parse(invalid));
        Assert.Throws<ArgumentException>(() =>
            UnifiedX64SignatureArtifact.Encode(new byte[63]));
    }

    [Fact]
    public void CrLfBomAndWhitespaceCannotChangeCanonicalPayload()
    {
        var (_, signature) = Sign();
        var encoded = UnifiedX64SignatureArtifact.Encode(signature);
        foreach (byte[] invalid in new byte[][] {
            [.. encoded[..^1], (byte)'\r', (byte)'\n'],
            [ (byte)0xEF, (byte)0xBB, (byte)0xBF, ..encoded],
            [ (byte)' ', .. encoded[..^1]],
            [..encoded[..^1], (byte)' ']
        })
            Assert.Throws<InvalidDataException>(() =>
                UnifiedX64SignatureArtifact.Parse(invalid));
    }

    [Fact]
    public void InvalidAlphabetPaddingAndNonCanonicalBase64AreRejected()
    {
        var (_, signature) = Sign();
        var encoded = UnifiedX64SignatureArtifact.Encode(signature);
        foreach (var b in new[] { (byte)'_', (byte)'-', (byte)'?', (byte)' ' })
        {
            var invalid = (byte[])encoded.Clone();
            invalid[0] = b;
            Assert.Throws<InvalidDataException>(() =>
                UnifiedX64SignatureArtifact.Parse(invalid));
        }
        var badPadding = (byte[])encoded.Clone();
        badPadding[^3] = (byte)'A';
        Assert.Throws<InvalidDataException>(() =>
            UnifiedX64SignatureArtifact.Parse(badPadding));
    }

    [Fact]
    public void PublicKeyFingerprintIsStableForPemWhitespace()
    {
        var (pem, _) = Sign();
        var first = UnifiedX64SignatureArtifact.FingerprintPublicKey(pem);
        var second = UnifiedX64SignatureArtifact.FingerprintPublicKey(
            "\n" + pem.Trim() + "\n");
        Assert.Equal(first, second);
        Assert.Matches("^[a-f0-9]{64}$", first);
    }

    [Fact]
    public void MalformedOrUntrustedPublicKeyIsNeverAccepted()
    {
        var (_, signature) = Sign();
        var bytes = UnifiedX64SignatureArtifact.Encode(signature);
        Assert.False(UnifiedX64SignatureArtifact.VerifyWithExplicitPublicKey(
            "not a pem", Tag, Digest, bytes));
        Assert.False(UnifiedX64SignatureArtifact.VerifyWithExplicitPublicKey(
            string.Empty, Tag, Digest, bytes));
        Assert.ThrowsAny<Exception>(() =>
            UnifiedX64SignatureArtifact.FingerprintPublicKey("not a pem"));
    }
}
