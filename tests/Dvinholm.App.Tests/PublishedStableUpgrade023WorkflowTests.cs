namespace Dvinholm.App.Tests;

/// <summary>Eight fail-closed contracts for the public-release offline upgrade audit.</summary>
public sealed class PublishedStableUpgrade023WorkflowTests
{
    private static string Workflow()
    {
        for (DirectoryInfo? dir = new(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var path = Path.Combine(dir.FullName, ".github", "workflows", "postrelease-upgrade-v023.yml");
            if (File.Exists(path)) return File.ReadAllText(path).Replace("\r\n", "\n", StringComparison.Ordinal);
        }
        throw new FileNotFoundException("Published-upgrade workflow missing.");
    }

    [Fact]
    public void RunsOnChangesAndAllowsIndependentManualAudit()
    {
        var source = Workflow();
        Assert.Contains("  push:", source);
        Assert.Contains("branches: [\"main\"]", source);
        Assert.Contains("  pull_request:", source);
        Assert.Contains("  workflow_dispatch:", source);
    }

    [Fact]
    public void EntireWorkflowIsReadOnlyAndDoesNotPublish()
    {
        var source = Workflow();
        Assert.Contains("permissions:\n  contents: read", source);
        Assert.DoesNotContain("contents: write", source);
        Assert.DoesNotContain("gh release create", source);
        Assert.DoesNotContain("gh release edit", source);
    }

    [Fact]
    public void MustDownloadBothActualPublishedReleases()
    {
        var source = Workflow();
        Assert.Contains("gh release download v0.2.2", source);
        Assert.Contains("gh release download v0.2.3", source);
        Assert.Contains("--pattern 'Dvinholm-Windows-x86.zip' --pattern 'SHA256SUMS.txt'", source);
    }

    [Fact]
    public void OnlyOriginalApprovedStableCommitIsAccepted()
    {
        var source = Workflow();
        Assert.Contains("target_commitish -cne '91ecc343608c931eb033523e3db6012a6c5dbd58'", source);
        Assert.Contains("-ExpectedCommit '91ecc343608c931eb033523e3db6012a6c5dbd58'", source);
    }

    [Fact]
    public void VerifiesPublishedStatusAndPreciselyTwoAssets()
    {
        var source = Workflow();
        Assert.Contains("$release.draft -or $release.prerelease", source);
        Assert.Contains("$assets.Count -ne 2", source);
        Assert.Contains("Unexpected published v0.2.3 assets.", source);
    }

    [Fact]
    public void PinsGithubDigestAndDownloadedArchiveIndependently()
    {
        var source = Workflow();
        const string digest = "fae05958bf1ca4ef92f915508b66da5acb85b4c975c6eb84a950cb8da707ed44";
        Assert.True(source.Split(digest, StringSplitOptions.None).Length >= 3);
        Assert.Contains("$zipAsset.size -ne 62245250", source);
        Assert.Contains("Get-FileHash", source);
    }

    [Fact]
    public void ExistingEightGateMirrorRollbackMustRunOnPublicArchives()
    {
        var source = Workflow();
        Assert.Contains("Test-RealStableUpgrade023.ps1", source);
        Assert.Contains("-PreviousArchiveDirectory artifacts/published-v022", source);
        Assert.Contains("-CandidateArchiveDirectory artifacts/published-v023", source);
        Assert.Contains("Eight disposable published v0.2.2 to v0.2.3 mirror and rollback gates", source);
    }

    [Fact]
    public void DoesNotLaunchGuiOrTouchInstalledUserData()
    {
        var source = Workflow();
        Assert.Contains("No installed application, live updater, GUI or real J2534 touched.", source);
        Assert.DoesNotContain("Start-Process", source);
        Assert.DoesNotContain("Dvinholm.App.exe", source);
        Assert.DoesNotContain("dotnet publish", source);
        Assert.DoesNotContain("actions/upload-artifact", source);
    }
}
