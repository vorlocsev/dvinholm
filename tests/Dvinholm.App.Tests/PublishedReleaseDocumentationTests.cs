using System.Text.RegularExpressions;

namespace Dvinholm.App.Tests;

/// <summary>
/// Keep public release docs internally consistent as the Stable version moves.
/// This suite is offline: it never queries GitHub or confuses CI with hardware tests.
/// </summary>
public sealed class PublishedReleaseDocumentationTests
{
    private static string Read(string relative)
    {
        for (DirectoryInfo? d = new(AppContext.BaseDirectory); d is not null; d = d.Parent)
        {
            var path = Path.Combine(d.FullName, relative.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(path))
                return File.ReadAllText(path).Replace("\r\n", "\n", StringComparison.Ordinal);
        }
        throw new FileNotFoundException($"Release documentation not found: {relative}");
    }

    private static string CurrentStableTag()
    {
        var readme = Read("README.md");
        var match = Regex.Match(readme,
            @"badge/stable-(v[0-9]+\.[0-9]+\.[0-9]+)-");
        Assert.True(match.Success, "README must include an explicit versioned Stable badge.");
        return match.Groups[1].Value;
    }

    [Fact]
    public void StableBadgeLinksToTheDisplayedTag()
    {
        var readme = Read("README.md");
        Assert.Contains($"/releases/tag/{CurrentStableTag()}", readme);
    }

    [Fact]
    public void DownloadLinksUseCurrentStableTag()
    {
        var readme = Read("README.md");
        Assert.Contains($"/releases/download/{CurrentStableTag()}/Dvinholm-Windows-x86.zip", readme);
        Assert.Contains($"**Published Stable {CurrentStableTag()}**", readme);
        Assert.Contains($"/releases/download/{CurrentStableTag()}/Dvinholm-Windows-x86-Lite.zip", readme);
    }

    [Fact]
    public void ChecksumSidecarComesFromTheSameRelease()
    {
        var readme = Read("README.md");
        Assert.Contains($"/releases/download/{CurrentStableTag()}/SHA256SUMS.txt", readme);
        // The actual digests belong to the release sidecar, not an
        // unverified/hardcoded hash embedded in the README.
        Assert.Contains("authoritative hashes", readme);
        Assert.Contains("same published release", readme);
    }

    [Fact]
    public void LatestReleaseHeadingMatchesBadge()
    {
        var readme = Read("README.md");
        Assert.Contains($"## Latest stable release — {CurrentStableTag()}", readme);
    }

    [Fact]
    public void ChangelogMarksLatestReleaseAsPublished()
    {
        var changelog = Read("CHANGELOG.md");
        var tag = CurrentStableTag().TrimStart('v');
        Assert.Matches(new Regex(@"(?m)^## \[" + Regex.Escape(tag) + @"\] — 20[0-9]{2}-[0-9]{2}-[0-9]{2} \(Stable published\)$"), changelog);
        Assert.Contains("a9e61883a413d4afaebb86fb468987c904250092", changelog);
        Assert.DoesNotContain($"## [{tag}] — Unreleased", changelog);
    }

    [Fact]
    public void StableDocsDoNotAdvertiseSyntheticDemoControls()
    {
        var readme = Read("README.md");
        Assert.Contains("Demo/preview/test-only controls are **excluded from Stable**.", readme);
        Assert.Contains("Current Development", readme);
    }

    [Fact]
    public void RussianGuideDistinguishesStableFromDevelopment()
    {
        var guide = Read("docs/USER_GUIDE_RU.md");
        Assert.Contains("опубликованной Stable 0.2.4", guide);
        Assert.Contains("до 16 элементов в Stable v0.2.4", guide);
        Assert.Contains("Launcher.exe", guide);
    }

    [Fact]
    public void DistributionGuideAndRoadmapDoNotClaimPendingPublication()
    {
        var dist = Read("docs/WINDOWS-DISTRIBUTIONS.md");
        var roadmap = Read("ROADMAP.md");
        Assert.Contains("Published Stable **v0.2.4**", dist);
        Assert.Contains("Lite (published Stable v0.2.4)", dist);
        Assert.Contains("unsigned MSIX", dist);
        Assert.Contains("public Stable v0.2.4 release is already published", roadmap);
        Assert.Contains("Historical Development 0.2.4 pre-release plan", roadmap);
    }
}
