namespace Dvinholm.App.Tests;

/// <summary>
/// Eight offline source contracts for a validation-only Stable 0.2.4 staging
/// workflow that builds both ZIP flavors from one SHA without publishing.
/// No GUI, installed updater, external runtime or real J2534 adapter required.
/// </summary>
public sealed class StableStaging024ContractTests
{
    private static string Read(string relativePath)
    {
        for (DirectoryInfo? directory = new(AppContext.BaseDirectory);
             directory is not null; directory = directory.Parent)
        {
            var full = Path.Combine(directory.FullName,
                relativePath.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(full))
                return File.ReadAllText(full);
        }
        throw new FileNotFoundException("Stable 0.2.4 staging source not found", relativePath);
    }

    private static string Workflow => Read(".github/workflows/stable-staging-v0.2.4.yml");

    [Fact]
    public void StagingCannotPublishTagReleaseOrWriteContents()
    {
        var yaml = Workflow;
        Assert.Contains("contents: read", yaml);
        Assert.DoesNotContain("contents: write", yaml);
        Assert.DoesNotContain("gh release create", yaml);
        Assert.DoesNotContain("gh release edit", yaml);
        Assert.DoesNotContain("git tag ", yaml);
        Assert.DoesNotContain("release: published", yaml);
        Assert.Contains("STAGED-NOT-PUBLISHED", yaml);
    }

    [Fact]
    public void BothVariantsBuildProductionOnlyWithoutTrimmingOrDebug()
    {
        var yaml = Workflow;
        Assert.Contains("DvinholmDevelopmentFeatures=false", yaml);
        Assert.Contains("PublishTrimmed=false", yaml);
        Assert.Contains("PublishAot=false", yaml);
        Assert.Contains("DebugType=none", yaml);
        Assert.Contains("DebugSymbols=false", yaml);
        Assert.Contains("-p:Version=0.2.4", yaml);
        Assert.Contains("tests/Dvinholm.Stable.Tests/Dvinholm.Stable.Tests.csproj", yaml);
        Assert.True(yaml.Split("DvinholmDevelopmentFeatures=false").Length >= 3);
    }

    [Fact]
    public void PortableAndLiteHaveExplicitlyDifferentRuntimeModes()
    {
        var yaml = Workflow;
        Assert.Contains("--runtime win-x86 --self-contained true", yaml);
        Assert.Contains("-p:WindowsAppSDKSelfContained=true", yaml);
        Assert.Contains("--runtime win-x86 --self-contained false", yaml);
        Assert.Contains("-p:WindowsAppSDKSelfContained=false", yaml);
        Assert.Contains("Publish production-only Portable and Lite from same exact commit", yaml);
        Assert.Contains("Lite must be smaller than Portable", yaml);
    }

    [Fact]
    public void ExactShaAndStableChannelArePinnedOnBothVariantChecks()
    {
        var yaml = Workflow;
        Assert.Contains("0.2.4+${{ github.sha }}.stable", yaml);
        Assert.Contains("Test-ReleaseCandidate024.ps1", yaml);
        Assert.Contains("Test-ReleaseCandidate024Lite.ps1", yaml);
        Assert.Contains("-ExpectedCommit '${{ github.sha }}' -Channel stable", yaml);
        Assert.Contains("-GenerateChecksum", yaml);
        Assert.True(yaml.Split("-Channel stable").Length >= 5);
    }

    [Fact]
    public void CombinedManifestMustHaveTwoCanonicalLowercaseEntries()
    {
        var yaml = Workflow;
        Assert.Contains("Test-WindowsDistributionChecksums.ps1", yaml);
        Assert.Contains("SHA256SUMS.txt", yaml);
        Assert.Contains("lines.Count -ne 2", yaml);
        Assert.Contains("^[0-9a-f]{64}  Dvinholm-Windows-x86[.]zip$", yaml);
        Assert.Contains("^[0-9a-f]{64}  Dvinholm-Windows-x86-Lite[.]zip$", yaml);
        Assert.Contains("$singlePortable", yaml);
        Assert.Contains("$singleLite", yaml);
        Assert.Contains("-cne $singlePortable", yaml);
        Assert.Contains("-cne $singleLite", yaml);
    }

    [Fact]
    public void StagingArtifactContainsExactlyTwoZipFilesAndOneManifest()
    {
        var yaml = Workflow;
        Assert.Contains("Dvinholm-v0.2.4-STAGED-NOT-PUBLISHED", yaml);
        Assert.Contains("actions/upload-artifact@v7", yaml);
        Assert.Contains("Dvinholm-Windows-x86.zip", yaml);
        Assert.Contains("Dvinholm-Windows-x86-Lite.zip", yaml);
        Assert.Contains("artifacts/stable024-combined/SHA256SUMS.txt", yaml);
        Assert.Contains("files.Count -ne 3", yaml);
        Assert.Contains("$expectedFiles -cnotcontains $_.Name", yaml);
    }

    [Fact]
    public void DownloadedZipBytesMustBeReverifiedNotReblessed()
    {
        var yaml = Workflow;
        Assert.Contains("actions/download-artifact@v7", yaml);
        Assert.Contains("artifacts/stable024-downloaded", yaml);
        Assert.Contains("Reverify downloaded pair without regenerating any ZIP hash", yaml);
        Assert.Contains("Subset the ORIGINAL downloaded checksum lines", yaml);
        Assert.Contains("$lines[0] + [Environment]::NewLine", yaml);
        Assert.Contains("$lines[1] + [Environment]::NewLine", yaml);
        var downloadStep = yaml.IndexOf("Reverify downloaded pair without regenerating any ZIP hash",
            StringComparison.Ordinal);
        Assert.True(downloadStep >= 0);
        Assert.DoesNotContain("-Generate", yaml[downloadStep..]);
    }

    [Fact]
    public void HumanGuiHardwareAndReleaseAreNotClaimedByPassingStaging()
    {
        var yaml = Workflow;
        Assert.Contains("No signing, installation", yaml);
        Assert.Contains("Published Stable v0.2.3 remains unchanged", yaml);
        Assert.Contains("GUI and physical J2534 untested", yaml);
        Assert.Contains("NOT PUBLISHED", yaml);
        Assert.DoesNotContain("Start-Process Dvinholm.App.exe", yaml);
        Assert.DoesNotContain("gh release create", yaml);
    }
}
