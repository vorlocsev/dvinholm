namespace Dvinholm.App.Tests;

/// <summary>
/// Eight offline source contracts for the nonpublishing v0.2.4 Lite RC.
/// Real ZIP and simulated-host prerequisite checks run on Windows CI.
/// </summary>
public sealed class ReleaseCandidate024LiteContractTests
{
    private static string CheckoutFile(string relativePath)
    {
        for (DirectoryInfo? directory = new(AppContext.BaseDirectory);
             directory is not null; directory = directory.Parent)
        {
            var full = Path.Combine(directory.FullName,
                relativePath.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(full))
                return File.ReadAllText(full);
        }
        throw new FileNotFoundException("Lite RC contract source missing", relativePath);
    }

    private static string Verifier => CheckoutFile("eng/Test-ReleaseCandidate024Lite.ps1");
    private static string Workflow => CheckoutFile(".github/workflows/release-candidate-lite-v0.2.4.yml");

    [Fact]
    public void RejectsInvalidCommitAndChecksAllEightGates()
    {
        var content = Verifier;
        for (var gate = 1; gate <= 8; gate++)
            Assert.Contains($"RC gate {gate}/8", content);
        Assert.Contains("^[0-9a-f]{40}$", content);
        Assert.Contains("NOT PUBLISHED", content);
    }

    [Fact]
    public void EnforcesOnlyLiteZipAndStrictSha256Sidecar()
    {
        var content = Verifier;
        Assert.Contains("Dvinholm-Windows-x86-Lite.zip", content);
        Assert.Contains("SHA256SUMS.txt", content);
        Assert.Contains("expected $expectedCount flat files", content);
        Assert.Contains(@"Dvinholm-Windows-x86-Lite[.]zip\r?\n\z", content);
        Assert.Contains("one lowercase SHA-256 manifest entry", content);
        Assert.Contains("$match.Groups[1].Value -cne $digest", content);
    }

    [Fact]
    public void EnforcesLiteSizeAndExcludesEmbeddedDotnetAndDebug()
    {
        var content = Verifier;
        Assert.Contains("$length -lt 1MB -or $length -ge 30MB", content);
        Assert.Contains("Test-WindowsDistribution.ps1", content);
        Assert.Contains("-Flavor Lite -ZipPath $zipPath", content);
        Assert.Contains("no debug tools", content);
        Assert.DoesNotContain("-Flavor Portable", content);
    }

    [Fact]
    public void RequiresExactBundledPrerequisiteCheckersAndSimulatedX86Extraction()
    {
        var content = Verifier;
        Assert.Contains("Test-LiteBundledPreflight.ps1", content);
        Assert.Contains("Test-WindowsDistributionExtraction.ps1", content);
        Assert.Contains("-SimulatedLiteHost", content);
        Assert.Contains("SIMULATED host", content);
        Assert.Contains("NOT an actual Windows readiness", content);
    }

    [Fact]
    public void ChecksNoSyntheticCodeButRetainsReadOnlyDiagnosticActions()
    {
        var content = Verifier;
        foreach (var forbidden in new[]
        {
            "CanDemoFrameGenerator", "UdsOfflineSample",
            "UdsOfflineTextReport", "AddTestProfiles_Click",
            "PreviewUdsSample_Click", "SYNTHETIC_DEMO"
        })
            Assert.Contains(forbidden, content);
        foreach (var required in new[] { "ReadVin_Click", "ReadDid_Click", "ReadDtc_Click" })
            Assert.Contains(required, content);
    }

    [Fact]
    public void Version024AndProductVersionRequireExactCommitAndRcChannel()
    {
        var content = Verifier;
        Assert.Contains("'0.2.4'", content);
        Assert.Contains("0.2.4+$ExpectedCommit.$Channel", content);
        Assert.Contains("Diagnostics.FileVersionInfo", content);
        Assert.Contains("AssemblyName", content);
    }

    [Fact]
    public void WorkflowIsNonpublishingAndRunsProductionOnlyX86()
    {
        var content = Workflow;
        Assert.Contains("contents: read", content);
        Assert.DoesNotContain("contents: write", content);
        Assert.DoesNotContain("gh release create", content);
        Assert.DoesNotContain("gh release edit", content);
        Assert.Contains("tests/Dvinholm.Stable.Tests/Dvinholm.Stable.Tests.csproj", content);
        Assert.Contains("-p:DvinholmDevelopmentFeatures=false", content);
        Assert.Contains("--runtime win-x86", content);
        Assert.Contains("--self-contained false", content);
        Assert.Contains("-p:WindowsAppSDKSelfContained=false", content);
        Assert.Contains("-p:PublishTrimmed=false", content);
        Assert.Contains("-p:PublishAot=false", content);
        Assert.Contains("-p:DebugType=none", content);
        Assert.Contains("-p:Version=0.2.4", content);
    }

    [Fact]
    public void WorkflowRevalidatesDownloadedArtifactWithoutRegeneratingChecksum()
    {
        var content = Workflow;
        Assert.Contains("eng/Test-ReleaseCandidate024Lite.ps1", content);
        Assert.Contains("-GenerateChecksum", content);
        Assert.Contains("actions/upload-artifact@v7", content);
        Assert.Contains("actions/download-artifact@v7", content);
        Assert.Contains("Dvinholm-v0.2.4-LITE-RC-TEST-ONLY", content);
        Assert.Contains("lite-rc-v0.2.4-downloaded", content);
        Assert.Contains("Reverify downloaded Lite bytes and embedded version", content);
        Assert.Contains("NOT PUBLISHED", content);
        Assert.True(content.Split("Test-ReleaseCandidate024Lite.ps1").Length >= 4);
    }
}
