namespace Dvinholm.App.Tests;

/// <summary>
/// Eight deterministic offline checks for the one-download Windows update-menu
/// acceptance artifact. WinUI is never launched by these tests.
/// </summary>
public sealed class UpdateMenuOneDownloadBundleTests
{
    private static string Read(string relative)
    {
        for (DirectoryInfo? current = new(AppContext.BaseDirectory);
             current is not null; current = current.Parent)
        {
            var filename = Path.Combine(current.FullName, relative.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(filename))
                return File.ReadAllText(filename);
        }
        throw new FileNotFoundException("Acceptance artifact source unavailable", relative);
    }

    private static string Launcher => Read("START-UPDATE-MENU-ACCEPTANCE.cmd");
    private static string Workflow => Read(".github/workflows/stable-staging-v0.2.4.yml");
    private static string Guide => Read("docs/UPDATE_MENU_WINDOWS_ACCEPTANCE.md");

    [Fact]
    public void LauncherHasOnlyLaunchOrPrepareOnlyModes()
    {
        Assert.Contains("if \"%~1\"==\"\" goto launch", Launcher);
        Assert.Contains("if /I \"%~1\"==\"--prepare-only\" if \"%~2\"==\"\" goto prepare", Launcher);
        Assert.Contains("exit /b 2", Launcher);
        Assert.DoesNotContain("%*", Launcher);
    }

    [Fact]
    public void LauncherDelegatesOnlyToExistingReceiptValidatedKitScript()
    {
        Assert.Contains("setlocal DisableDelayedExpansion", Launcher);
        Assert.Contains("call \"%~dp0kit\\START-STABLE-ACCEPTANCE-024.cmd\" --prepare-only", Launcher);
        Assert.Contains("call \"%~dp0kit\\START-STABLE-ACCEPTANCE-024.cmd\"", Launcher);
        Assert.DoesNotContain("powershell -Command", Launcher);
        Assert.DoesNotContain("curl ", Launcher);
    }

    [Fact]
    public void WorkflowCopiesOnlyThreeAlreadyVerifiedCandidateAssets()
    {
        var workflow = Workflow;
        var bundle = BundleSection(workflow);
        Assert.Contains("Dvinholm-Windows-x86.zip", bundle);
        Assert.Contains("Dvinholm-Windows-x86-Lite.zip", bundle);
        Assert.Contains("SHA256SUMS.txt", bundle);
        Assert.Contains("Join-Path 'artifacts/stable024-downloaded' $name", bundle);
        Assert.DoesNotContain("Test-WindowsDistributionChecksums.ps1 -Directory $candidate -Generate", bundle);
    }

    [Fact]
    public void WorkflowCopiesExactCommitReceiptAndVerifierScripts()
    {
        var bundle = BundleSection(Workflow);
        Assert.Contains("Copy-Item -LiteralPath $name -Destination $kit -ErrorAction Stop", bundle);
        Assert.Contains("BUILD_COMMIT_024_STABLE.txt", bundle);
        Assert.Contains("Start-StableAcceptance024.ps1", bundle);
        Assert.Contains("Prepare-StableAcceptance024.ps1", bundle);
        Assert.Contains("Dvinholm.App.csproj", bundle);
    }

    [Fact]
    public void WorkflowRejectsMismatchedReceiptBeforeRunningWrapper()
    {
        var bundle = BundleSection(Workflow);
        Assert.Contains("$rawReceipt.Substring(0, 40) -cne '${{ github.sha }}'", bundle);
        Assert.Contains("$rawReceipt -cnotmatch '\\A[0-9a-f]{40}(\\r?\\n)?\\z'", bundle);
        Assert.True(bundle.IndexOf("Single-download acceptance receipt does not match", StringComparison.Ordinal)
            < bundle.IndexOf("& cmd.exe /d /c", StringComparison.Ordinal));
    }

    [Fact]
    public void WorkflowRunsPreparationOnlyAndDeletesAllExtractedTestFiles()
    {
        var bundle = BundleSection(Workflow);
        Assert.Contains("START-UPDATE-MENU-ACCEPTANCE.cmd --prepare-only", bundle);
        Assert.Contains("Test-WindowsDistributionChecksums.ps1 -Directory $candidate", bundle);
        Assert.Contains("if ($LASTEXITCODE -ne 0)", bundle);
        Assert.Contains("Remove-Item -LiteralPath $destination -Recurse -Force", bundle);
        Assert.DoesNotContain("Start-Process", bundle);
    }

    [Fact]
    public void UploadedBundleIsAnArtifactNotAGitHubRelease()
    {
        var workflow = Workflow;
        var upload = workflow[workflow.IndexOf("- name: Upload update-menu Windows one-download", StringComparison.Ordinal)..];
        Assert.Contains("uses: actions/upload-artifact@v7", upload);
        Assert.Contains("name: Dvinholm-UPDATE-MENU-WINDOWS-ONE-DOWNLOAD", upload);
        Assert.Contains("path: artifacts/update-menu-acceptance-bundle/", upload);
        Assert.DoesNotContain("gh release", upload);
        Assert.DoesNotContain("create-release", upload);
    }

    [Fact]
    public void ReceiptAndGuideAgreeOnSafeVisibleMenuAndEightPendingManualChecks()
    {
        var receipt = Read("eng/Prepare-StableAcceptance024.ps1");
        var launcher = Read("eng/Start-StableAcceptance024.ps1");
        Assert.Contains("Update menu and channel selection remain available", receipt);
        Assert.DoesNotContain("Update channel UI remains disabled", receipt);
        Assert.Contains("Existing installed Stable 0.2.4 and user data are unchanged", receipt);
        Assert.Contains("published Stable v0.2.4 and installed user data unchanged", launcher);
        Assert.Contains("Dvinholm-UPDATE-MENU-WINDOWS-ONE-DOWNLOAD", Guide);
        Assert.Equal(8, System.Text.RegularExpressions.Regex.Matches(Guide, @"(?m)^- \[ \] ").Count);
    }

    private static string BundleSection(string workflow)
    {
        var from = workflow.IndexOf("- name: Assemble and verify single-download", StringComparison.Ordinal);
        var to = workflow.IndexOf("- name: Upload update-menu Windows one-download", from, StringComparison.Ordinal);
        Assert.True(from >= 0 && to > from);
        return workflow[from..to];
    }
}
