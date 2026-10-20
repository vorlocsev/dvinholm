using System.Net;
using System.Text;
using System.Text.Json;

namespace Dvinholm.App.Tests;

public sealed class LatestStableReleaseLookupTests
{
    private const string Sha = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string TagObjectSha = "cccccccccccccccccccccccccccccccccccccccc";
    private const string Digest = "sha256:bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
    private const string ZipUrl = "https://github.com/vorlocsev/dvinholm/releases/download/v0.2.2/Dvinholm-Windows-x86.zip";

    [Fact]
    public async Task PublishedStableRelease_UsesVerifiedLightweightGitTagAndExpectedArchive()
    {
        var requests = new List<string>();
        using var http = Client(request =>
        {
            requests.Add(request.RequestUri!.AbsolutePath);
            return request.RequestUri.AbsolutePath switch
            {
                "/repos/vorlocsev/dvinholm/releases/latest" => Json(Release()),
                "/repos/vorlocsev/dvinholm/git/ref/tags/v0.2.2" => Json(TagReference()),
                _ => throw new InvalidOperationException("Unexpected HTTP request")
            };
        });

        var release = await LatestBuildUpdater.GetLatestStableReleaseAsync(http);

        Assert.Equal(Sha, release.CommitSha);
        Assert.Equal("v0.2.2", release.ReleaseTag);
        Assert.Equal("0.2.2 · aaaaaaa", release.DisplayVersion);
        Assert.Equal(123456L, release.ArtifactSize);
        Assert.Equal(Digest, release.ArtifactDigest);
        Assert.Equal("https://api.github.com/repos/vorlocsev/dvinholm/releases/assets/7654", release.DownloadUrl);
        Assert.Equal(2, requests.Count);
    }

    [Fact]
    public async Task MatchingStableTagCommit_DoesNotOfferReplacementForCurrentStableOrCiInstall()
    {
        using var http = SuccessfulClient();
        var release = await LatestBuildUpdater.GetLatestStableReleaseAsync(http);

        Assert.False(LatestBuildUpdater.IsUpdateAvailable(Sha, release));
        var status = LatestBuildUpdater.FormatUpdateStatus("0.2.2 · aaaaaaa", release, false);
        Assert.Contains("Up to date (latest: 0.2.2 · aaaaaaa)", status);
        Assert.DoesNotContain("Available:", status);
    }

    [Fact]
    public async Task DifferentStableTagCommit_IsExplicitlyOfferedWithoutChangingCiChannel()
    {
        using var http = SuccessfulClient();
        var release = await LatestBuildUpdater.GetLatestStableReleaseAsync(http);

        Assert.True(LatestBuildUpdater.IsUpdateAvailable(new string('d', 40), release));
        Assert.Contains("Available: 0.2.2 · aaaaaaa",
            LatestBuildUpdater.FormatUpdateStatus("0.2.1 · ddddddd", release, true));
    }

    [Fact]
    public async Task AnnotatedTag_IsDereferencedToItsActualCommit()
    {
        var requests = new List<string>();
        using var http = Client(request =>
        {
            requests.Add(request.RequestUri!.AbsolutePath);
            return request.RequestUri.AbsolutePath switch
            {
                "/repos/vorlocsev/dvinholm/releases/latest" => Json(Release()),
                "/repos/vorlocsev/dvinholm/git/ref/tags/v0.2.2" => Json(TagReference("tag", TagObjectSha)),
                "/repos/vorlocsev/dvinholm/git/tags/" + TagObjectSha =>
                    Json(JsonSerializer.Serialize(new { @object = new { type = "commit", sha = Sha } })),
                _ => throw new InvalidOperationException("Unexpected HTTP request")
            };
        });

        var release = await LatestBuildUpdater.GetLatestStableReleaseAsync(http);

        Assert.Equal(Sha, release.CommitSha);
        Assert.Equal(3, requests.Count);
    }

    [Fact]
    public async Task MutableReleaseTargetCommitish_CannotOverrideVerifiedGitTag()
    {
        using var http = SuccessfulClient(Release(targetCommitish: "main"));
        var release = await LatestBuildUpdater.GetLatestStableReleaseAsync(http);

        Assert.Equal(Sha, release.CommitSha);
    }

    [Theory]
    [InlineData("v0.2")]
    [InlineData("ci-2878")]
    [InlineData("v0.2.2-rc.1")]
    [InlineData("v0.2.xyz")]
    [InlineData("0.2.2")]
    public async Task InvalidVersionTag_IsRejectedBeforeGitObjectLookup(string tag)
    {
        using var http = Client(request =>
        {
            Assert.EndsWith("/releases/latest", request.RequestUri!.AbsolutePath);
            return Json(Release(tag: tag));
        });

        await Assert.ThrowsAsync<InvalidDataException>(
            () => LatestBuildUpdater.GetLatestStableReleaseAsync(http));
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task DraftOrPrerelease_IsNeverUsedForStableUpdates(bool draft, bool prerelease)
    {
        using var http = Client(request =>
        {
            Assert.EndsWith("/releases/latest", request.RequestUri!.AbsolutePath);
            return Json(Release(draft: draft, prerelease: prerelease));
        });

        await Assert.ThrowsAsync<InvalidDataException>(
            () => LatestBuildUpdater.GetLatestStableReleaseAsync(http));
    }

    [Theory]
    [InlineData("https://github.com/vorlocsev/dvinholm/releases/download/ci-2878/Dvinholm-Windows-x86.zip",
        123456L, Digest, 7654L)]
    [InlineData("https://evil.example/dvinholm.zip", 123456L, Digest, 7654L)]
    [InlineData(ZipUrl + "?token=secret", 123456L, Digest, 7654L)]
    [InlineData(ZipUrl, 0L, Digest, 7654L)]
    [InlineData(ZipUrl, 524288001L, Digest, 7654L)]
    [InlineData(ZipUrl, 123456L, "", 7654L)]
    [InlineData(ZipUrl, 123456L, "sha256:invalid", 7654L)]
    [InlineData(ZipUrl, 123456L, Digest, 0L)]
    public async Task UnsafeOrIncompleteStableAssetMetadata_IsRejected(
        string url, long size, string digest, long assetId)
    {
        using var http = SuccessfulClient(Release(url: url, size: size, digest: digest, assetId: assetId));

        await Assert.ThrowsAsync<InvalidDataException>(
            () => LatestBuildUpdater.GetLatestStableReleaseAsync(http));
    }

    [Fact]
    public async Task MissingPortableZip_FailsClosed()
    {
        using var http = SuccessfulClient(Release(assetName: "SHA256SUMS.txt"));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => LatestBuildUpdater.GetLatestStableReleaseAsync(http));
    }

    [Theory]
    [InlineData("blob", Sha)]
    [InlineData("commit", "bad-sha")]
    public async Task NonCommitOrMalformedTagTarget_IsRejected(string type, string sha)
    {
        using var http = Client(request =>
            request.RequestUri!.AbsolutePath.EndsWith("/releases/latest", StringComparison.Ordinal)
                ? Json(Release())
                : Json(TagReference(type, sha)));

        await Assert.ThrowsAsync<InvalidDataException>(
            () => LatestBuildUpdater.GetLatestStableReleaseAsync(http));
    }

    [Fact]
    public async Task HttpRateLimit_StopsLookupWithVisibleErrorRatherThanOfferingAnUnverifiedBuild()
    {
        using var http = Client(_ => new HttpResponseMessage(HttpStatusCode.Forbidden));

        var error = await Assert.ThrowsAsync<HttpRequestException>(
            () => LatestBuildUpdater.GetLatestStableReleaseAsync(http));
        Assert.Equal(HttpStatusCode.Forbidden, error.StatusCode);
    }

    [Theory]
    [InlineData(true, "dddddddddddddddddddddddddddddddddddddddd", "v0.2.2", true)]
    [InlineData(true, "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", "v0.2.2", false)]
    [InlineData(false, "dddddddddddddddddddddddddddddddddddddddd", "v0.2.2", false)]
    [InlineData(true, "dddddddddddddddddddddddddddddddddddddddd", null, false)]
    [InlineData(true, null, "v0.2.2", true)]
    public void StableReplacementRequiresExplicitConfirmationForDifferentCommit(
        bool isStableChannel, string? currentSha, string? releaseTag, bool expected)
    {
        var candidate = new LatestBuildInfo(
            Sha, 0, 0, ZipUrl, 123456, Digest, "2026-10-20T04:35:06Z", releaseTag);

        Assert.Equal(expected,
            LatestBuildUpdater.RequiresStableReplacementConfirmation(
                isStableChannel, currentSha, candidate));
    }

    private static HttpClient SuccessfulClient(string? releaseJson = null) =>
        Client(request => request.RequestUri!.AbsolutePath.EndsWith("/releases/latest", StringComparison.Ordinal)
            ? Json(releaseJson ?? Release())
            : Json(TagReference()));

    private static string Release(
        string tag = "v0.2.2",
        bool draft = false,
        bool prerelease = false,
        string url = ZipUrl,
        long size = 123456L,
        string digest = Digest,
        long assetId = 7654L,
        string assetName = "Dvinholm-Windows-x86.zip",
        string targetCommitish = "main") =>
        JsonSerializer.Serialize(new
        {
            tag_name = tag,
            draft,
            prerelease,
            published_at = "2026-10-20T04:35:06Z",
            target_commitish = targetCommitish,
            assets = new[]
            {
                new
                {
                    id = assetId,
                    name = assetName,
                    size,
                    browser_download_url = url,
                    digest
                }
            }
        });

    private static string TagReference(string type = "commit", string sha = Sha) =>
        JsonSerializer.Serialize(new { @object = new { type, sha } });

    private static HttpResponseMessage Json(string json) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

    private static HttpClient Client(Func<HttpRequestMessage, HttpResponseMessage> factory) =>
        new(new StubHandler(factory));

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> factory) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(factory(request));
    }
}
