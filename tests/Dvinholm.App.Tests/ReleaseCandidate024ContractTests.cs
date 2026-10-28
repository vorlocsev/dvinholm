using System.Text.RegularExpressions;

namespace Dvinholm.App.Tests;

/// <summary>
/// Eight offline source-level fail-closed guards for the validation-only
/// v0.2.4 release candidate path. They never launch WinUI or publish releases.
/// </summary>
[Trait("Category", "ArchivedWorkflow")]
public sealed class ReleaseCandidate024ContractTests
{
    private static string CheckoutFile(string relativePath)
    {
        for (DirectoryInfo? dir = new(AppContext.BaseDirectory);
             dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName,
                relativePath.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(candidate))
                return File.ReadAllText(candidate);
        }
        throw new FileNotFoundException("Cannot find RC contract file", relativePath);
    }

    private static string Validator => CheckoutFile("eng/Test-ReleaseCandidate024.ps1");
    private static string Workflow => CheckoutFile(".github/workflows/release-candidate-v0.2.4.yml");

    [Fact]
    public void VerifierHasAllEightGatesAndRejectsNonExactCommit()
    {
        var script = Validator;
        for (var gate = 1; gate <= 8; gate++)
            Assert.Contains($"RC gate {gate}/8", script);
        Assert.Contains("^[0-9a-f]{40}$", script);
    }

    [Fact]
    public void VerifierRequiresOnlyExactPortableZipAndChecksum()
    {
        var script = Validator;
        Assert.Contains("Dvinholm-Windows-x86.zip", script);
        Assert.Contains("SHA256SUMS.txt", script);
        Assert.Contains("expected $expectedCount flat files", script);
        Assert.Contains("lowercase SHA-256 manifest entry", script);
    }

    [Fact]
    public void VerifierChecksBoundedSizeHashesLayoutAndExtractedX86()
    {
        var script = Validator;
        Assert.Contains("10MB", script);
        Assert.Contains("150MB", script);
        Assert.Contains("SHA256", script);
        Assert.Contains("Test-WindowsDistribution.ps1", script);
        Assert.Contains("Test-WindowsDistributionExtraction.ps1", script);
    }

    [Fact]
    public void VerifierRejectsSyntheticTypesAndKeepsLiveDiagnosticHandlers()
    {
        var script = Validator;
        foreach (var forbidden in new[] {
            "CanDemoFrameGenerator", "UdsOfflineSample",
            "UdsOfflineTextReport", "AddTestProfiles_Click", "SYNTHETIC_DEMO"
        })
            Assert.Contains(forbidden, script);
        foreach (var required in new[] { "ReadVin_Click", "ReadDid_Click", "ReadDtc_Click" })
            Assert.Contains(required, script);
    }

    [Fact]
    public void VerifierRequiresVersion024AndExactCommitBoundProductVersion()
    {
        var script = Validator;
        Assert.Contains("Dvinholm.App 0.2.4", script);
        Assert.Contains("'0.2.4'", script);
        Assert.Contains("0.2.4+$ExpectedCommit.$Channel", script);
        Assert.Contains("NOT PUBLISHED", script);
    }

    [Fact]
    public void WorkflowHasReadOnlyPermissionsAndNoReleasePublishing()
    {
        var workflow = Workflow;
        Assert.Contains("contents: read", workflow);
        Assert.DoesNotContain("contents: write", workflow);
        Assert.DoesNotContain("gh release create", workflow);
        Assert.DoesNotContain("gh release edit", workflow);
        Assert.DoesNotContain("release: published", workflow);
    }

    [Fact]
    public void WorkflowPublishesWindowsX86ProductionWithoutTrimming()
    {
        var workflow = Workflow;
        foreach (var required in new[] {
            "--runtime win-x86", "--self-contained true",
            "-p:DvinholmDevelopmentFeatures=false",
            "-p:PublishTrimmed=false", "-p:PublishAot=false",
            "-p:DebugType=none", "-p:Version=0.2.4",
            "-p:InformationalVersion=0.2.4+"
        })
            Assert.Contains(required, workflow);
    }

    [Fact]
    public void WorkflowVerifiesDownloadedArtifactAndProductionSmoke()
    {
        var workflow = Workflow;
        Assert.Contains("tests/Dvinholm.Stable.Tests/Dvinholm.Stable.Tests.csproj", workflow);
        Assert.Contains("eng/Test-ReleaseCandidate024.ps1", workflow);
        Assert.Contains("-GenerateChecksum", workflow);
        Assert.Contains("actions/upload-artifact@v7", workflow);
        Assert.Contains("actions/download-artifact@v7", workflow);
        Assert.Contains("artifacts/rc-v0.2.4-downloaded", workflow);
        Assert.Contains("NOT PUBLISHED", workflow);
        Assert.True(Regex.Matches(workflow, @"Test-ReleaseCandidate024[.]ps1").Count >= 3);
    }
}
