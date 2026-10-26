namespace Dvinholm.App.Tests;

public sealed class StableVersionStatusTests
{
    private const string FrozenSha = "a9e61883a413d4afaebb86fb468987c904250092";
    private const string FutureSha = "1234567890abcdef1234567890abcdef12345678";

    [Fact]
    public void InstalledStable_ShowsVersionOnly_WithoutShaOrChannelSuffix()
    {
        Assert.Equal("0.2.4",
            LatestBuildUpdater.FormatInstalledVersion($"0.2.4+{FrozenSha}.stable"));
    }

    [Fact]
    public void InstalledStable_ParsesExactCommit_ForUpToDateComparison()
    {
        var parsed = LatestBuildUpdater.ParseCommitSha($"0.2.4+{FrozenSha}.stable");
        var latest = Release("v0.2.4", FrozenSha);
        Assert.Equal(FrozenSha, parsed);
        Assert.False(LatestBuildUpdater.IsUpdateAvailable(parsed, latest));
    }

    [Fact]
    public void CorruptedStableMetadata_DoesNotClaimMatchingCommit()
    {
        foreach (var value in new[] {
            "0.2.4+short.stable",
            "0.2.4+zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz.stable",
            $"0.2.4+{FrozenSha}.stable.untrusted"
        })
        {
            Assert.Null(LatestBuildUpdater.ParseCommitSha(value));
            Assert.False(LatestBuildUpdater.IsStableInformationalVersion(value));
        }
    }

    [Fact]
    public void DevelopmentInstalledVersion_StillDisplaysCiRunAndShortSha()
    {
        Assert.Equal("CI #3567 · a9e6188",
            LatestBuildUpdater.FormatInstalledVersion($"0.2.4+{FrozenSha}.ci3567"));
        Assert.Equal(FrozenSha,
            LatestBuildUpdater.ParseCommitSha($"0.2.4+{FrozenSha}.ci3567"));
    }

    [Fact]
    public void StableAndDevelopmentMetadata_AreDistinguished()
    {
        Assert.True(LatestBuildUpdater.IsStableInformationalVersion($"0.2.4+{FrozenSha}.stable"));
        Assert.False(LatestBuildUpdater.IsStableInformationalVersion($"0.2.4+{FrozenSha}.ci3582"));
        Assert.Equal("0.2.4", LatestBuildUpdater.FormatInstalledVersion($"0.2.4+{FrozenSha}.STABLE"));
    }

    [Fact]
    public void PublishedStableCandidate_DisplaysReleaseNumberOnly()
    {
        Assert.Equal("0.2.5", Release("v0.2.5", FutureSha).DisplayVersion);
        var dev = new LatestBuildInfo(FutureSha, 3600, 1, "https://example.com/dev.zip",
            1234, null, "2026-10-26");
        Assert.Equal("CI #3600 · 1234567", dev.DisplayVersion);
    }

    [Fact]
    public void StableUpdateOffer_DisplaysOnlyCurrentAndAvailableReleaseNumbers()
    {
        var message = LatestBuildUpdater.FormatUpdateStatus("0.2.4",
            Release("v0.2.5", FutureSha), updateAvailable: true);
        Assert.Equal("Current: 0.2.4 · Available: 0.2.5", message);
        Assert.DoesNotContain("a9e6188", message);
        Assert.DoesNotContain("1234567", message);
    }

    [Fact]
    public void MatchingStableRelease_DisplaysNoAvailableOffer()
    {
        var message = LatestBuildUpdater.FormatUpdateStatus("0.2.4",
            Release("v0.2.4", FrozenSha), updateAvailable: false);
        Assert.Equal("Current: 0.2.4 · Up to date (latest: 0.2.4)", message);
        Assert.DoesNotContain("Available:", message);
    }

    private static LatestBuildInfo Release(string tag, string sha) =>
        new(sha, 0, 0, "https://github.com/vorlocsev/dvinholm/releases/download/" +
            tag + "/Dvinholm-Windows-x86.zip", 1234, null, "2026-10-26", tag);
}
