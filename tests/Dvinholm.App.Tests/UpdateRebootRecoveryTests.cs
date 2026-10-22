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
        Assert.Contains(@"HKCU:\Software\Microsoft\Windows\CurrentVersion\Run", script);
        Assert.Contains("$quote + $recoveryScript + $quote + ' -RecoverOnLogon'", script);
        Assert.Contains("if ($Watch -or $RecoverOnLogon)", script);
    }

    [Fact]
    public void RecoveryUsesUniqueStartupEntryAndGuardsAbandonedMutex()
    {
        var script = GenerateScript();

        var recoveryId = LatestBuildUpdater.RecoveryTransactionId(Backup);
        Assert.Contains($"$runName = 'DvinholmRecovery-{recoveryId}'", script);
        Assert.Contains($"$mutexName = 'Local\\DvinholmRecovery-{recoveryId}'", script);
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


    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, false, true)]
    [InlineData(false, true, false)]
    [InlineData(false, true, true)]
    [InlineData(true, false, false)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    [InlineData(true, true, true)]
    public void RebootRecovery_MirrorsPreviousBuildExactlyAndRemovesObsoleteFiles(
        bool nestedFiles, bool binaryBytes, bool obsoleteDirectory)
    {
        // Real Windows Robocopy /MIR, real PowerShell recovery mode and a
        // mock of ONLY the executable launch. Never use an installed app.
        CreateMarker(PreviousMarker);
        Directory.CreateDirectory(Backup);
        Directory.CreateDirectory(Destination);
        var oldExecutable = Path.Combine(Backup, "Dvinholm.App.exe");
        File.WriteAllBytes(oldExecutable, binaryBytes
            ? new byte[] { 0, 1, 255, 2, 254, 3 }
            : System.Text.Encoding.UTF8.GetBytes("previous executable"));
        File.WriteAllText(Path.Combine(Backup, "settings.json"), "{\"baud\":125000}");
        if (nestedFiles)
        {
            var nested = Path.Combine(Backup, "Assets", "Nested");
            Directory.CreateDirectory(nested);
            File.WriteAllBytes(Path.Combine(nested, "blob.bin"), new byte[] { 0, 255, 17, 33 });
        }
        File.WriteAllText(Path.Combine(Destination, "Dvinholm.App.exe"), "partial replacement");
        File.WriteAllText(Path.Combine(Destination, "obsolete.dll"), "must disappear");
        if (obsoleteDirectory)
        {
            var obsolete = Path.Combine(Destination, "OldBuild", "Unused");
            Directory.CreateDirectory(obsolete);
            File.WriteAllText(Path.Combine(obsolete, "leftover.txt"), "must disappear");
        }
        var outside = Path.Combine(_root, "user-file.txt");
        File.WriteAllText(outside, "outside install");
        static Dictionary<string, byte[]> Snapshot(string directory) =>
            Directory.GetFiles(directory, "*", SearchOption.AllDirectories)
                .ToDictionary(file => Path.GetRelativePath(directory, file),
                    File.ReadAllBytes, StringComparer.OrdinalIgnoreCase);
        var expected = Snapshot(Backup);
        GenerateScript(mockRecoveredAppStartup: true);

        var result = ExecuteResume();

        Assert.Equal(0, result.ExitCode);
        var actual = Snapshot(Destination);
        Assert.Equal(expected.Keys.OrderBy(key => key, StringComparer.OrdinalIgnoreCase),
            actual.Keys.OrderBy(key => key, StringComparer.OrdinalIgnoreCase));
        foreach (var (relativePath, expectedBytes) in expected)
            Assert.Equal(expectedBytes, actual[relativePath]);
        Assert.Equal("outside install", File.ReadAllText(outside));
        Assert.False(File.Exists(PreviousMarker));
        Assert.False(Directory.Exists(State));
        Assert.False(Directory.Exists(Backup));
    }


    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, false, true)]
    [InlineData(false, true, false)]
    [InlineData(false, true, true)]
    [InlineData(true, false, false)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    [InlineData(true, true, true)]
    public void RebootRecovery_InvalidBackupCanBeRepairedAndRetriedWithoutDataLoss(
        bool nestedAssets, bool sameMetadata, bool obsoleteDirectory)
    {
        // Phase 1: a pending checkpoint and an unusable backup must survive
        // an attempted sign-in recovery, without modifying the installed app.
        CreateMarker(PreviousMarker);
        Directory.CreateDirectory(Backup);
        Directory.CreateDirectory(Destination);
        var destinationExe = Path.Combine(Destination, "Dvinholm.App.exe");
        File.WriteAllText(destinationExe, "corrupt-old-build");
        File.WriteAllText(Path.Combine(Destination, "obsolete.dll"), "bad update");
        File.WriteAllText(Path.Combine(Backup, "settings.json"), "{\"rate\":125000}");
        if (obsoleteDirectory)
        {
            var extra = Path.Combine(Destination, "Obsolete", "Nested");
            Directory.CreateDirectory(extra);
            File.WriteAllText(Path.Combine(extra, "broken.dat"), "partial");
        }
        if (nestedAssets)
        {
            var assetDir = Path.Combine(Backup, "Assets", "Nested");
            Directory.CreateDirectory(assetDir);
            File.WriteAllBytes(Path.Combine(assetDir, "binary.dat"), new byte[] { 0, 255, 7, 200 });
        }
        var externalFile = Path.Combine(_root, "untouched-user-file.txt");
        File.WriteAllText(externalFile, "user data survives both attempts");
        GenerateScript(mockRecoveredAppStartup: true);

        var initialRecovery = ExecuteResume();

        Assert.Equal(0, initialRecovery.ExitCode);
        Assert.True(File.Exists(PreviousMarker));
        Assert.True(Directory.Exists(Backup));
        Assert.Equal("corrupt-old-build", File.ReadAllText(destinationExe));
        Assert.True(File.Exists(Path.Combine(Destination, "obsolete.dll")));
        Assert.Contains("checkpoint preserved",
            File.ReadAllText(Path.Combine(LocalData, "Dvinholm", "update.log")),
            StringComparison.OrdinalIgnoreCase);

        // Phase 2: repair only the backup and run the same durable script.
        // Robocopy, manifest validation and SHA-256 comparisons run for real.
        var backupExe = Path.Combine(Backup, "Dvinholm.App.exe");
        File.WriteAllText(backupExe, "trusted-old-build");
        if (sameMetadata)
            File.SetLastWriteTimeUtc(destinationExe, File.GetLastWriteTimeUtc(backupExe));
        static Dictionary<string, byte[]> Snapshot(string folder) =>
            Directory.GetFiles(folder, "*", SearchOption.AllDirectories)
                .ToDictionary(path => Path.GetRelativePath(folder, path),
                    File.ReadAllBytes, StringComparer.OrdinalIgnoreCase);
        var expected = Snapshot(Backup);

        var retriedRecovery = ExecuteResume();

        Assert.Equal(0, retriedRecovery.ExitCode);
        var actual = Snapshot(Destination);
        Assert.Equal(expected.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase),
            actual.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase));
        foreach (var (path, bytes) in expected)
            Assert.Equal(bytes, actual[path]);
        Assert.False(File.Exists(Path.Combine(Destination, "obsolete.dll")));
        Assert.False(File.Exists(PreviousMarker));
        Assert.False(Directory.Exists(Backup));
        Assert.False(Directory.Exists(State));
        Assert.Equal("user data survives both attempts", File.ReadAllText(externalFile));

        // Phase 3: repeated sign-in after a committed recovery is idempotent.
        var afterCommit = Snapshot(Destination);
        var redundantRecovery = ExecuteResume();
        Assert.Equal(0, redundantRecovery.ExitCode);
        var afterRepeat = Snapshot(Destination);
        Assert.Equal(afterCommit.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase),
            afterRepeat.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase));
        foreach (var (path, bytes) in afterCommit)
            Assert.Equal(bytes, afterRepeat[path]);
        Assert.False(Directory.Exists(State));
        Assert.Equal("user data survives both attempts", File.ReadAllText(externalFile));
    }


    [Theory]
    [InlineData(false, "garbage")]
    [InlineData(false, "empty")]
    [InlineData(false, "binary")]
    [InlineData(false, "both")]
    [InlineData(true, "garbage")]
    [InlineData(true, "empty")]
    [InlineData(true, "binary")]
    [InlineData(true, "both")]
    public void RebootRecovery_RejectsCorruptOrAmbiguousCheckpointUntilRepaired(
        bool firstInstall, string damage)
    {
        // Run the production -RecoverOnLogon script, Robocopy and checksum
        // checks in disposable directories. Only old-app launch is mocked.
        var activeMarker = firstInstall ? FirstMarker : PreviousMarker;
        var otherMarker = firstInstall ? PreviousMarker : FirstMarker;
        CreateMarker(activeMarker);
        Directory.CreateDirectory(Backup);
        Directory.CreateDirectory(Destination);
        var partial = Path.Combine(Destination, "partial.dll");
        File.WriteAllBytes(partial, new byte[] { 0, 255, 37, 1 });
        var unrelated = Path.Combine(_root, "notes.txt");
        File.WriteAllText(unrelated, "must survive invalid checkpoint");
        File.WriteAllText(Path.Combine(Backup, "Dvinholm.App.exe"), "trusted old app");
        File.WriteAllBytes(Path.Combine(Backup, "settings.bin"), new byte[] { 1, 0, 255 });

        switch (damage)
        {
            case "garbage":
                File.WriteAllText(activeMarker, "ready-but-not-committed");
                break;
            case "empty":
                File.WriteAllText(activeMarker, string.Empty);
                break;
            case "binary":
                File.WriteAllBytes(activeMarker, new byte[] { 0, 255, 1 });
                break;
            case "both":
                CreateMarker(otherMarker);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(damage));
        }

        var originalMarker = File.ReadAllBytes(activeMarker);
        var originalPartial = File.ReadAllBytes(partial);
        var originalExe = File.ReadAllBytes(Path.Combine(Backup, "Dvinholm.App.exe"));
        var originalSettings = File.ReadAllBytes(Path.Combine(Backup, "settings.bin"));
        GenerateScript(mockRecoveredAppStartup: true);

        var rejected = ExecuteResume();

        // A bad control file must never trigger replacement, first-install
        // deletion, checkpoint disarming, or backup deletion.
        Assert.Equal(0, rejected.ExitCode);
        Assert.True(File.Exists(activeMarker));
        Assert.Equal(originalMarker, File.ReadAllBytes(activeMarker));
        if (damage == "both")
            Assert.True(File.Exists(otherMarker));
        Assert.Equal(originalPartial, File.ReadAllBytes(partial));
        Assert.Equal(originalExe, File.ReadAllBytes(Path.Combine(Backup, "Dvinholm.App.exe")));
        Assert.Equal(originalSettings, File.ReadAllBytes(Path.Combine(Backup, "settings.bin")));
        Assert.Equal("must survive invalid checkpoint", File.ReadAllText(unrelated));
        Assert.Contains("checkpoint preserved",
            File.ReadAllText(Path.Combine(LocalData, "Dvinholm", "update.log")),
            StringComparison.OrdinalIgnoreCase);

        // Once the checkpoint is manually repaired, the same durable script
        // must recover normally without requiring a new installation.
        if (damage == "both")
            File.Delete(otherMarker);
        else
            File.WriteAllText(activeMarker, "pending");
        var recovered = ExecuteResume();

        Assert.Equal(0, recovered.ExitCode);
        Assert.False(File.Exists(activeMarker));
        Assert.False(Directory.Exists(State));
        Assert.False(Directory.Exists(Backup));
        if (firstInstall)
            Assert.False(Directory.Exists(Destination));
        else
        {
            Assert.False(File.Exists(partial));
            Assert.Equal(originalExe, File.ReadAllBytes(Path.Combine(Destination, "Dvinholm.App.exe")));
            Assert.Equal(originalSettings, File.ReadAllBytes(Path.Combine(Destination, "settings.bin")));
        }
        Assert.Equal("must survive invalid checkpoint", File.ReadAllText(unrelated));
    }


    [Theory]
    [InlineData("missing-backup", false)]
    [InlineData("missing-backup", true)]
    [InlineData("missing-executable", false)]
    [InlineData("missing-executable", true)]
    [InlineData("executable-as-directory", false)]
    [InlineData("executable-as-directory", true)]
    [InlineData("zero-byte-executable", false)]
    [InlineData("zero-byte-executable", true)]
    public void RebootRecovery_InvalidBackupExecutablePreservesFilesUntilRepaired(
        string damage, bool nestedFiles)
    {
        // The real sign-in recovery uses Robocopy and SHA-256 in disposable
        // directories. No production installed app or actual J2534 is touched.
        CreateMarker(PreviousMarker);
        Directory.CreateDirectory(Destination);
        var partiallyInstalled = Path.Combine(Destination, "partial-update.dat");
        File.WriteAllBytes(partiallyInstalled, new byte[] { 0, 255, 9, 254 });
        if (nestedFiles)
        {
            var nested = Path.Combine(Destination, "Incomplete", "Nested");
            Directory.CreateDirectory(nested);
            File.WriteAllText(Path.Combine(nested, "stale.txt"), "partial");
        }

        var backupExe = Path.Combine(Backup, "Dvinholm.App.exe");
        if (damage != "missing-backup")
            Directory.CreateDirectory(Backup);
        if (damage == "missing-executable")
            File.WriteAllText(Path.Combine(Backup, "settings.json"), "{\"rate\":125000}");
        else if (damage == "executable-as-directory")
            Directory.CreateDirectory(backupExe);
        else if (damage == "zero-byte-executable")
            File.WriteAllBytes(backupExe, Array.Empty<byte>());

        var unrelated = Path.Combine(_root, "user-data.txt");
        File.WriteAllText(unrelated, "never touch my files");

        static Dictionary<string, byte[]> Files(string folder) =>
            Directory.Exists(folder)
                ? Directory.GetFiles(folder, "*", SearchOption.AllDirectories)
                    .ToDictionary(p => Path.GetRelativePath(folder, p), File.ReadAllBytes,
                        StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);

        var originalInstalled = Files(Destination);
        var originalBackup = Files(Backup);
        GenerateScript(mockRecoveredAppStartup: true);

        var rejected = ExecuteResume();

        Assert.Equal(0, rejected.ExitCode);
        Assert.True(File.Exists(PreviousMarker));
        Assert.Equal(originalInstalled.Keys.OrderBy(p => p, StringComparer.OrdinalIgnoreCase),
            Files(Destination).Keys.OrderBy(p => p, StringComparer.OrdinalIgnoreCase));
        foreach (var (name, bytes) in originalInstalled)
            Assert.Equal(bytes, Files(Destination)[name]);
        Assert.Equal(originalBackup.Keys.OrderBy(p => p, StringComparer.OrdinalIgnoreCase),
            Files(Backup).Keys.OrderBy(p => p, StringComparer.OrdinalIgnoreCase));
        foreach (var (name, bytes) in originalBackup)
            Assert.Equal(bytes, Files(Backup)[name]);
        Assert.Equal("never touch my files", File.ReadAllText(unrelated));
        Assert.Contains("checkpoint preserved",
            File.ReadAllText(Path.Combine(LocalData, "Dvinholm", "update.log")),
            StringComparison.OrdinalIgnoreCase);

        // Repair only the backup. Its new binary and nested assets become the
        // exact expected installation on retry.
        if (Directory.Exists(backupExe))
            Directory.Delete(backupExe, recursive: true);
        Directory.CreateDirectory(Backup);
        File.WriteAllText(backupExe, "trusted previous executable");
        File.WriteAllBytes(Path.Combine(Backup, "driver.bin"),
            new byte[] { 4, 0, 255, 11 });
        if (nestedFiles)
        {
            var assets = Path.Combine(Backup, "Assets", "Nested");
            Directory.CreateDirectory(assets);
            File.WriteAllBytes(Path.Combine(assets, "can.dat"),
                new byte[] { 0, 1, 255, 3 });
        }

        var expected = Files(Backup);
        var retried = ExecuteResume();

        Assert.Equal(0, retried.ExitCode);
        Assert.False(File.Exists(PreviousMarker));
        Assert.False(Directory.Exists(Backup));
        Assert.False(Directory.Exists(State));
        var actual = Files(Destination);
        Assert.Equal(expected.Keys.OrderBy(p => p, StringComparer.OrdinalIgnoreCase),
            actual.Keys.OrderBy(p => p, StringComparer.OrdinalIgnoreCase));
        foreach (var (name, bytes) in expected)
            Assert.Equal(bytes, actual[name]);
        Assert.False(File.Exists(partiallyInstalled));
        Assert.Equal("never touch my files", File.ReadAllText(unrelated));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }
}
