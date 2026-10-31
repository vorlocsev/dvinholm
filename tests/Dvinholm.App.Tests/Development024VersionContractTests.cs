namespace Dvinholm.App.Tests;

/// <summary>
/// Eight offline version-channel contracts. They do not create tags or publish
/// releases and distinguish the frozen published Stable v0.2.4 from later Development.
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
    public void MainDevelopment024KeepsProductionFeaturesOffByDefault()
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
    public void PublicReadmePointsToFrozenStable024AndSeparatesDevelopment()
    {
        var readme = Read("README.md");
        Assert.Contains("badge/stable-v0.2.4", readme);
        Assert.Contains("/releases/download/v0.2.4/Dvinholm-Windows-x86.zip", readme);
        Assert.Contains("/releases/download/v0.2.4/Dvinholm-Windows-x86-Lite.zip", readme);
        Assert.Contains("a9e61883a413d4afaebb86fb468987c904250092", readme);
        Assert.Contains("Launcher.exe", readme);
        Assert.Contains("framework-dependent", readme);
        Assert.DoesNotContain("the published, supported Stable remains **v0.2.3**", readme);
        Assert.DoesNotContain("0.2.4 (unreleased, experimental)", readme);
    }

    [Fact]
    public void RussianGuideFirstLaunchSeparatesStableAndDevelopmentEntrypoints()
    {
        var guide = Read("docs/USER_GUIDE_RU.md");
        var start = guide.Split("## 1. Первый запуск", StringSplitOptions.None)[1]
            .Split("## 2.", StringSplitOptions.None)[0];
        Assert.Contains("/releases/download/v0.2.4/Dvinholm-Windows-x86.zip", start);
        Assert.Contains("/releases/download/v0.2.4/Dvinholm-Windows-x86-Lite.zip", start);
        Assert.Contains("Launcher.exe", start);
        Assert.Contains("Dvinholm.App.exe", start);
        Assert.DoesNotContain("/releases/download/v0.2.3/", start);
    }

    [Fact]
    public void ChangelogAndRoadmapDistinguishPublished024FromHistoricalPlans()
    {
        var changelog = Read("CHANGELOG.md");
        var roadmap = Read("ROADMAP.md");
        Assert.Contains("## [0.2.4] — 2026-10-26 (Stable published)", changelog);
        Assert.Contains("a9e61883a413d4afaebb86fb468987c904250092", changelog);
        Assert.Contains("## [0.2.3] — 2026-10-24", changelog);
        Assert.Contains("public Stable v0.2.4 release is already published", roadmap);
        Assert.Contains("## Historical Development 0.2.4 pre-release plan", roadmap);
        Assert.DoesNotContain("## Next Development milestone: 0.2.4 (unreleased)", roadmap);
    }
}
