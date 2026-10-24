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
        Assert.Contains($"Download **Dvinholm {CurrentStableTag()}**", readme);
    }

    [Fact]
    public void ChecksumSidecarComesFromTheSameRelease()
    {
        var readme = Read("README.md");
        Assert.Contains($"/releases/download/{CurrentStableTag()}/SHA256SUMS.txt", readme);
        Assert.Matches(new Regex(@"SHA-256:\*\* \x60[0-9a-f]{64}\x60"), readme);
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
        Assert.Matches(new Regex(@"(?m)^## \[" + Regex.Escape(tag) + @"\] — 20[0-9]{2}-[0-9]{2}-[0-9]{2}$"), changelog);
        Assert.DoesNotContain($"## [{tag}] — Unreleased", changelog);
    }

    [Fact]
    public void StableDocsDoNotAdvertiseSyntheticDemoControls()
    {
        var readme = Read("README.md");
        Assert.Contains("excluded from Stable", readme);
        Assert.Contains("NOT Stable v0.2.3", readme);
    }

    [Fact]
    public void RussianGuideDistinguishesStableFromDevelopment()
    {
        var guide = Read("docs/USER_GUIDE_RU.md");
        Assert.Contains("опубликованной Stable 0.2.3", guide);
        Assert.Contains("до 16 элементов в Stable v0.2.3", guide);
    }

    [Fact]
    public void DistributionGuideAndRoadmapDoNotClaimPendingPublication()
    {
        var dist = Read("docs/WINDOWS-DISTRIBUTIONS.md");
        var roadmap = Read("ROADMAP.md");
        Assert.Contains("published **v0.2.3**", dist);
        Assert.Contains("Lite ZIP and unsigned MSIX artifacts are still experimental", dist);
        Assert.Contains("Stable v0.2.3 published and next steps", roadmap);
        Assert.Contains("actual **installed** 0.2.2 -> published 0.2.3 updater smoke", roadmap);
    }
}
