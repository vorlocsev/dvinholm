namespace Dvinholm.App.Tests;

/// <summary>
/// Eight offline contracts for the exact-artifact pre-release HOLD report.
/// GitHub Actions additionally runs the report generator on downloaded ZIPs.
/// </summary>
public sealed class StableReadiness024ContractTests
{
    private static string Source(string path)
    {
        for (DirectoryInfo? directory = new(AppContext.BaseDirectory);
             directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName,
                path.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(candidate)) return File.ReadAllText(candidate);
        }
        throw new FileNotFoundException("Stable readiness source contract missing", path);
    }

    private static string Script => Source("eng/Write-StableReadiness024.ps1");
    private static string Workflow => Source(".github/workflows/stable-staging-v0.2.4.yml");

    [Fact]
    public void ExactShaIsRequiredForTheCandidate()
    {
        Assert.Contains("^[0-9a-f]{40}$", Script);
        Assert.Contains("Readiness gate 1/8", Script);
        Assert.Contains("Candidate Git commit", Script);
        Assert.Contains("ExpectedCommit", Script);
    }

    [Fact]
    public void NeverOverwritesExistingReadinessEvidence()
    {
        Assert.Contains("report already exists; refusing overwrite", Script);
        Assert.Contains("report parent directory must exist", Script);
        Assert.Contains("Readiness gate 2/8", Script);
        Assert.DoesNotContain("Remove-Item -LiteralPath $target", Script);
    }

    [Fact]
    public void RequiresExactlyThreeOriginalDownloadedArtifactFiles()
    {
        Assert.Contains("$files.Count -ne 3", Script);
        Assert.Contains("$names -cnotcontains $_.Name", Script);
        Assert.Contains("Dvinholm-Windows-x86.zip", Script);
        Assert.Contains("Dvinholm-Windows-x86-Lite.zip", Script);
        Assert.Contains("SHA256SUMS.txt", Script);
        Assert.Contains("Readiness gate 3/8", Script);
    }

    [Fact]
    public void CombinedManifestMustBeStrictAndCanonical()
    {
        Assert.Contains("Readiness gate 4/8", Script);
        Assert.Contains("([0-9a-f]{64})  Dvinholm-Windows-x86[.]zip", Script);
        Assert.Contains("([0-9a-f]{64})  Dvinholm-Windows-x86-Lite[.]zip", Script);
        Assert.Contains(@"Lite[.]zip\r?\n\z", Script);
        Assert.DoesNotContain("-Generate", Script);
    }

    [Fact]
    public void BothHashesAreCalculatedAndComparedToDownloadedManifest()
    {
        Assert.Contains("Get-FileHash -LiteralPath $portable -Algorithm SHA256", Script);
        Assert.Contains("Get-FileHash -LiteralPath $lite -Algorithm SHA256", Script);
        Assert.Contains("$matched.Groups[1].Value", Script);
        Assert.Contains("$matched.Groups[2].Value", Script);
        Assert.Contains("Readiness gate 5/8", Script);
        Assert.Contains("Readiness gate 6/8", Script);
    }

    [Fact]
    public void VariantsHaveRealByteSizesAndLiteMustBeSmaller()
    {
        Assert.Contains("(Get-Item -LiteralPath $portable).Length", Script);
        Assert.Contains("(Get-Item -LiteralPath $lite).Length", Script);
        Assert.Contains("$lSize -ge $pSize", Script);
        Assert.Contains("Readiness gate 7/8", Script);
        Assert.Contains("Size (bytes)", Script);
    }

    [Fact]
    public void EightManualWindowsChecksAreAlwaysUncheckedAndReportHoldsRelease()
    {
        var script = Script;
        Assert.Contains("$manual.Count -ne 8", script);
        Assert.Contains("Readiness gate 8/8", script);
        Assert.Contains("Release decision: HOLD", script);
        Assert.Contains("NO GO for publication", script);
        Assert.Contains("Physical J2534/vehicle tests remain", script);
        Assert.Contains("Manually test the installed application updater", script);
        Assert.Contains("Obtain explicit user authorization", script);
        Assert.DoesNotContain("gh release create", script);
    }

    [Fact]
    public void WorkflowVerifiesDownloadedBytesBeforeGeneratingAndUploadingReport()
    {
        var workflow = Workflow;
        Assert.Contains("eng/Write-StableReadiness024.ps1", workflow);
        Assert.Contains("Generate fail-closed Stable 0.2.4 pre-release HOLD evidence", workflow);
        Assert.Contains("-ArchiveDirectory 'artifacts/stable024-downloaded'", workflow);
        Assert.Contains("-ExpectedCommit '${{ github.sha }}'", workflow);
        Assert.Contains("-ReportPath 'READINESS-0.2.4.md'", workflow);
        Assert.Contains("Exactly eight human Windows GUI checks must remain unchecked", workflow);
        Assert.Contains("READINESS-0.2.4.md", workflow);
        Assert.Contains("Dvinholm-v0.2.4-STABLE-WINDOWS-ACCEPTANCE-KIT", workflow);
        Assert.True(workflow.IndexOf("One-click Stable 0.2.4 acceptance against downloaded ZIPs", StringComparison.Ordinal)
            < workflow.IndexOf("Generate fail-closed Stable 0.2.4 pre-release HOLD evidence", StringComparison.Ordinal));
        Assert.Contains("contents: read", workflow);
        Assert.DoesNotContain("contents: write", workflow);
        Assert.DoesNotContain("gh release create", workflow);
    }
}
