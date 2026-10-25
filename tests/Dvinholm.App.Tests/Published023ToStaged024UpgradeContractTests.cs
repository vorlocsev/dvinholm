namespace Dvinholm.App.Tests;

/// <summary>
/// Eight offline contracts for the read-only published Stable v0.2.3 to
/// staged Stable v0.2.4 upgrade rehearsal. Real archive gates execute on
/// Windows CI, never on a user installation or against J2534 hardware.
/// </summary>
public sealed class Published023ToStaged024UpgradeContractTests
{
    private static string Source(string path)
    {
        for (DirectoryInfo? root = new(AppContext.BaseDirectory);
             root is not null; root = root.Parent)
        {
            var file = Path.Combine(root.FullName, path.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(file)) return File.ReadAllText(file);
        }
        throw new FileNotFoundException("Missing stable upgrade source contract", path);
    }

    private static string Script => Source("eng/Test-RealStableUpgrade024.ps1");
    private static string Workflow => Source(".github/workflows/stable-staging-v0.2.4.yml");

    [Fact]
    public void RefusesWrongShaAndUntrustedArtifactFileSets()
    {
        var script = Script;
        Assert.Contains("^[0-9a-f]{40}$", script);
        Assert.Contains("Assert-ExactFiles $old", script);
        Assert.Contains("Assert-ExactFiles $new", script);
        Assert.Contains("old and candidate directories must differ", script);
        Assert.Contains("Dvinholm-Windows-x86-Lite.zip", script);
    }

    [Fact]
    public void RequiresActualPublished023ShaAndBinaryVersion()
    {
        var script = Script;
        const string sha = "91ecc343608c931eb033523e3db6012a6c5dbd58";
        Assert.Contains(sha, script);
        Assert.Contains("Test-ReleaseCandidate023.ps1", script);
        Assert.Contains("-Channel stable", script);
        Assert.Contains("v0.2.3 Portable", script);
    }

    [Fact]
    public void UsesStrictOriginalTwoEntryManifestForBothNewZips()
    {
        var script = Script;
        Assert.Contains("lines.Count -ne 2", script);
        Assert.Contains("^[0-9a-f]{64}  Dvinholm-Windows-x86[.]zip$", script);
        Assert.Contains("^[0-9a-f]{64}  Dvinholm-Windows-x86-Lite[.]zip$", script);
        Assert.Contains("Test-WindowsDistributionChecksums.ps1", script);
        Assert.Contains("Subset only the ORIGINAL first line", script);
        Assert.DoesNotContain("-GenerateChecksum", script);
    }

    [Fact]
    public void CandidateMustBeProduction024AndExactStableSha()
    {
        var script = Script;
        Assert.Contains("Test-ReleaseCandidate024.ps1", script);
        Assert.Contains("-ExpectedCommit $ExpectedCommit -Channel stable", script);
        Assert.Contains("before.Version.ToString(3) -cne '0.2.3'", script);
        Assert.Contains("after.Version.ToString(3) -cne '0.2.4'", script);
    }

    [Fact]
    public void BackupAndRollbackUseExactByteSnapshotsAndHandleRobocopyCodes()
    {
        var script = Script;
        Assert.Contains("robocopy $from $to /MIR /R:0 /W:0", script);
        Assert.Contains("$result -ge 8", script);
        Assert.Contains("$global:LASTEXITCODE = 0", script);
        Assert.Contains("Snapshot $backup", script);
        Assert.Contains("Snapshot $installed", script);
        Assert.Contains("Mirror $backup $installed", script);
        Assert.Contains("Rollback", script);
    }

    [Fact]
    public void UpgradeRemovesObsoletePayloadAndPreservesIndependentUserData()
    {
        var script = Script;
        Assert.Contains("obsolete-from-v023.dll", script);
        Assert.Contains("independent-user-settings", script);
        Assert.Contains("ecu-profiles.json", script);
        Assert.Contains("can-baud-rate.txt", script);
        Assert.Contains("selected-j2534-dll.txt", script);
        Assert.Contains("Assert-Same (Snapshot $userdata) $userBaseline 'Forward user data'", script);
        Assert.Contains("Assert-Same (Snapshot $userdata) $userBaseline 'Rollback user data'", script);
    }

    [Fact]
    public void WorkflowDownloadsAndValidatesOriginalPublishedReleaseMetadata()
    {
        var yaml = Workflow;
        Assert.Contains("gh api", yaml);
        Assert.Contains("releases/tags/v0.2.3", yaml);
        Assert.Contains("91ecc343608c931eb033523e3db6012a6c5dbd58", yaml);
        Assert.Contains("$release.draft -or $release.prerelease", yaml);
        Assert.Contains("gh release download v0.2.3", yaml);
        Assert.Contains("artifacts/published-stable-v023", yaml);
        Assert.Contains("artifacts/stable024-downloaded", yaml);
        Assert.Contains("Test-RealStableUpgrade024.ps1", yaml);
    }

    [Fact]
    public void WorkflowRemainsReadOnlyAndMakesNoGuiOrReleaseClaims()
    {
        var yaml = Workflow;
        var script = Script;
        Assert.Contains("contents: read", yaml);
        Assert.DoesNotContain("contents: write", yaml);
        Assert.DoesNotContain("gh release create", yaml);
        Assert.DoesNotContain("gh release edit", yaml);
        Assert.DoesNotContain("Start-Process", script);
        Assert.DoesNotContain("Dvinholm.App.exe", script);
        Assert.Contains("NOT A GUI UPDATER TEST", yaml);
        Assert.Contains("NOT PUBLISHED", yaml);
        Assert.Contains("No installed apps", script);
    }
}
