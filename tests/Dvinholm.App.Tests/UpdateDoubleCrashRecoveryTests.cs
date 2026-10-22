using System.Diagnostics;
using System.Text;
using Dvinholm.App;

namespace Dvinholm.App.Tests;

/// <summary>
/// Two genuine Windows process kills at distinct transaction phases, followed
/// by an actual isolated PowerShell recovery using real Robocopy and SHA-256.
/// No installed Dvinholm executable, actual reboot or real adapter is used.
/// </summary>
public sealed class UpdateDoubleCrashRecoveryTests
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
    public async Task TwoKilledRecoveryOwners_ThirdAttemptCommitsExactlyOnce(
        bool firstInstall, bool nestedAssets, bool secondAsWatch)
    {
        if (!OperatingSystem.IsWindows())
            return;

        var root = Path.Combine(Path.GetTempPath(), "dvinholm-double-crash",
            Guid.NewGuid().ToString("N"));
        var backup = Path.Combine(root, "previous-" + Guid.NewGuid().ToString("N"));
        var state = backup + ".state";
        var marker = Path.Combine(state, firstInstall
            ? "first-install.pending" : "previous.pending");
        var destination = Path.Combine(root, "installed");
        var scriptPath = Path.Combine(root, "recovery.ps1");
        var logPath = Path.Combine(root, "launches.txt");
        var unrelated = Path.Combine(root, "personal.txt");
        var appData = Path.Combine(root, "LocalData");

        Directory.CreateDirectory(destination);
        Directory.CreateDirectory(state);
        Directory.CreateDirectory(Path.Combine(appData, "Dvinholm"));
        File.WriteAllText(marker, "pending");
        File.WriteAllText(Path.Combine(destination, "abandoned.dll"), "partial update");
        File.WriteAllText(unrelated, "never change me");
        if (nestedAssets)
        {
            var extra = Path.Combine(destination, "Old", "Nested");
            Directory.CreateDirectory(extra);
            File.WriteAllBytes(Path.Combine(extra, "stale.bin"),
                new byte[] { 9, 0, 255 });
        }

        Dictionary<string, byte[]>? expected = null;
        if (!firstInstall)
        {
            Directory.CreateDirectory(backup);
            File.WriteAllText(Path.Combine(backup, "Dvinholm.App.exe"), "previous-build");
            File.WriteAllBytes(Path.Combine(backup, "setting.bin"), new byte[] { 0, 255, 8, 1 });
            if (nestedAssets)
            {
                var nested = Path.Combine(backup, "Assets", "Nested");
                Directory.CreateDirectory(nested);
                File.WriteAllBytes(Path.Combine(nested, "payload.dat"),
                    new byte[] { 0, 2, 255, 4 });
            }
            expected = Snapshot(backup);
        }

        // Git checkout uses CRLF on Windows; normalize the *test copy* so
        // injection anchors match regardless of the checkout line endings.
        var script = LatestBuildUpdater.BuildReplacementScript(
            Path.Combine(root, "staging"), destination, -1, scriptPath, backup)
            .Replace("\r\n", "\n", StringComparison.Ordinal);
        const string mutexGate = "if (-not $haveRecoveryLock) { exit 0 }";
        const string recoveredLaunch =
            "$restoredProcess = Start-Process -FilePath $restoredExe -WorkingDirectory $destination -PassThru";
        const string mirrorFinished = "EnsureMirroredBytes $backup $destination";
        const string firstInstallRemoved =
            "Remove-Item -LiteralPath $newMarker -Force -ErrorAction Stop";

        Assert.Contains(mutexGate, script);
        Assert.Contains(recoveredLaunch, script);
        Assert.Contains(mirrorFinished, script);
        Assert.Contains(firstInstallRemoved, script);
        Assert.Contains("catch [System.Threading.AbandonedMutexException]", script);

        const string afterLock = """
            if ($env:DVINHOLM_TEST_CRASH_PHASE -eq 'after-lock') {
                'held' | Set-Content -LiteralPath $env:DVINHOLM_TEST_SIGNAL
                while ($true) { Start-Sleep -Milliseconds 25 }
            }
            """;
        const string afterMutation = """
            if ($env:DVINHOLM_TEST_CRASH_PHASE -eq 'after-change') {
                'mutated' | Set-Content -LiteralPath $env:DVINHOLM_TEST_SIGNAL
                while ($true) { Start-Sleep -Milliseconds 25 }
            }
            """;

        // Inject the post-mirror checkpoint before replacing the launch
        // statement, otherwise the original adjacent anchor is already gone.
        script = script.Replace(mirrorFinished + "\n            $restoredProcess",
                mirrorFinished + "\n" + afterMutation + "            $restoredProcess",
                StringComparison.Ordinal)
            .Replace(recoveredLaunch,
                "Add-Content -LiteralPath $env:DVINHOLM_TEST_LAUNCH_LOG -Value $restoredExe\n" +
                "$restoredProcess = [pscustomobject]@{ HasExited = $false }",
                StringComparison.Ordinal)
            .Replace("Start-Sleep -Seconds 5", "Start-Sleep -Milliseconds 1",
                StringComparison.Ordinal)
            .Replace(mutexGate, mutexGate + "\n" + afterLock, StringComparison.Ordinal)
            .Replace(firstInstallRemoved, afterMutation + firstInstallRemoved,
                StringComparison.Ordinal);
        // Both crash gates must be present in the actual recovery path.
        Assert.Contains(afterLock, script);
        Assert.Contains(mirrorFinished + "\n" + afterMutation, script);
        File.WriteAllText(scriptPath, script, new UTF8Encoding(false));

        try
        {
            // First crash occurs with the real mutex acquired, before mutation.
            await KillAtCheckpoint(scriptPath, appData, logPath,
                Path.Combine(root, "first.signal"), "after-lock", asWatch: false);
            Assert.True(File.Exists(marker));
            Assert.True(File.Exists(Path.Combine(destination, "abandoned.dll")));
            Assert.False(File.Exists(logPath));
            Assert.Equal("never change me", File.ReadAllText(unrelated));
            if (!firstInstall)
                AssertSnapshot(expected!, Snapshot(backup));

            // Next owner gets the abandoned mutex, performs the destructive
            // restore/cleanup, then crashes BEFORE the pending marker is disarmed.
            await KillAtCheckpoint(scriptPath, appData, logPath,
                Path.Combine(root, "second.signal"), "after-change", secondAsWatch);
            Assert.True(File.Exists(marker));
            Assert.True(Directory.Exists(state));
            Assert.False(File.Exists(logPath));
            Assert.Equal("never change me", File.ReadAllText(unrelated));
            if (firstInstall)
                Assert.False(Directory.Exists(destination));
            else
            {
                AssertSnapshot(expected!, Snapshot(destination));
                AssertSnapshot(expected!, Snapshot(backup));
            }

            // Third attempt must be able to repeat the mutation safely,
            // restore the previous build / delete failed first install,
            // remove the checkpoint, and launch at most one recovered app.
            var recovered = await RunRecovery(scriptPath, appData, logPath,
                asWatch: !secondAsWatch);
            Assert.Equal(0, recovered.ExitCode);
            Assert.False(File.Exists(marker));
            Assert.False(Directory.Exists(state));
            Assert.False(Directory.Exists(backup));
            Assert.Equal("never change me", File.ReadAllText(unrelated));
            if (firstInstall)
            {
                Assert.False(Directory.Exists(destination));
                Assert.False(File.Exists(logPath));
            }
            else
            {
                AssertSnapshot(expected!, Snapshot(destination));
                Assert.Single(File.ReadAllLines(logPath));
            }

            var repeated = await RunRecovery(scriptPath, appData, logPath, asWatch: false);
            Assert.Equal(0, repeated.ExitCode);
            Assert.Equal("never change me", File.ReadAllText(unrelated));
            if (firstInstall)
                Assert.False(Directory.Exists(destination));
            else
            {
                AssertSnapshot(expected!, Snapshot(destination));
                Assert.Single(File.ReadAllLines(logPath));
            }
        }
        finally
        {
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

    private static async Task KillAtCheckpoint(
        string script, string appData, string launchLog, string signal,
        string crashPhase, bool asWatch)
    {
        using var owner = StartRecovery(script, appData, launchLog,
            asWatch, crashPhase, signal);
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            while (!File.Exists(signal))
            {
                Assert.False(owner.HasExited,
                    $"Recovery process exited before test gate {crashPhase}.");
                await Task.Delay(25, timeout.Token);
            }
        }
        finally
        {
            if (!owner.HasExited)
            {
                owner.Kill(entireProcessTree: true);
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                await owner.WaitForExitAsync(timeout.Token);
            }
        }
        Assert.True(owner.HasExited);
    }

    private static Process StartRecovery(string script, string appData,
        string launchLog, bool asWatch, string crashPhase, string signal)
    {
        var executable = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            "System32", "WindowsPowerShell", "v1.0", "powershell.exe");
        var start = new ProcessStartInfo
        {
            FileName = executable,
            WorkingDirectory = Path.GetDirectoryName(script)!,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (var argument in new[]
                 { "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass",
                   "-File", script })
            start.ArgumentList.Add(argument);
        if (asWatch)
        {
            start.ArgumentList.Add("-Watch");
            start.ArgumentList.Add("-HelperPid");
            start.ArgumentList.Add(int.MaxValue.ToString());
        }
        else
            start.ArgumentList.Add("-RecoverOnLogon");
        start.Environment["LOCALAPPDATA"] = appData;
        start.Environment["DVINHOLM_TEST_LAUNCH_LOG"] = launchLog;
        start.Environment["DVINHOLM_TEST_CRASH_PHASE"] = crashPhase;
        start.Environment["DVINHOLM_TEST_SIGNAL"] = signal;
        return Process.Start(start)
            ?? throw new InvalidOperationException("Recovery test process did not start.");
    }

    private static async Task<(int ExitCode, string Output)> RunRecovery(
        string script, string appData, string launchLog, bool asWatch)
    {
        using var process = StartRecovery(script, appData, launchLog,
            asWatch, "", "");
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await process.WaitForExitAsync(timeout.Token);
        return (process.ExitCode, await stdout + await stderr);
    }

    private static Dictionary<string, byte[]> Snapshot(string directory) =>
        Directory.GetFiles(directory, "*", SearchOption.AllDirectories)
            .ToDictionary(path => Path.GetRelativePath(directory, path),
                File.ReadAllBytes, StringComparer.OrdinalIgnoreCase);

    private static void AssertSnapshot(
        Dictionary<string, byte[]> expected, Dictionary<string, byte[]> actual)
    {
        Assert.Equal(expected.Keys.OrderBy(x => x, StringComparer.OrdinalIgnoreCase),
            actual.Keys.OrderBy(x => x, StringComparer.OrdinalIgnoreCase));
        foreach (var (file, bytes) in expected)
            Assert.Equal(bytes, actual[file]);
    }
}
