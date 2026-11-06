using System.Net;
using System.Security.Cryptography;
using System.Text.Json;

namespace Dvinholm.App.Tests;

public sealed class UnifiedX64SignatureFetcherTests
{
    private const string Tag = "v0.3.0";
    private const string BaseUrl =
        "https://github.com/vorlocsev/dvinholm/releases/download/v0.3.0/";
    private const string CdnUrl =
        "https://release-assets.githubusercontent.com/test-asset?response-content-disposition=attachment";

    private sealed class FakeHandler(Func<Uri, HttpResponseMessage> respond)
        : HttpMessageHandler
    {
        internal int Requests;
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests++;
            return Task.FromResult(respond(request.RequestUri!));
        }
    }

    private static void InFixture(Action<string, UnifiedX64UpdateCandidate,
        UnifiedX64StagedPackage, byte[]> test)
    {
        var dir = Path.Combine(Path.GetTempPath(),
            "dvinholm-sig-fetch-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var zipFile = Path.Combine(dir, UnifiedX64ReleaseFeed.PackageAsset);
            var bytes = new byte[] { 0x50, 0x4b, 3, 4, 1, 2, 3, 4 };
            File.WriteAllBytes(zipFile, bytes);
            var sha = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
            var candidate = new UnifiedX64UpdateCandidate(Tag,
                UnifiedX64ReleaseFeed.PackageAsset, bytes.Length,
                BaseUrl + UnifiedX64ReleaseFeed.PackageAsset,
                BaseUrl + UnifiedX64ReleaseFeed.ChecksumsAsset,
                BaseUrl + UnifiedX64ReleaseFeed.SignatureAsset);
            var staged = new UnifiedX64StagedPackage(dir, zipFile,
                sha, bytes.Length, 4, 1024);
            using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var signed = key.SignData(
                UnifiedX64PublisherTrust.GetCanonicalPayload(Tag, sha),
                HashAlgorithmName.SHA256,
                DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
            test(dir, candidate, staged, UnifiedX64SignatureArtifact.Encode(signed));
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public void NoSignatureAssetDoesNotContactAnyServer() =>
        InFixture((dir, candidate, staged, signature) =>
        {
            var handler = new FakeHandler(_ => throw new InvalidOperationException("No request expected"));
            using var http = new HttpClient(handler);
            var found = UnifiedX64SignatureFetcher.FetchAsync(
                candidate with { SignatureUrl = null }, staged, http)
                .GetAwaiter().GetResult();
            Assert.False(found.Published);
            Assert.False(found.WellFormed);
            Assert.False(found.PublisherVerified);
            Assert.Equal(0, handler.Requests);
        });

    [Fact]
    public void CanonicalSignatureMayBeFetchedButIsNotPublisherTrusted() =>
        InFixture((dir, candidate, staged, signature) =>
        {
            var handler = new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) {
                Content = new ByteArrayContent(signature) });
            using var http = new HttpClient(handler);
            var found = UnifiedX64SignatureFetcher.FetchAsync(candidate, staged, http)
                .GetAwaiter().GetResult();
            Assert.True(found.Published);
            Assert.True(found.WellFormed);
            Assert.False(found.PublisherVerified);
            Assert.Equal(1, handler.Requests);
        });

    [Fact]
    public void GithubRedirectAllowsOnlyKnownReleaseCdnHost() =>
        InFixture((dir, candidate, staged, signature) =>
        {
            var handler = new FakeHandler(url =>
                url.Host == "github.com"
                    ? new HttpResponseMessage(HttpStatusCode.Redirect) {
                        Headers = { Location = new Uri(CdnUrl) } }
                    : new HttpResponseMessage(HttpStatusCode.OK) {
                        Content = new ByteArrayContent(signature) });
            using var http = new HttpClient(handler);
            Assert.True(UnifiedX64SignatureFetcher.FetchAsync(candidate, staged, http)
                .GetAwaiter().GetResult().WellFormed);
            Assert.Equal(2, handler.Requests);
        });

    [Fact]
    public void ForeignSignatureAssetUrlRejectedBeforeNetwork() =>
        InFixture((dir, candidate, staged, signature) =>
        {
            var handler = new FakeHandler(_ => throw new Exception("Must not request"));
            using var http = new HttpClient(handler);
            Assert.Throws<InvalidDataException>(() =>
                UnifiedX64SignatureFetcher.FetchAsync(candidate with {
                    SignatureUrl = "https://example.com/malware.sig"
                }, staged, http).GetAwaiter().GetResult());
            Assert.Equal(0, handler.Requests);
        });

    [Fact]
    public void RedirectToForeignOrInsecureHostIsRejected() =>
        InFixture((dir, candidate, staged, signature) =>
        {
            foreach (var redirect in new[] {
                "https://evil.example/signature", "http://release-assets.githubusercontent.com/file",
                "https://release-assets.githubusercontent.com.evil.example/path"
            })
            {
                var handler = new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.Redirect) {
                    Headers = { Location = new Uri(redirect) } });
                using var http = new HttpClient(handler);
                Assert.Throws<InvalidDataException>(() =>
                    UnifiedX64SignatureFetcher.FetchAsync(candidate, staged, http)
                        .GetAwaiter().GetResult());
                Assert.Equal(1, handler.Requests);
            }
        });

    [Fact]
    public void ExcessiveOrIncorrectSignatureLengthFailsClosed() =>
        InFixture((dir, candidate, staged, signature) =>
        {
            foreach (var content in new[] { new byte[91], signature[..^2] })
            {
                var handler = new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) {
                    Content = new ByteArrayContent(content) });
                using var http = new HttpClient(handler);
                Assert.Throws<InvalidDataException>(() =>
                    UnifiedX64SignatureFetcher.FetchAsync(candidate, staged, http)
                        .GetAwaiter().GetResult());
            }
        });

    [Fact]
    public void BadCanonicalBase64IsRejectedAfterTransport() =>
        InFixture((dir, candidate, staged, signature) =>
        {
            var altered = (byte[])signature.Clone();
            altered[0] = (byte)'_';
            var handler = new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) {
                Content = new ByteArrayContent(altered) });
            using var http = new HttpClient(handler);
            Assert.Throws<InvalidDataException>(() =>
                UnifiedX64SignatureFetcher.FetchAsync(candidate, staged, http)
                    .GetAwaiter().GetResult());
        });

    [Fact]
    public void TamperedStagedZipPreventsSignatureRequest() =>
        InFixture((dir, candidate, staged, signature) =>
        {
            File.WriteAllBytes(staged.ZipPath, new byte[] {1, 2, 3, 4, 5, 6, 7, 8});
            var handler = new FakeHandler(_ => throw new Exception("Network request forbidden"));
            using var http = new HttpClient(handler);
            Assert.Throws<InvalidDataException>(() =>
                UnifiedX64SignatureFetcher.FetchAsync(candidate, staged, http)
                    .GetAwaiter().GetResult());
            Assert.Equal(0, handler.Requests);
        });

    [Fact]
    public void ReleaseFeedDiscoversOnlyOneExactSignatureAsset()
    {
        var prefix = BaseUrl;
        object[] assets = [
            new { name = UnifiedX64ReleaseFeed.PackageAsset, size = 20000000,
                browser_download_url = prefix + UnifiedX64ReleaseFeed.PackageAsset },
            new { name = UnifiedX64ReleaseFeed.ChecksumsAsset, size = 200,
                browser_download_url = prefix + UnifiedX64ReleaseFeed.ChecksumsAsset },
            new { name = UnifiedX64ReleaseFeed.SignatureAsset, size = 89,
                browser_download_url = prefix + UnifiedX64ReleaseFeed.SignatureAsset }
        ];
        byte[] Metadata(object[] list) => JsonSerializer.SerializeToUtf8Bytes(new[] {
            new { tag_name = Tag, draft = false, prerelease = false, assets = list }
        });
        var signed = UnifiedX64ReleaseFeed.SelectCandidate(
            Metadata(assets), UpdateSourceChannel.Stable);
        Assert.NotNull(signed);
        Assert.Equal(prefix + UnifiedX64ReleaseFeed.SignatureAsset, signed.SignatureUrl);
        var duplicated = UnifiedX64ReleaseFeed.SelectCandidate(
            Metadata([..assets, assets[2]]), UpdateSourceChannel.Stable);
        Assert.NotNull(duplicated);
        Assert.Null(duplicated.SignatureUrl);
    }
}
