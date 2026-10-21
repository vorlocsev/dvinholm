using System.Diagnostics;
using System.Text;
using Dvinholm.App;

namespace Dvinholm.App.Tests;

/// <summary>
/// Simulates the Run-at-next-sign-in recovery entry with a new PowerShell
/// process, using isolated local folders. Never modifies the real HKCU Run key
/// or starts a real application/vehicle diagnostic session.
/// </summary>
public sealed class UpdateRebootRecoveryTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "Dvinholm-reboot-recovery-tests", Guid.NewGuid().ToString("N"));

    private string Source => Path.Combine(_root, "new-build");
    private string Destination => Path.Combine(_root, "installed-app");
    private string Backup => Path.Combine(_root, "previous-build");
    private string State => Backup + ".state";
    private string ScriptPath => Path.Combine(_root, "recovery.ps1");
    private string PreviousMarker => Path.Combine(State, "previous.pending");
    private string FirstMarker => Path.Combine(State, "first-install.pending");
    private string Identity => Path.Combine(State, "helper.identity");
    private string LocalData => Path.Combine(_root, "LocalData");

    public UpdateRebootRecoveryTests()
    {
        Directory.CreateDirectory(_root);
        Directory.CreateDirectory(Path.Combine(LocalData, "Dvinholm"));
    }

    private string GenerateScript(bool mockRecoveredAppStartup = false,
        string? destination = null, string? backup = null, string? scriptPath = null)
    {
        var script = LatestBuildUpdater.BuildReplacementScript(
            Source, destination ?? Destination, 1234, scriptPath ?? ScriptPath, backup ?? Backup);
        if (mockRecoveredAppStartup)
        {
            const string realStart =
                "$restoredProcess = Start-Process -FilePath $restoredExe -WorkingDirectory $destination -PassThru";
            Assert.Contains(realStart, script);
            script = script.Replace(realStart,
                "$restoredProcess = [pscustomobject]@{ HasExited = $false }",
                StringComparison.Ordinal).Replace(
                "Start-Sleep -Seconds 5", "Start-Sleep -Milliseconds 1", StringComparison.Ordinal);
        }

        File.WriteAllText(scriptPath ?? ScriptPath, script, new UTF8Encoding(false));
        return script;
    }

    private (int ExitCode, string Output) ExecuteResume(string? path = null)
    {
        var powerShell = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            "System32", "WindowsPowerShell", "v1.0", "powershell.exe");
        var start = new ProcessStartInfo
        {
            FileName = powerShell,
            WorkingDirectory = _root,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        start.Environment["LOCALAPPDATA"] = LocalData;
        foreach (var argument in new[]
                 { "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass",
                   "-File", path ?? ScriptPath, "-RecoverOnLogon" })
            start.ArgumentList.Add(argument);

        using var process = Process.Start(start)
            ?? throw new InvalidOperationException("Windows PowerShell did not start.");
        Assert.True(process.WaitForExit(30000), "Sign-in recovery test timed out.");
        return (process.ExitCode, process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd());
    }

    private void CreateMarker(string marker)
    {
        Directory.CreateDirectory(State);
        File.WriteAllText(marker, "pending");
    }

    [Fact]
    public void DurableScriptAndRunRegistration_PrecedePendingMarkerAndDestructiveMirror()
    {
        var script = GenerateScript();
        var guardAck = script.IndexOf(
            "throw 'Update recovery guard did not acknowledge readiness.'", StringComparison.Ordinal);
        var copied = script.IndexOf(
            "Copy-Item -LiteralPath $scriptPath -Destination $recoveryScript", guardAck,
            StringComparison.Ordinal);
        var registered = script.IndexOf(
            "New-ItemProperty -Path $runKey -Name $runName", copied, StringComparison.Ordinal);
        var checkpoint = script.IndexOf(
            "'pending' | Set-Content -LiteralPath $pendingMarker", registered,
            StringComparison.Ordinal);
        var mirror = script.IndexOf(
            "$replacementStarted = $true", checkpoint, StringComparison.Ordinal);

        Assert.True(guardAck > 0 && copied > guardAck);
        Assert.True(registered > copied && checkpoint > registered && mirror > checkpoint);
        Assert.Contains("HKCU:\Software\Microsoft\Windows\CurrentVersion\Run", script);
        Assert.Contains("$recoveryScript + '\" -RecoverOnLogon'", script);
        Assert.Contains("if ($Watch -or $RecoverOnLogon)", script);
    }

    [Fact]
    public void RecoveryUsesUniqueStartupEntryAndGuardsAbandonedMutex()
    {
        var script = GenerateScript();

        Assert.Contains("'DvinholmRecovery-' + [IO.Path]::GetFileName($backup)", script);
        Assert.Contains("Join-Path $stateDir 'resume.ps1'", script);
        Assert.Contains("Join-Path $stateDir 'helper.identity'", script);
        Assert.Contains("StartTime.ToUniversalTime().Ticks", script);
        Assert.Contains("AbandonedMutexException", script);
        Assert.Contains("FinishRecoveryTransaction", script);
        Assert.Contains("Remove-ItemProperty -Path $runKey -Name $runName", script);
        Assert.Contains("if (ClearStartupRecovery)", script);
    }

    [Fact]
    public void RebootRecovery_InterruptedFirstInstallRemovesOnlyPartialDestination()
    {
        Directory.CreateDirectory(Destination);
        File.WriteAllText(Path.Combine(Destination, "partial.exe"), "incomplete");
        File.WriteAllText(Path.Combine(_root, "unrelated.txt"), "do not delete");
        CreateMarker(FirstMarker);
        GenerateScript();

        var result = ExecuteResume();

        Assert.Equal(0, result.ExitCode);
        Assert.False(Directory.Exists(Destination));
        Assert.False(File.Exists(FirstMarker));
        Assert.Equal("do not delete", File.ReadAllText(Path.Combine(_root, "unrelated.txt")));
    }

    [Fact]
    public void RebootRecovery_WithoutCheckpointDoesNotTouchInstalledApp()
    {
        Directory.CreateDirectory(Destination);
        File.WriteAllText(Path.Combine(Destination, "Dvinholm.App.exe"), "complete install");
        Directory.CreateDirectory(Backup);
        File.WriteAllText(Path.Combine(Backup, "unused.txt"), "old preflight backup");
        Directory.CreateDirectory(State);
        GenerateScript();

        var result = ExecuteResume();

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("complete install", File.ReadAllText(Path.Combine(Destination, "Dvinholm.App.exe")));
        Assert.False(Directory.Exists(Backup));
        Assert.False(Directory.Exists(State));
    }

    [Fact]
    public void RebootRecovery_WithInvalidPreviousBuildKeepsCheckpointAndFiles()
    {
        CreateMarker(PreviousMarker);
        Directory.CreateDirectory(Backup);
        Directory.CreateDirectory(Destination);
        File.WriteAllText(Path.Combine(Backup, "old-data.txt"), "retain");
        File.WriteAllText(Path.Combine(Destination, "partial.exe"), "unusable new build");
        GenerateScript();

        var result = ExecuteResume();

        Assert.Equal(0, result.ExitCode);
        Assert.True(File.Exists(PreviousMarker));
        Assert.Equal("retain", File.ReadAllText(Path.Combine(Backup, "old-data.txt")));
        Assert.Equal("unusable new build", File.ReadAllText(Path.Combine(Destination, "partial.exe")));
        Assert.Contains("checkpoint preserved",
            File.ReadAllText(Path.Combine(LocalData, "Dvinholm", "update.log")),
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RebootRecovery_WithValidPreviousBuildRestoresAndDisarmsCheckpoint()
    {
        CreateMarker(PreviousMarker);
        Directory.CreateDirectory(Backup);
        Directory.CreateDirectory(Destination);
        File.WriteAllText(Path.Combine(Backup, "Dvinholm.App.exe"), "working old executable");
        File.WriteAllText(Path.Combine(Backup, "old.dll"), "previous build library");
        File.WriteAllText(Path.Combine(Destination, "partial.exe"), "broken update");
        GenerateScript(mockRecoveredAppStartup: true);

        var result = ExecuteResume();

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("working old executable",
            File.ReadAllText(Path.Combine(Destination, "Dvinholm.App.exe")));
        Assert.Equal("previous build library", File.ReadAllText(Path.Combine(Destination, "old.dll")));
        Assert.False(File.Exists(Path.Combine(Destination, "partial.exe")));
        Assert.False(File.Exists(PreviousMarker));
        Assert.False(Directory.Exists(Backup));
    }

    [Fact]
    public void RebootRecovery_ActiveOriginalHelperDoesNotRollbackItsFiles()
    {
        CreateMarker(FirstMarker);
        Directory.CreateDirectory(Destination);
        File.WriteAllText(Path.Combine(Destination, "partial.exe"), "helper still working");
        using var helper = Process.GetCurrentProcess();
        var ticks = helper.StartTime.ToUniversalTime().Ticks;
        File.WriteAllText(Identity, $"{helper.Id}|{ticks}");
        GenerateScript();

        var result = ExecuteResume();

        Assert.Equal(0, result.ExitCode);
        Assert.True(File.Exists(FirstMarker));
        Assert.Equal("helper still working", File.ReadAllText(Path.Combine(Destination, "partial.exe")));
    }

    [Fact]
    public void RebootRecovery_QuotedApostrophePathsPreserveUnrelatedFiles()
    {
        var destination = Path.Combine(_root, "O'Neil's installed app");
        var backup = Path.Combine(_root, "O'Neil's backup");
        var scriptPath = Path.Combine(_root, "O'Neil's recovery.ps1");
        var state = backup + ".state";
        Directory.CreateDirectory(destination);
        Directory.CreateDirectory(state);
        File.WriteAllText(Path.Combine(state, "first-install.pending"), "pending");
        File.WriteAllText(Path.Combine(destination, "partial.exe"), "incomplete");
        var untouched = Path.Combine(_root, "O'Neil's notes.txt");
        File.WriteAllText(untouched, "saved notes");
        GenerateScript(destination: destination, backup: backup, scriptPath: scriptPath);

        var result = ExecuteResume(scriptPath);

        Assert.Equal(0, result.ExitCode);
        Assert.False(Directory.Exists(destination));
        Assert.False(Directory.Exists(state));
        Assert.Equal("saved notes", File.ReadAllText(untouched));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }
}
