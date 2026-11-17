using System.Text.Json;

namespace Dvinholm.App.Tests;

public sealed class UnifiedX64ReleaseFeedTests
{
    private static byte[] Releases(string tag, bool prerelease,
        string package = UnifiedX64ReleaseFeed.PackageAsset,
        bool checksum = true, int copies = 1,
        long bytes = 25_000_000, string? packageUrl = null)
    {
        var baseUrl = "https://github.com/vorlocsev/dvinholm/releases/download/" + tag + "/";
        var assets = Enumerable.Range(0, copies).Select(_ => new
        {
            name = package,
            size = bytes,
            browser_download_url = packageUrl ?? baseUrl + package
        }).ToList();
        if (checksum)
            assets.Add(new
            {
                name = UnifiedX64ReleaseFeed.ChecksumsAsset,
                size = 500L,
                browser_download_url = baseUrl + UnifiedX64ReleaseFeed.ChecksumsAsset
            });
        return JsonSerializer.SerializeToUtf8Bytes(new[]
        {
            new { tag_name = tag, draft = false, prerelease, assets }
        });
    }

    [Fact]
    public void StableReleaseRequiresExactX64PackageAndChecksumAsset()
    {
        var candidate = UnifiedX64ReleaseFeed.SelectCandidate(
            Releases("v0.3.0", false), UpdateSourceChannel.Stable);
        Assert.NotNull(candidate);
        Assert.Equal("v0.3.0", candidate.Tag);
        Assert.Equal("Dvinholm-Windows-x64.zip", candidate.PackageName);
        Assert.Equal(25_000_000, candidate.PackageBytes);
    }

    [Fact]
    public void DevelopmentReleaseRequiresExplicitCiPrerelease()
    {
        var candidate = UnifiedX64ReleaseFeed.SelectCandidate(
            Releases("ci-4700", true), UpdateSourceChannel.Development);
        Assert.NotNull(candidate);
        Assert.Equal("ci-4700", candidate.Tag);
        Assert.Null(UnifiedX64ReleaseFeed.SelectCandidate(
            Releases("ci-4700", false), UpdateSourceChannel.Development));
    }

    [Fact]
    public void X86AndLegacyLiteArchivesCannotImpersonateX64()
    {
        foreach (var package in new[] { "Dvinholm-Windows-x86.zip",
            "Dvinholm-Windows-x86-Lite.zip", "Dvinholm-Windows-x64-Lite.zip" })
            Assert.Null(UnifiedX64ReleaseFeed.SelectCandidate(
                Releases("v0.3.0", false, package: package),
                UpdateSourceChannel.Stable));
    }

    [Fact]
    public void MissingChecksumMetadataDoesNotProduceUpdateCandidate() =>
        Assert.Null(UnifiedX64ReleaseFeed.SelectCandidate(
            Releases("v0.3.0", false, checksum: false),
            UpdateSourceChannel.Stable));

    [Fact]
    public void DuplicatePackageEntryIsNeverArbitrarilySelected() =>
        Assert.Null(UnifiedX64ReleaseFeed.SelectCandidate(
            Releases("v0.3.0", false, copies: 2),
            UpdateSourceChannel.Stable));

    [Theory]
    [InlineData("http://github.com/vorlocsev/dvinholm/releases/download/v0.3.0/Dvinholm-Windows-x64.zip")]
    [InlineData("https://example.com/vorlocsev/dvinholm/releases/download/v0.3.0/Dvinholm-Windows-x64.zip")]
    [InlineData("https://github.com/other/dvinholm/releases/download/v0.3.0/Dvinholm-Windows-x64.zip")]
    [InlineData("https://github.com/vorlocsev/dvinholm/releases/download/v0.3.0/Dvinholm-Windows-x86.zip")]
    public void ForeignOrArchitectureMismatchedUrlIsRejected(string url) =>
        Assert.Null(UnifiedX64ReleaseFeed.SelectCandidate(
            Releases("v0.3.0", false, packageUrl: url), UpdateSourceChannel.Stable));

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(600_000_000)]
    public void ImplausiblePackageSizeCannotBeAdvertised(long bytes) =>
        Assert.Null(UnifiedX64ReleaseFeed.SelectCandidate(
            Releases("v0.3.0", false, bytes: bytes), UpdateSourceChannel.Stable));

    [Fact]
    public void ChannelMismatchAndUnexpectedJsonRootFailClosed()
    {
        Assert.Null(UnifiedX64ReleaseFeed.SelectCandidate(
            Releases("v0.3.0", false), UpdateSourceChannel.Development));
        Assert.Null(UnifiedX64ReleaseFeed.SelectCandidate(
            Releases("ci-4700", true), UpdateSourceChannel.Stable));
        Assert.Throws<InvalidDataException>(() =>
            UnifiedX64ReleaseFeed.SelectCandidate(
                JsonSerializer.SerializeToUtf8Bytes(new { message = "Unauthorized" }),
                UpdateSourceChannel.Stable));
    }
}
