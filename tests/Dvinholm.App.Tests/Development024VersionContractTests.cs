namespace Dvinholm.App.Tests;

/// <summary>
/// Eight offline version-channel contracts. They do not create tags or publish
/// releases and deliberately distinguish Development binaries from Stable 0.2.3.
/// </summary>
public sealed class Development024VersionContractTests
{
    private static string Read(string path)
    {
        for (DirectoryInfo? dir = new(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var full = Path.Combine(dir.FullName, path.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(full))
                return File.ReadAllText(full).Replace("\r\n", "\n", StringComparison.Ordinal);
        }
        throw new FileNotFoundException("Missing version contract source: " + path);
    }

    [Fact]
    public void MainProjectDefaultsToUnreleased024WithoutDemoTools()
    {
        var project = Read("src/Dvinholm.App/Dvinholm.App.csproj");
        Assert.Contains("<Version>0.2.4</Version>", project);
        Assert.Contains("<DvinholmDevelopmentFeatures Condition=", project);
        Assert.Contains("== ''\">false</DvinholmDevelopmentFeatures>", project);
        Assert.Contains("<Compile Remove=\"CanDemoFrameGenerator.cs\" />", project);
    }

    [Fact]
    public void DevelopmentUsesOnlyFrameworkDependentLiteAndExactCommit()
    {
        var ci = Read(".github/workflows/ci.yml");
        var lite = ci.Split("      - name: Publish application x86\n", StringSplitOptions.None)[1]
            .Split("      - name: Verify branded Windows icon is published\n", StringSplitOptions.None)[0];
        Assert.Contains("-p:Version=0.2.4", lite);
        Assert.Contains("-p:InformationalVersion=0.2.4+${{ github.sha }}.ci${{ github.run_number }}", lite);
        Assert.Contains("--self-contained false", lite);
        Assert.Contains("-p:DvinholmDevelopmentFeatures=true", lite);
        Assert.DoesNotContain("      - name: Publish Portable", ci);
        Assert.Contains("Dvinholm-Windows-x86.zip", ci);
        Assert.DoesNotContain("      - name: Publish Portable", ci);
    }

    [Fact]
    public void OtherDistributionWorkflowsAreManualOnly()
    {
        foreach (var path in new[]
        {
            ".github/workflows/release-candidate-lite-v0.2.4.yml",
            ".github/workflows/release-candidate-v0.2.4.yml",
            ".github/workflows/lite-msix.yml",
            ".github/workflows/stable-staging-v0.2.4.yml"
        })
        {
            var text = Read(path);
            Assert.Contains("on:\n  workflow_dispatch:", text);
            Assert.DoesNotContain("  pull_request:", text);
            Assert.DoesNotContain("  push:", text);
        }
    }

    [Fact]
    public void ApplicationIdentityAndUpdateTransportUse024DevelopmentUserAgent()
    {
        var updater = Read("src/Dvinholm.App/LatestBuildUpdater.cs");
        Assert.Contains("new ProductInfoHeaderValue(\"Dvinholm\", \"0.2.4\")", updater);
        Assert.Contains("CurrentCiRunNumber is int runNumber", updater);
        Assert.Contains("var marker = suffix.LastIndexOf(\".ci\"", updater);
    }

    [Fact]
    public void Published023PackagingStillPinsStableVersionWithoutDemoFeatures()
    {
        var workflow = Read(".github/workflows/release-v0.2.3.yml");
        Assert.Contains("-p:Version=0.2.3", workflow);
        Assert.Contains("-p:DvinholmDevelopmentFeatures=false", workflow);
        Assert.Contains("-p:PublishTrimmed=false", workflow);
        Assert.DoesNotContain("-p:Version=0.2.4", workflow);
        Assert.Contains("APPROVE-DVINHOLM-V0.2.3", workflow);
    }

    [Fact]
    public void PublicReadmeDoesNotMislabel024AsReleasedStable()
    {
        var readme = Read("README.md");
        Assert.Contains("badge/stable-v0.2.3", readme);
        Assert.Contains("Development CI ZIPs identify as **0.2.4** (unreleased", readme);
        Assert.Contains("/releases/download/v0.2.3/Dvinholm-Windows-x86.zip", readme);
        Assert.DoesNotContain("/releases/download/v0.2.4/", readme);
    }

    [Fact]
    public void RussianGuideFirstLaunchLinksActualPublishedStable023Archive()
    {
        var guide = Read("docs/USER_GUIDE_RU.md");
        var start = guide.Split("## 1. Первый запуск", StringSplitOptions.None)[1]
            .Split("## 2.", StringSplitOptions.None)[0];
        Assert.Contains("/releases/download/v0.2.3/Dvinholm-Windows-x86.zip", start);
        Assert.Contains("Development CI-сборки 0.2.4", start);
        Assert.DoesNotContain("стабильный ZIP v0.2.1", start);
    }

    [Fact]
    public void ChangelogAndRoadmapReserve024WithoutClaimingPublication()
    {
        var changelog = Read("CHANGELOG.md");
        var roadmap = Read("ROADMAP.md");
        Assert.Contains("## [0.2.4] — Unreleased (Development only)", changelog);
        Assert.Contains("## [0.2.3] — 2026-10-24", changelog);
        Assert.Contains("## Next Development milestone: 0.2.4 (unreleased)", roadmap);
        Assert.Contains("No 0.2.4 Stable tag/release without explicit approval", roadmap);
    }
}
