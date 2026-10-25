namespace Dvinholm.App.Tests;

/// <summary>
/// Eight deterministic offline contracts for the one-click Windows 0.2.4 RC
/// acceptance launcher. GitHub Actions additionally executes the real batch
/// in --prepare-only mode against a downloaded RC ZIP on Windows x86.
/// </summary>
public sealed class Rc024OneClickLauncherTests
{
    private static string Read(string relativePath)
    {
        for (DirectoryInfo? directory = new(AppContext.BaseDirectory);
             directory is not null; directory = directory.Parent)
        {
            var fullPath = Path.Combine(directory.FullName,
                relativePath.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(fullPath))
                return File.ReadAllText(fullPath);
        }
        throw new FileNotFoundException("RC acceptance launcher source missing.", relativePath);
    }

    private static string Launcher => Read("START-ACCEPTANCE-024.cmd");
    private static string Workflow => Read(".github/workflows/release-candidate-v0.2.4.yml");

    [Fact]
    public void RequiresExactRunProvenanceFileBeforePreparing()
    {
        var script = Launcher;
        var requireCommit = script.IndexOf("if not exist \"%PROVENANCE%\"", StringComparison.Ordinal);
        var readCommit = script.IndexOf("set /p EXPECTED_COMMIT=", StringComparison.Ordinal);
        var preparation = script.IndexOf("Prepare-RcAcceptance024.ps1", StringComparison.Ordinal);
        Assert.Contains("BUILD_COMMIT_024.txt", script);
        Assert.True(requireCommit >= 0 && readCommit > requireCommit && preparation > readCommit);
        Assert.Contains("if not defined EXPECTED_COMMIT", script);
    }

    [Fact]
    public void RequiresBothDownloadedCandidateFiles()
    {
        var script = Launcher;
        Assert.Contains("Dvinholm-v0.2.4-RC-TEST-ONLY", script);
        Assert.Contains("Dvinholm-Windows-x86.zip", script);
        Assert.Contains("SHA256SUMS.txt", script);
        Assert.Contains("set \"CANDIDATE=%KIT%..\\candidate\"", script);
        Assert.True(script.IndexOf("SHA256SUMS.txt", StringComparison.Ordinal)
            < script.IndexOf("Prepare-RcAcceptance024.ps1", StringComparison.Ordinal));
    }

    [Fact]
    public void ExistingOutputIsRejectedBeforeAnyExtraction()
    {
        var script = Launcher;
        var existence = script.IndexOf("if exist \"%OUTPUT%\"", StringComparison.Ordinal);
        var invoke = script.IndexOf("powershell.exe -NoProfile", StringComparison.Ordinal);
        Assert.Contains("Dvinholm-024-acceptance", script);
        Assert.Contains("Never overwrite", script);
        Assert.True(existence >= 0 && invoke > existence);
    }

    [Fact]
    public void InvokesShaBoundReadOnlyPreparationScript()
    {
        var script = Launcher;
        Assert.Contains("powershell.exe -NoProfile -ExecutionPolicy Bypass", script);
        Assert.Contains("-File \"%KIT%eng\\Prepare-RcAcceptance024.ps1\"", script);
        Assert.Contains("-ArchiveDirectory \"%CANDIDATE%\"", script);
        Assert.Contains("-ExpectedCommit \"%EXPECTED_COMMIT%\"", script);
        Assert.Contains("-DestinationDirectory \"%OUTPUT%\"", script);
        Assert.Contains("if errorlevel 1 (", script);
    }

    [Fact]
    public void RequiresReceiptPrivateDataAndExeBeforeAnyLaunch()
    {
        var script = Launcher;
        var receipt = script.IndexOf("ACCEPTANCE-0.2.4.txt", StringComparison.Ordinal);
        var privateData = script.IndexOf("if not exist \"%OUTPUT%\\acceptance-user-data", StringComparison.Ordinal);
        var app = script.IndexOf("if not exist \"%OUTPUT%\\Dvinholm.App.exe", StringComparison.Ordinal);
        var start = script.IndexOf("start \"\" /D", StringComparison.Ordinal);
        Assert.True(receipt >= 0 && privateData > receipt && app > privateData && start > app);
        Assert.Contains("Refusing startup", script);
    }

    [Fact]
    public void PrepareOnlyReturnsBeforeGuiAndRejectsUnknownArguments()
    {
        var script = Launcher;
        var only = script.LastIndexOf("if /I \"%~1\"==\"--prepare-only\"", StringComparison.Ordinal);
        var start = script.IndexOf("start \"\" /D", StringComparison.Ordinal);
        Assert.True(only >= 0 && only < start);
        Assert.Contains("WinUI was NOT launched", script);
        Assert.Contains("Unknown argument", script);
        Assert.Contains("exit /b 0", script);
    }

    [Fact]
    public void ManualLaunchTargetsOnlyPrivateAcceptedCopy()
    {
        var script = Launcher;
        Assert.Contains("setlocal DisableDelayedExpansion", script);
        Assert.Contains("DVINHOLM_ACCEPTANCE_DATA_DIR=%OUTPUT%\\acceptance-user-data", script);
        Assert.Contains("start \"\" /D \"%OUTPUT%\" \"%OUTPUT%\\Dvinholm.App.exe\"", script);
        Assert.DoesNotContain("start \"\" \"Dvinholm.App.exe\"", script);
        Assert.Contains("Stable v0.2.3 has NOT been changed", script);
    }

    [Fact]
    public void WorkflowDownloadsVerifiesAndDryRunsLauncherWithoutReleasing()
    {
        var workflow = Workflow;
        Assert.Contains("name: Dvinholm 0.2.4 RC (validation only)", workflow);
        Assert.Contains("BUILD_COMMIT_024.txt", workflow);
        Assert.Contains("START-ACCEPTANCE-024.cmd", workflow);
        Assert.Contains("Dvinholm-v0.2.4-WINDOWS-ACCEPTANCE-KIT", workflow);
        Assert.Contains("artifacts/rc-v0.2.4-downloaded", workflow);
        Assert.Contains("START-ACCEPTANCE-024.cmd --prepare-only", workflow);
        Assert.Contains("Second attempt must not overwrite", workflow);
        Assert.Contains("contents: read", workflow);
        Assert.DoesNotContain("gh release create", workflow);
        Assert.DoesNotContain("gh release edit", workflow);
    }
}
