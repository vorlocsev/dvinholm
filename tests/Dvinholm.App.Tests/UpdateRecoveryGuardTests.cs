using System.Diagnostics;
using System.Text;
using Dvinholm.App;

namespace Dvinholm.App.Tests;

/// <summary>
/// Windows-only isolated PowerShell tests for a separate updater crash guard.
/// The real application, J2534 hardware, UI and installed build are never touched.
/// </summary>
public sealed class UpdateRecoveryGuardTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "Dvinholm-update-guard-tests", Guid.NewGuid().ToString("N"));

    private string Source => Path.Combine(_root, "new-staged-build");
    private string Destination => Path.Combine(_root, "installed-app");
    private string Backup => Path.Combine(_root, "old-build-backup");
    private string StateDirectory => Backup + ".state";
    private string ScriptPath => Path.Combine(_root, "updater.ps1");
    private string PreviousMarker => Path.Combine(StateDirectory, "previous.pending");
    private string FirstInstallMarker => Path.Combine(StateDirectory, "first-install.pending");
    private string LocalData => Path.Combine(_root, "LocalData");

    public UpdateRecoveryGuardTests()
    {
        Directory.CreateDirectory(_root);
        Directory.CreateDirectory(Path.Combine(LocalData, "Dvinholm"));
    }

    private string GenerateScript()
    {
        var script = LatestBuildUpdater.BuildReplacementScript(
            Source, Destination, 1234, ScriptPath, Backup);
        File.WriteAllText(ScriptPath, script, new UTF8Encoding(false));
        return script;
    }

    private (int Code, string Output) RunScript(string script, params string[] arguments)
    {
        var powerShell = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            "System32", "WindowsPowerShell", "v1.0", "powershell.exe");
        var processInfo = new ProcessStartInfo
        {
            FileName = powerShell,
            WorkingDirectory = _root,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        processInfo.Environment["LOCALAPPDATA"] = LocalData;
        processInfo.ArgumentList.Add("-NoProfile");
        processInfo.ArgumentList.Add("-NonInteractive");
        processInfo.ArgumentList.Add("-ExecutionPolicy");
        processInfo.ArgumentList.Add("Bypass");
        processInfo.ArgumentList.Add("-File");
        processInfo.ArgumentList.Add(script);
        foreach (var argument in arguments)
            processInfo.ArgumentList.Add(argument);

        using var process = Process.Start(processInfo)
            ?? throw new InvalidOperationException("Could not start isolated PowerShell test process.");
        Assert.True(process.WaitForExit(30000), "PowerShell guard test did not terminate.");
        return (process.ExitCode, process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd());
    }

    private (int Code, string Output) RunGuard()
    {
        GenerateScript();
        // The updater helper is deliberately absent: the watcher must be
        // able to recover after that process has already terminated.
        return RunScript(ScriptPath, "-Watch", "-HelperPid", "2147483647");
    }

    private void CreateCheckpoint(string marker)
    {
        Directory.CreateDirectory(StateDirectory);
        File.WriteAllText(marker, "pending");
    }

    [Fact]
    public void GeneratedPowerShell_HasNoParserErrors()
    {
        GenerateScript();
        var parserPath = Path.Combine(_root, "check-syntax.ps1");
        File.WriteAllText(parserPath,
            "param([string]$Target)\n" +
            "$tokens=$null; $parseErrors=$null\n" +
            "[System.Management.Automation.Language.Parser]::ParseFile($Target,[ref]$tokens,[ref]$parseErrors) | Out-Null\n" +
            "if ($parseErrors.Count -gt 0) { $parseErrors | Out-String | Write-Output; exit 3 }\n" +
            "exit 0\n");
        var run = RunScript(parserPath, "-Target", ScriptPath);
        Assert.True(run.Code == 0, run.Output);
    }

    [Fact]
    public void GuardMustAcknowledgeReadinessBeforeAnyReplacementCheckpoint()
    {
        var script = GenerateScript();
        var start = script.IndexOf("$guardArguments = @(", StringComparison.Ordinal);
        var acknowledge = script.IndexOf(
            "if (-not (Test-Path -LiteralPath $guardReady))", start, StringComparison.Ordinal);
        var createCheckpoint = script.IndexOf(
            "'pending' | Set-Content -LiteralPath $pendingMarker", start, StringComparison.Ordinal);
        var replace = script.IndexOf(
            "$replacementStarted = $true", start, StringComparison.Ordinal);

        Assert.True(start > 0);
        Assert.True(acknowledge > start);
        Assert.True(createCheckpoint > acknowledge);
        Assert.True(replace > createCheckpoint);
        Assert.Contains("if ($guardProcess.HasExited)", script);
        Assert.Contains("Wait-Process -Id $HelperPid", script);
    }

    [Fact]
    public void PreviousBuildMustBeVerifiedBeforeGuardIsArmed()
    {
        var script = GenerateScript();
        var backupVerified = script.IndexOf(
            "if (-not (Test-Path -LiteralPath (Join-Path $backup 'Dvinholm.App.exe')))",
            StringComparison.Ordinal);
        var backupReady = script.IndexOf("$backupReady = $true", StringComparison.Ordinal);
        var guardStart = script.IndexOf("$guardArguments = @(", StringComparison.Ordinal);
        var checkpoint = script.IndexOf(
            "'pending' | Set-Content -LiteralPath $pendingMarker", StringComparison.Ordinal);

        Assert.True(backupVerified > 0);
        Assert.True(backupReady > backupVerified);
        Assert.True(guardStart > backupReady);
        Assert.True(checkpoint > guardStart);
    }

    [Fact]
    public void CheckpointDisarmsOnlyAfterSuccessfulStartupOrVerifiedRollback()
    {
        var script = GenerateScript();
        var commit = script.IndexOf("# Startup succeeded: disarm the guard", StringComparison.Ordinal);
        var removeMarker = script.IndexOf(
            "Remove-Item -LiteralPath $pendingMarker -Force -ErrorAction Stop",
            commit, StringComparison.Ordinal);
        var commitCleanup = script.IndexOf(
            "FinishRecoveryTransaction", removeMarker, StringComparison.Ordinal);
        var rollback = script.IndexOf(
            "# Restore is verified running; the guard must not roll it back again.",
            StringComparison.Ordinal);
        var rollbackDisarm = script.IndexOf(
            "Remove-Item -LiteralPath $existingMarker -Force -ErrorAction Stop",
            rollback, StringComparison.Ordinal);
        var rollbackCleanup = script.IndexOf(
            "FinishRecoveryTransaction", rollbackDisarm, StringComparison.Ordinal);
        var cleanupFunction = script.IndexOf(
            "function FinishRecoveryTransaction", StringComparison.Ordinal);
        var unregisterFirst = script.IndexOf(
            "if (ClearStartupRecovery)", cleanupFunction, StringComparison.Ordinal);
        var deleteBackupAfterUnregister = script.IndexOf(
            "Remove-Item -LiteralPath $backup -Recurse", unregisterFirst,
            StringComparison.Ordinal);

        Assert.True(commit > 0 && removeMarker > commit && commitCleanup > removeMarker);
        Assert.True(rollback > 0 && rollbackDisarm > rollback && rollbackCleanup > rollbackDisarm);
        Assert.True(cleanupFunction > 0 && unregisterFirst > cleanupFunction &&
            deleteBackupAfterUnregister > unregisterFirst);
        Assert.Contains("if ($replacementStarted -and $hadDestination -and $backupReady)", script);
    }

    [Fact]
    public void WatchWithoutCheckpoint_LeavesInstalledFilesUntouched()
    {
        Directory.CreateDirectory(Destination);
        Directory.CreateDirectory(Backup);
        var installedFile = Path.Combine(Destination, "personal.txt");
        File.WriteAllText(installedFile, "keep old install");
        File.WriteAllText(Path.Combine(Backup, "orphan.txt"), "abandoned preflight backup");

        var run = RunGuard();

        Assert.True(run.Code == 0, run.Output);
        Assert.Equal("keep old install", File.ReadAllText(installedFile));
        Assert.False(Directory.Exists(Backup));
        Assert.False(Directory.Exists(StateDirectory));
    }

    [Fact]
    public void WatchInterruptedFirstInstall_RemovesPartialDestination()
    {
        Directory.CreateDirectory(Destination);
        File.WriteAllText(Path.Combine(Destination, "partial-new-build.txt"), "unfinished");
        CreateCheckpoint(FirstInstallMarker);
        var unrelated = Path.Combine(_root, "important-user-data.txt");
        File.WriteAllText(unrelated, "untouched");

        var run = RunGuard();

        Assert.True(run.Code == 0, run.Output);
        Assert.False(Directory.Exists(Destination));
        Assert.False(File.Exists(FirstInstallMarker));
        Assert.Equal("untouched", File.ReadAllText(unrelated));
    }

    [Fact]
    public void WatchPreviousBuildWithoutExecutable_PreservesCheckpointAndExistingFiles()
    {
        Directory.CreateDirectory(Destination);
        Directory.CreateDirectory(Backup);
        File.WriteAllText(Path.Combine(Destination, "partial.txt"), "failed new build");
        File.WriteAllText(Path.Combine(Backup, "old-settings.txt"), "old preserved");
        CreateCheckpoint(PreviousMarker);

        var run = RunGuard();

        Assert.True(run.Code == 0, run.Output);
        Assert.True(File.Exists(PreviousMarker));
        Assert.Equal("failed new build", File.ReadAllText(Path.Combine(Destination, "partial.txt")));
        Assert.Equal("old preserved", File.ReadAllText(Path.Combine(Backup, "old-settings.txt")));
        var recoveryLog = Path.Combine(LocalData, "Dvinholm", "update.log");
        Assert.Contains("checkpoint preserved", File.ReadAllText(recoveryLog),
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void WatchFirstInstallWithApostropheInDestination_RestoresSafeEmptyState()
    {
        var unusualDestination = Path.Combine(_root, "O'Neil's installed app");
        var unusualBackup = Path.Combine(_root, "O'Neil's previous build");
        var unusualState = unusualBackup + ".state";
        Directory.CreateDirectory(unusualDestination);
        Directory.CreateDirectory(unusualState);
        File.WriteAllText(Path.Combine(unusualDestination, "partial.exe"), "unfinished");
        File.WriteAllText(Path.Combine(unusualState, "first-install.pending"), "pending");

        var unusualScript = Path.Combine(_root, "O'Neil's updater.ps1");
        File.WriteAllText(unusualScript, LatestBuildUpdater.BuildReplacementScript(
            Source, unusualDestination, 1234, unusualScript, unusualBackup),
            new UTF8Encoding(false));

        var run = RunScript(unusualScript, "-Watch", "-HelperPid", "2147483647");

        Assert.True(run.Code == 0, run.Output);
        Assert.False(Directory.Exists(unusualDestination));
        Assert.False(Directory.Exists(unusualState));
    }


    // Eight isolated, real PowerShell guard executions. These deliberately
    // exercise the recovery path, not a simulated Windows reboot or real app launch.
    [Theory]
    [InlineData(false, false, false, false)]
    [InlineData(false, true, false, false)]
    [InlineData(false, false, true, false)]
    [InlineData(false, true, true, false)]
    [InlineData(true, false, false, false)]
    [InlineData(true, true, false, false)]
    [InlineData(true, false, true, false)]
    [InlineData(true, true, true, true)]
    public void InterruptedFirstInstallAndPreCheckpointCleanup_PreserveUnrelatedData(
        bool interruptedInstall, bool nestedPartialFiles, bool abandonedBackup, bool unusualPath)
    {
        var install = unusualPath ? Path.Combine(_root, "O'Neil's app") : Destination;
        var backup = unusualPath ? Path.Combine(_root, "O'Neil's backup") : Backup;
        var scriptPath = unusualPath ? Path.Combine(_root, "O'Neil's recovery.ps1") : ScriptPath;
        var state = backup + ".state";
        var marker = Path.Combine(state, "first-install.pending");
        var outside = Path.Combine(_root, "user-data.json");
        File.WriteAllText(outside, "{\"keep\":true}");
        Directory.CreateDirectory(install);
        File.WriteAllText(Path.Combine(install, "existing.txt"), "unchanged before checkpoint");
        if (nestedPartialFiles)
        {
            var nested = Path.Combine(install, "Assets", "nested");
            Directory.CreateDirectory(nested);
            File.WriteAllText(Path.Combine(nested, "partial.dat"), "incomplete bytes");
        }
        if (abandonedBackup)
        {
            Directory.CreateDirectory(backup);
            File.WriteAllText(Path.Combine(backup, "orphan.dat"), "discard if safe");
        }
        if (interruptedInstall)
        {
            Directory.CreateDirectory(state);
            File.WriteAllText(marker, "pending");
        }

        File.WriteAllText(scriptPath, LatestBuildUpdater.BuildReplacementScript(
            Source, install, 1234, scriptPath, backup), new UTF8Encoding(false));
        var run = RunScript(scriptPath, "-Watch", "-HelperPid", "2147483647");

        Assert.True(run.Code == 0, run.Output);
        Assert.Equal("{\"keep\":true}", File.ReadAllText(outside));
        if (interruptedInstall)
            Assert.False(Directory.Exists(install));
        else
        {
            Assert.Equal("unchanged before checkpoint",
                File.ReadAllText(Path.Combine(install, "existing.txt")));
            if (nestedPartialFiles)
                Assert.Equal("incomplete bytes",
                    File.ReadAllText(Path.Combine(install, "Assets", "nested", "partial.dat")));
        }
        Assert.False(File.Exists(marker));
        Assert.False(Directory.Exists(state));
        Assert.False(Directory.Exists(backup));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }
}
