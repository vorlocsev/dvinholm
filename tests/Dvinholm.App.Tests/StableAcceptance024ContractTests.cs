using Dvinholm.App;

namespace Dvinholm.App.Tests;

/// <summary>
/// Eight deterministic offline gates for the unpublished Stable 0.2.4
/// human-acceptance kit. A separate Windows CI job executes the actual
/// downloaded ZIPs, never opens WinUI or touches installed user data.
/// </summary>
[Trait("Category", "ArchivedWorkflow")]
public sealed class StableAcceptance024ContractTests
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
        throw new FileNotFoundException("Stable acceptance contract missing", path);
    }

    private static string Prepare => Source("eng/Prepare-StableAcceptance024.ps1");
    private static string Workflow => Source(".github/workflows/stable-staging-v0.2.4.yml");

    [Fact]
    public void CommitAndDestinationMustBeExactAndFailClosed()
    {
        var script = Prepare;
        Assert.Contains("^[0-9a-f]{40}$", script);
        Assert.Contains("Stable acceptance destination already exists: refusing overwrite", script);
        Assert.Contains("Stable acceptance destination parent must already exist", script);
        Assert.True(script.IndexOf("if (Test-Path -LiteralPath $target)", StringComparison.Ordinal)
            < script.IndexOf("Test-WindowsDistributionChecksums.ps1", StringComparison.Ordinal));
    }

    [Fact]
    public void RequiresExactlyTheThreeCombinedStagingFiles()
    {
        var script = Prepare;
        Assert.Contains("$entries.Count -ne 3", script);
        Assert.Contains("$expectedNames -cnotcontains $_.Name", script);
        Assert.Contains("Dvinholm-Windows-x86.zip", script);
        Assert.Contains("Dvinholm-Windows-x86-Lite.zip", script);
        Assert.Contains("SHA256SUMS.txt", script);
    }

    [Fact]
    public void StrictTwoLineShaAndOriginalDownloadedHashMustBeUsed()
    {
        var script = Prepare;
        Assert.Contains("$lines.Count -ne 2", script);
        Assert.Contains("^[0-9a-f]{64}  Dvinholm-Windows-x86[.]zip$", script);
        Assert.Contains("^[0-9a-f]{64}  Dvinholm-Windows-x86-Lite[.]zip$", script);
        Assert.Contains("Test-WindowsDistributionChecksums.ps1", script);
        Assert.Contains("Subset ORIGINAL downloaded checksum entries", script);
        Assert.Contains("$lines[0] + [Environment]::NewLine", script);
        Assert.Contains("$lines[1] + [Environment]::NewLine", script);
        Assert.DoesNotContain("-GenerateChecksum", script);
    }

    [Fact]
    public void MustValidateBothRealFlavorsInStableChannelBeforeExtraction()
    {
        var script = Prepare;
        var portable = script.IndexOf("Test-ReleaseCandidate024.ps1", StringComparison.Ordinal);
        var lite = script.IndexOf("Test-ReleaseCandidate024Lite.ps1", StringComparison.Ordinal);
        var extraction = script.IndexOf("[IO.Compression.ZipFile]::ExtractToDirectory(", StringComparison.Ordinal);
        Assert.True(portable > 0 && lite > portable && extraction > lite);
        Assert.Contains("-ExpectedCommit $ExpectedCommit -Channel stable", script);
        Assert.DoesNotContain("-Channel rc", script);
        Assert.Contains("Stable acceptance Portable eight-gate binary audit failed", script);
        Assert.Contains("Stable acceptance Lite eight-gate binary audit failed", script);
    }

    [Fact]
    public void AtomicPrivateExtractionMustNeverMergeWithInstalledDirectory()
    {
        var script = Prepare;
        Assert.Contains("dvinholm-stable024-audit-", script);
        Assert.Contains("dvinholm-stable024-stage-", script);
        Assert.Contains("[IO.Directory]::Move($stage, $target)", script);
        Assert.Contains("destination appeared during extraction: refusing overwrite", script);
        Assert.Contains("Remove-Item -LiteralPath $stage -Recurse -Force", script);
        Assert.Contains("Remove-Item -LiteralPath $audit -Recurse -Force", script);
    }

    [Fact]
    public void StableReceiptKeepsDirectExeRelaunchOutOfInstalledUserData()
    {
        var path = Path.Combine(Path.GetTempPath(),
            "Dvinholm-stable-024-contract-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        try
        {
            File.WriteAllText(
                Path.Combine(path, DvinholmUserDataDirectory.Rc024AcceptanceReceipt),
                "Stable 0.2.4 staged acceptance");
            Assert.Contains("ACCEPTANCE-0.2.4.txt", Prepare);
            Assert.Contains("acceptance-user-data", Prepare);
            Assert.True(DvinholmUserDataDirectory.HasAcceptanceReceiptInDirectory(path));
            var actual = DvinholmUserDataDirectory.Resolve(
                path, Path.Combine(path, "installed-user-data"), null, true);
            Assert.Equal(Path.Combine(path, DvinholmUserDataDirectory.AcceptanceFolder), actual);
        }
        finally
        {
            Directory.Delete(path, recursive: true);
        }
    }

    [Fact]
    public void WorkflowRunsActualDownloadedStableArtifactsAndNoOverwriteRehearsal()
    {
        var yaml = Workflow;
        Assert.Contains("artifacts/stable024-downloaded", yaml);
        Assert.Contains("Prepare-StableAcceptance024.ps1", yaml);
        Assert.Contains("Eight real Stable 0.2.3 to staged 0.2.4", yaml);
        Assert.Contains("ACCEPTANCE-0.2.4.txt", yaml);
        Assert.Contains("Stable acceptance repeated preparation illegally overwrote", yaml);
        Assert.Contains("destination already exists", yaml);
        Assert.Contains("human acceptance preparation passed", yaml);
    }

    [Fact]
    public void WorkflowAndKitHaveNoReleasePermissionsOrGuiExecution()
    {
        var yaml = Workflow;
        var script = Prepare;
        Assert.Contains("contents: read", yaml);
        Assert.DoesNotContain("contents: write", yaml);
        Assert.DoesNotContain("gh release create", yaml);
        Assert.Contains("Dvinholm-v0.2.4-STABLE-WINDOWS-ACCEPTANCE-KIT", yaml);
        Assert.Contains("BUILD_COMMIT_024_STABLE.txt", yaml);
        Assert.Contains("eng/Prepare-StableAcceptance024.ps1", yaml);
        Assert.Contains("NOT PUBLISHED", yaml);
        Assert.DoesNotContain("Start-Process", script);
        Assert.DoesNotContain("Start-Process", yaml);
        Assert.Contains("No GUI", script);
    }
}
