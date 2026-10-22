using System.Diagnostics;
using System.Text;
using Dvinholm.App;

namespace Dvinholm.App.Tests;

/// <summary>
/// Executes actual Windows recovery PowerShell processes against isolated
/// temporary directories. Kills the real mutex owner after it acquires the
/// production lock, then proves a second process can recover the checkpoint.
/// </summary>
public sealed class UpdateAbandonedMutexTests
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
    public async Task KilledMutexOwner_AllowsNextGuardOrLoginRecoveryToComplete(
        bool firstInstall, bool nestedAssets, bool resumeAsWatch)
    {
        if (!OperatingSystem.IsWindows())
            return;

        var root = Path.Combine(Path.GetTempPath(),
            "Dvinholm-abandoned-mutex", Guid.NewGuid().ToString("N"));
        var destination = Path.Combine(root, "installed");
        var backup = Path.Combine(root, "backup-" + Guid.NewGuid().ToString("N"));
        var state = backup + ".state";
        var scriptPath = Path.Combine(root, "resume.ps1");
        var signal = Path.Combine(root, "owner-acquired.signal");
        var release = Path.Combine(root, "release.signal");
        var localData = Path.Combine(root, "LocalData");
        var launchLog = Path.Combine(root, "recovered-launches.log");
        var unrelated = Path.Combine(root, "personal.txt");
        var checkpoint = Path.Combine(state,
            firstInstall ? "first-install.pending" : "previous.pending");
        Directory.CreateDirectory(destination);
        Directory.CreateDirectory(state);
        Directory.CreateDirectory(Path.Combine(localData, "Dvinholm"));
        File.WriteAllText(checkpoint, "pending");
        File.WriteAllText(Path.Combine(destination, "stale.tmp"), "partial update");
        File.WriteAllText(unrelated, "untouched user data");
        Dictionary<string, byte[]>? expected = null;
        if (!firstInstall)
        {
            Directory.CreateDirectory(backup);
            File.WriteAllText(Path.Combine(backup, "Dvinholm.App.exe"), "trusted old application");
            File.WriteAllBytes(Path.Combine(backup, "prefs.bin"),
                new byte[] { 0, 255, 1, 254 });
            if (nestedAssets)
            {
                var assetRoot = Path.Combine(backup, "Assets", "Deep");
                Directory.CreateDirectory(assetRoot);
                File.WriteAllBytes(Path.Combine(assetRoot, "can.dat"),
                    new byte[] { 7, 0, 253, 99 });
            }
            expected = Snapshot(backup);
        }
        else if (nestedAssets)
        {
            var partial = Path.Combine(destination, "Incomplete", "Nested");
            Directory.CreateDirectory(partial);
            File.WriteAllBytes(Path.Combine(partial, "broken.bin"),
                new byte[] { 0, 255 });
        }

        var script = LatestBuildUpdater.BuildReplacementScript(
            Path.Combine(root, "staging"), destination, -1, scriptPath, backup);
        const string processLaunch =
            "$restoredProcess = Start-Process -FilePath $restoredExe -WorkingDirectory $destination -PassThru";
        const string mutexGate = "if (-not $haveRecoveryLock) { exit 0 }";
        Assert.Contains(processLaunch, script);
        Assert.Contains(mutexGate, script);
        Assert.Contains("catch [System.Threading.AbandonedMutexException]", script);
        var lockName = LatestBuildUpdater.RecoveryTransactionId(backup);
        Assert.Contains($"$mutexName = 'Local\\DvinholmRecovery-{lockName}'", script);

        // No file mirror or checkpoint operation is mocked. Only app launch
        // and a gate immediately after production mutex acquisition differ.
        script = script.Replace(processLaunch,
                "Add-Content -LiteralPath $env:DVINHOLM_TEST_LAUNCH_LOG -Value $restoredExe\n" +
                "$restoredProcess = [pscustomobject]@{ HasExited = $false }",
                StringComparison.Ordinal)
            .Replace("Start-Sleep -Seconds 5", "Start-Sleep -Milliseconds 1",
                StringComparison.Ordinal)
            .Replace(mutexGate, mutexGate + "\n" + """
                if ($env:DVINHOLM_TEST_LOCK_SIGNAL) {
                    'owned' | Set-Content -LiteralPath $env:DVINHOLM_TEST_LOCK_SIGNAL
                    while (-not (Test-Path -LiteralPath $env:DVINHOLM_TEST_RELEASE_SIGNAL)) {
                        Start-Sleep -Milliseconds 25
                    }
                }
                """, StringComparison.Ordinal);
        File.WriteAllText(scriptPath, script, new UTF8Encoding(false));

        Process? owner = null;
        try
        {
            owner = StartRecovery(scriptPath, asWatch: false, localData,
                launchLog, signal, release);
            using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20)))
            {
                while (!File.Exists(signal))
                {
                    Assert.False(owner.HasExited,
                        $"First recovery exited before acquiring its mutex: {owner.ExitCode}");
                    await Task.Delay(25, timeout.Token);
                }
            }

            // The first process owns the real Windows named mutex but has not
            // touched the checkpoint or installation. Force a crash.
            Assert.True(File.Exists(checkpoint));
            Assert.True(File.Exists(Path.Combine(destination, "stale.tmp")));
            owner.Kill(entireProcessTree: true);
            using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15)))
                await owner.WaitForExitAsync(timeout.Token);
            Assert.True(owner.HasExited);
            Assert.True(File.Exists(checkpoint));
            Assert.True(Directory.Exists(state));
            if (!firstInstall)
                AssertSnapshot(expected!, Snapshot(backup));
            Assert.Equal("untouched user data", File.ReadAllText(unrelated));

            // A fresh process receives ownership of the abandoned mutex. It
            // must run real Robocopy and SHA-256 validation successfully.
            var resumed = await ExecuteRecovery(scriptPath, resumeAsWatch,
                localData, launchLog);
            Assert.Equal(0, resumed.ExitCode);
            Assert.False(File.Exists(checkpoint));
            Assert.False(Directory.Exists(state));
            Assert.False(Directory.Exists(backup));
            Assert.Equal("untouched user data", File.ReadAllText(unrelated));
            if (firstInstall)
            {
                Assert.False(Directory.Exists(destination));
                Assert.False(File.Exists(launchLog));
            }
            else
            {
                AssertSnapshot(expected!, Snapshot(destination));
                Assert.False(File.Exists(Path.Combine(destination, "stale.tmp")));
                Assert.Single(File.ReadAllLines(launchLog));
            }

            // Repeating a sign-in resume after success is harmless.
            var duplicate = await ExecuteRecovery(scriptPath, asWatch: false,
                localData, launchLog);
            Assert.Equal(0, duplicate.ExitCode);
            Assert.False(Directory.Exists(state));
            Assert.Equal("untouched user data", File.ReadAllText(unrelated));
            if (firstInstall)
                Assert.False(Directory.Exists(destination));
            else
            {
                AssertSnapshot(expected!, Snapshot(destination));
                Assert.Single(File.ReadAllLines(launchLog));
            }
        }
        finally
        {
            File.WriteAllText(release, "release");
            if (owner is not null)
            {
                if (!owner.HasExited)
                {
                    owner.Kill(entireProcessTree: true);
                    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                    await owner.WaitForExitAsync(timeout.Token);
                }
                owner.Dispose();
            }
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

    private static Process StartRecovery(
        string scriptPath, bool asWatch, string localData, string launchLog,
        string? lockSignal, string? releaseSignal)
    {
        var powerShell = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            "System32", "WindowsPowerShell", "v1.0", "powershell.exe");
        var psi = new ProcessStartInfo
        {
            FileName = powerShell,
            WorkingDirectory = Path.GetDirectoryName(scriptPath)!,
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (var argument in new[]
                 { "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass",
                   "-File", scriptPath })
            psi.ArgumentList.Add(argument);
        if (asWatch)
        {
            psi.ArgumentList.Add("-Watch");
            psi.ArgumentList.Add("-HelperPid");
            psi.ArgumentList.Add(int.MaxValue.ToString());
        }
        else
            psi.ArgumentList.Add("-RecoverOnLogon");
        psi.Environment["LOCALAPPDATA"] = localData;
        psi.Environment["DVINHOLM_TEST_LAUNCH_LOG"] = launchLog;
        psi.Environment["DVINHOLM_TEST_LOCK_SIGNAL"] = lockSignal ?? "";
        psi.Environment["DVINHOLM_TEST_RELEASE_SIGNAL"] = releaseSignal ?? "";
        return Process.Start(psi)
            ?? throw new InvalidOperationException("Could not start PowerShell recovery.");
    }

    private static async Task<(int ExitCode, string Output)> ExecuteRecovery(
        string scriptPath, bool asWatch, string localData, string launchLog)
    {
        using var process = StartRecovery(scriptPath, asWatch,
            localData, launchLog, null, null);
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await process.WaitForExitAsync(timeout.Token);
        return (process.ExitCode, await stdout + await stderr);
    }

    private static Dictionary<string, byte[]> Snapshot(string path) =>
        Directory.GetFiles(path, "*", SearchOption.AllDirectories)
            .ToDictionary(file => Path.GetRelativePath(path, file),
                File.ReadAllBytes, StringComparer.OrdinalIgnoreCase);

    private static void AssertSnapshot(
        Dictionary<string, byte[]> expected, Dictionary<string, byte[]> actual)
    {
        Assert.Equal(expected.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase),
            actual.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase));
        foreach (var (file, bytes) in expected)
            Assert.Equal(bytes, actual[file]);
    }
}
