using System.Diagnostics;
using System.Text;
using Dvinholm.App;

namespace Dvinholm.App.Tests;

/// <summary>
/// Real cross-process Windows mutex tests. The recovery script, Robocopy,
/// SHA-256 and checkpoint cleanup run against temporary directories only.
/// A test-only gate holds the first recovery after it owns the mutex.
/// </summary>
public sealed class UpdateConcurrentRecoveryTests
{
    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, false, true)]
    [InlineData(false, true, false)]
    [InlineData(false, true, true)]
    [InlineData(true, false, false)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    [InlineData(true, true, true)]
    public async Task ConcurrentGuardOrSignIn_CannotModifyPendingRecovery(
        bool firstInstall, bool nestedAssets, bool watcherContender)
    {
        if (!OperatingSystem.IsWindows())
            return;

        var root = Path.Combine(Path.GetTempPath(), "Dvinholm-mutex-tests",
            Guid.NewGuid().ToString("N"));
        var destination = Path.Combine(root, "installed");
        // Unique backup name makes the production named mutex test-specific.
        var backup = Path.Combine(root, "previous-" + Guid.NewGuid().ToString("N"));
        var state = backup + ".state";
        var checkpoint = Path.Combine(state,
            firstInstall ? "first-install.pending" : "previous.pending");
        var scriptPath = Path.Combine(root, "recovery.ps1");
        var logPath = Path.Combine(root, "launch.log");
        var localData = Path.Combine(root, "LocalData");
        var lockSignal = Path.Combine(root, "lock-held.signal");
        var releaseSignal = Path.Combine(root, "release.signal");
        var unrelatedPath = Path.Combine(root, "user-data.txt");
        Directory.CreateDirectory(destination);
        Directory.CreateDirectory(state);
        Directory.CreateDirectory(Path.Combine(localData, "Dvinholm"));
        File.WriteAllText(checkpoint, "pending");
        File.WriteAllText(Path.Combine(destination, "partial.dat"), "old partial replacement");
        File.WriteAllText(unrelatedPath, "never modified");

        Dictionary<string, byte[]>? previousSnapshot = null;
        if (!firstInstall)
        {
            Directory.CreateDirectory(backup);
            File.WriteAllText(Path.Combine(backup, "Dvinholm.App.exe"), "trusted previous build");
            File.WriteAllBytes(Path.Combine(backup, "settings.bin"),
                new byte[] { 0, 255, 1, 240 });
            if (nestedAssets)
            {
                var assets = Path.Combine(backup, "Assets", "Nested");
                Directory.CreateDirectory(assets);
                File.WriteAllBytes(Path.Combine(assets, "driver.dat"),
                    new byte[] { 99, 0, 254, 1 });
            }
            previousSnapshot = Snapshot(backup);
        }
        else if (nestedAssets)
        {
            var nested = Path.Combine(destination, "broken", "partial");
            Directory.CreateDirectory(nested);
            File.WriteAllBytes(Path.Combine(nested, "old.dat"),
                new byte[] { 0, 255 });
        }

        const string startRecovered =
            "$restoredProcess = Start-Process -FilePath $restoredExe -WorkingDirectory $destination -PassThru";
        const string mutexGate = "if (-not $haveRecoveryLock) { exit 0 }";
        var generated = LatestBuildUpdater.BuildReplacementScript(
            Path.Combine(root, "staging"), destination, -1, scriptPath, backup);
        Assert.Contains(startRecovered, generated);
        Assert.Contains(mutexGate, generated);
        generated = generated
            .Replace(startRecovered,
                "Add-Content -LiteralPath $env:DVINHOLM_TEST_LAUNCH_LOG -Value $restoredExe\n" +
                "$restoredProcess = [pscustomobject]@{ HasExited = $false }",
                StringComparison.Ordinal)
            .Replace("Start-Sleep -Seconds 5", "Start-Sleep -Milliseconds 1",
                StringComparison.Ordinal)
            .Replace(mutexGate, mutexGate + "\n" + """
                # Test-only synchronization inserted AFTER acquiring the real named
                # mutex, BEFORE touching the pending recovery transaction.
                if ($env:DVINHOLM_TEST_LOCK_SIGNAL) {
                    'held' | Set-Content -LiteralPath $env:DVINHOLM_TEST_LOCK_SIGNAL
                    while (-not (Test-Path -LiteralPath $env:DVINHOLM_TEST_RELEASE_SIGNAL)) {
                        Start-Sleep -Milliseconds 25
                    }
                }
                """, StringComparison.Ordinal);
        File.WriteAllText(scriptPath, generated, new UTF8Encoding(false));

        try
        {
            // This is a genuine separate PowerShell process, not a mocked mutex.
            var primary = RunRecoveryAsync(scriptPath, watcher: false,
                localData, logPath, lockSignal, releaseSignal);
            try
            {
                using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20)))
                {
                    while (!File.Exists(lockSignal))
                    {
                        if (primary.IsCompleted)
                        {
                            var earlyExit = await primary;
                            Assert.True(false, "Primary exited before locking: " + earlyExit.Output);
                        }
                        await Task.Delay(25, timeout.Token);
                    }
                }

                // A second, independent process races against the held lock.
                // Alternate watcher and sign-in entrypoints across eight cases.
                var contender = await RunRecoveryAsync(scriptPath, watcherContender,
                    localData, logPath, null, null);
                Assert.Equal(0, contender.Code);
                Assert.True(File.Exists(checkpoint),
                    "Second process must not disarm the pending checkpoint.");
                Assert.Equal("old partial replacement",
                    File.ReadAllText(Path.Combine(destination, "partial.dat")));
                Assert.Equal("never modified", File.ReadAllText(unrelatedPath));
                if (!firstInstall)
                    AssertSnapshot(previousSnapshot!, Snapshot(backup));
                Assert.False(File.Exists(logPath),
                    "Blocked contender must not launch a recovered executable.");
            }
            finally
            {
                // Release even when an assertion fails, so no helper is stranded.
                File.WriteAllText(releaseSignal, "release");
            }

            var primaryResult = await primary;
            Assert.Equal(0, primaryResult.Code);
            Assert.False(File.Exists(checkpoint));
            Assert.False(Directory.Exists(state));
            Assert.False(Directory.Exists(backup));
            Assert.Equal("never modified", File.ReadAllText(unrelatedPath));
            if (firstInstall)
            {
                Assert.False(Directory.Exists(destination));
                Assert.False(File.Exists(logPath));
            }
            else
            {
                AssertSnapshot(previousSnapshot!, Snapshot(destination));
                Assert.False(File.Exists(Path.Combine(destination, "partial.dat")));
                Assert.Single(File.ReadAllLines(logPath));
            }

            // A later retry must not re-run committed recovery.
            var retry = await RunRecoveryAsync(scriptPath, watcher: false,
                localData, logPath, null, null);
            Assert.Equal(0, retry.Code);
            Assert.Equal("never modified", File.ReadAllText(unrelatedPath));
            if (!firstInstall)
            {
                AssertSnapshot(previousSnapshot!, Snapshot(destination));
                Assert.Single(File.ReadAllLines(logPath));
            }
            else
                Assert.False(Directory.Exists(destination));
        }
        finally
        {
            File.WriteAllText(releaseSignal, "release");
            for (var attempt = 0; Directory.Exists(root); attempt++)
            {
                try { Directory.Delete(root, recursive: true); }
                catch (IOException) when (attempt < 49) { await Task.Delay(100); }
                catch (UnauthorizedAccessException) when (attempt < 49)
                {
                    await Task.Delay(100);
                }
            }
        }
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
    public async Task SameBackupLeafDifferentInstallations_RecoveryLocksRemainIndependent(
        bool firstInstallA, bool firstInstallB, bool apostrophePaths)
    {
        if (!OperatingSystem.IsWindows())
            return;

        var root = Path.Combine(Path.GetTempPath(),
            "Dvinholm-distinct-recovery-mutex-" + Guid.NewGuid().ToString("N"));
        var rootA = Path.Combine(root, apostrophePaths ? "O'Neil's A" : "install A");
        var rootB = Path.Combine(root, apostrophePaths ? "O'Neil's B" : "install B");
        var backupA = Path.Combine(rootA, "same-backup-name");
        var backupB = Path.Combine(rootB, "same-backup-name");
        var stateA = backupA + ".state";
        var stateB = backupB + ".state";
        var destinationA = Path.Combine(rootA, "installed");
        var destinationB = Path.Combine(rootB, "installed");
        var scriptA = Path.Combine(rootA, "recovery.ps1");
        var scriptB = Path.Combine(rootB, "recovery.ps1");
        var dataA = Path.Combine(rootA, "LocalData");
        var dataB = Path.Combine(rootB, "LocalData");
        var launchedA = Path.Combine(rootA, "launched.txt");
        var launchedB = Path.Combine(rootB, "launched.txt");
        var lockedA = Path.Combine(rootA, "locked.signal");
        var unlockedA = Path.Combine(rootA, "release.signal");
        Directory.CreateDirectory(rootA);
        Directory.CreateDirectory(rootB);
        Directory.CreateDirectory(Path.Combine(dataA, "Dvinholm"));
        Directory.CreateDirectory(Path.Combine(dataB, "Dvinholm"));

        var identityA = LatestBuildUpdater.RecoveryTransactionId(backupA);
        var identityB = LatestBuildUpdater.RecoveryTransactionId(backupB);
        Assert.NotEqual(identityA, identityB);
        Assert.Equal(identityA, LatestBuildUpdater.RecoveryTransactionId(
            backupA.ToUpperInvariant()));
        Assert.Equal(identityB, LatestBuildUpdater.RecoveryTransactionId(
            backupB.ToUpperInvariant()));

        static Dictionary<string, byte[]> Setup(
            string installed, string backup, string state, bool firstInstall, string tag)
        {
            Directory.CreateDirectory(installed);
            Directory.CreateDirectory(state);
            File.WriteAllText(Path.Combine(state,
                firstInstall ? "first-install.pending" : "previous.pending"), "pending");
            File.WriteAllText(Path.Combine(installed, "partial.dat"), "partial " + tag);
            if (firstInstall)
                return new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
            Directory.CreateDirectory(backup);
            File.WriteAllText(Path.Combine(backup, "Dvinholm.App.exe"), "old build " + tag);
            File.WriteAllBytes(Path.Combine(backup, "asset.bin"),
                new byte[] { 0, 255, 1, (byte)tag[0] });
            return Snapshot(backup);
        }

        var expectedA = Setup(destinationA, backupA, stateA, firstInstallA, "A");
        var expectedB = Setup(destinationB, backupB, stateB, firstInstallB, "B");
        var unrelatedA = Path.Combine(rootA, "personal.txt");
        var unrelatedB = Path.Combine(rootB, "personal.txt");
        File.WriteAllText(unrelatedA, "user A");
        File.WriteAllText(unrelatedB, "user B");

        static string Prepare(string rootPath, string destination, string backup,
            string script, string id, bool gate)
        {
            const string realStart =
                "$restoredProcess = Start-Process -FilePath $restoredExe -WorkingDirectory $destination -PassThru";
            const string mutexGate = "if (-not $haveRecoveryLock) { exit 0 }";
            var generated = LatestBuildUpdater.BuildReplacementScript(
                Path.Combine(rootPath, "staging"), destination, -1, script, backup);
            Assert.Contains(realStart, generated);
            Assert.Contains(mutexGate, generated);
            Assert.Contains($"$runName = 'DvinholmRecovery-{id}'", generated);
            Assert.Contains($"$mutexName = 'Local\\DvinholmRecovery-{id}'", generated);
            generated = generated.Replace(realStart,
                "Add-Content -LiteralPath $env:DVINHOLM_TEST_LAUNCH_LOG -Value $restoredExe\n" +
                "$restoredProcess = [pscustomobject]@{ HasExited = $false }",
                StringComparison.Ordinal)
                .Replace("Start-Sleep -Seconds 5", "Start-Sleep -Milliseconds 1",
                    StringComparison.Ordinal);
            if (gate)
                generated = generated.Replace(mutexGate, mutexGate + "\n" + """
                    'held' | Set-Content -LiteralPath $env:DVINHOLM_TEST_LOCK_SIGNAL
                    while (-not (Test-Path -LiteralPath $env:DVINHOLM_TEST_RELEASE_SIGNAL)) {
                        Start-Sleep -Milliseconds 25
                    }
                    """, StringComparison.Ordinal);
            File.WriteAllText(script, generated, new UTF8Encoding(false));
            return generated;
        }

        Prepare(rootA, destinationA, backupA, scriptA, identityA, gate: true);
        Prepare(rootB, destinationB, backupB, scriptB, identityB, gate: false);

        Task<(int Code, string Output)>? owner = null;
        try
        {
            owner = RunRecoveryAsync(scriptA, watcher: false, dataA, launchedA,
                lockedA, unlockedA);
            using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20)))
            {
                while (!File.Exists(lockedA))
                {
                    if (owner.IsCompleted)
                    {
                        var early = await owner;
                        Assert.True(false, "Owner exited without mutex: " + early.Output);
                    }
                    await Task.Delay(25, timeout.Token);
                }
            }

            // B must complete WHILE A holds its named mutex, proving the
            // full-path identity rather than a shared backup leaf is used.
            var resultB = await RunRecoveryAsync(scriptB, watcher: false,
                dataB, launchedB, null, null);
            Assert.Equal(0, resultB.Code);
            Assert.True(File.Exists(Path.Combine(stateA,
                firstInstallA ? "first-install.pending" : "previous.pending")));
            Assert.Equal("partial A", File.ReadAllText(Path.Combine(destinationA, "partial.dat")));
            Assert.False(Directory.Exists(stateB));
            Assert.False(Directory.Exists(backupB));
            if (firstInstallB)
            {
                Assert.False(Directory.Exists(destinationB));
                Assert.False(File.Exists(launchedB));
            }
            else
            {
                AssertSnapshot(expectedB, Snapshot(destinationB));
                Assert.Single(File.ReadAllLines(launchedB));
            }

            File.WriteAllText(unlockedA, "release");
            var resultA = await owner;
            Assert.Equal(0, resultA.Code);
            Assert.False(Directory.Exists(stateA));
            Assert.False(Directory.Exists(backupA));
            if (firstInstallA)
            {
                Assert.False(Directory.Exists(destinationA));
                Assert.False(File.Exists(launchedA));
            }
            else
            {
                AssertSnapshot(expectedA, Snapshot(destinationA));
                Assert.Single(File.ReadAllLines(launchedA));
            }
            Assert.Equal("user A", File.ReadAllText(unrelatedA));
            Assert.Equal("user B", File.ReadAllText(unrelatedB));
        }
        finally
        {
            File.WriteAllText(unlockedA, "release");
            if (owner is not null)
                try { await owner; } catch { /* test assertion handles failure */ }
            for (var attempt = 0; Directory.Exists(root); attempt++)
            {
                try { Directory.Delete(root, recursive: true); }
                catch (IOException) when (attempt < 49) { await Task.Delay(100); }
                catch (UnauthorizedAccessException) when (attempt < 49)
                {
                    await Task.Delay(100);
                }
            }
        }
    }

    private static Dictionary<string, byte[]> Snapshot(string path) =>
        Directory.GetFiles(path, "*", SearchOption.AllDirectories)
            .ToDictionary(file => Path.GetRelativePath(path, file), File.ReadAllBytes,
                StringComparer.OrdinalIgnoreCase);

    private static void AssertSnapshot(
        Dictionary<string, byte[]> expected, Dictionary<string, byte[]> actual)
    {
        Assert.Equal(expected.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase),
            actual.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase));
        foreach (var (name, bytes) in expected)
            Assert.Equal(bytes, actual[name]);
    }

    private static async Task<(int Code, string Output)> RunRecoveryAsync(
        string scriptPath, bool watcher, string localData, string launchLog,
        string? lockSignal, string? releaseSignal)
    {
        var executable = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            "System32", "WindowsPowerShell", "v1.0", "powershell.exe");
        var start = new ProcessStartInfo
        {
            FileName = executable,
            WorkingDirectory = Path.GetDirectoryName(scriptPath)!,
            UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true
        };
        foreach (var arg in new[]
                 { "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass",
                   "-File", scriptPath })
            start.ArgumentList.Add(arg);
        if (watcher)
        {
            start.ArgumentList.Add("-Watch");
            start.ArgumentList.Add("-HelperPid");
            start.ArgumentList.Add("2147483647");
        }
        else
            start.ArgumentList.Add("-RecoverOnLogon");
        start.Environment["LOCALAPPDATA"] = localData;
        start.Environment["DVINHOLM_TEST_LAUNCH_LOG"] = launchLog;
        // Null means no synchronization gate for this invocation.
        start.Environment["DVINHOLM_TEST_LOCK_SIGNAL"] = lockSignal ?? "";
        start.Environment["DVINHOLM_TEST_RELEASE_SIGNAL"] = releaseSignal ?? "";
        using var process = Process.Start(start)
            ?? throw new InvalidOperationException("Could not start recovery PowerShell.");
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await process.WaitForExitAsync(timeout.Token);
        return (process.ExitCode, await stdout + await stderr);
    }
}
