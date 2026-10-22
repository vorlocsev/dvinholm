using System.Diagnostics;
using System.Text;
using Dvinholm.App;

namespace Dvinholm.App.Tests;

/// <summary>
/// Exercises corrupted or absent recovery service files with real Windows
/// PowerShell, isolated backups, checkpoint files and Robocopy. Only
/// launching the recovered app executable is mocked.
/// </summary>
public sealed class UpdateRecoveryServiceFileTests
{
    [Theory]
    [InlineData(false, "missing-identity")]
    [InlineData(false, "damaged-identity")]
    [InlineData(false, "missing-resume")]
    [InlineData(false, "damaged-resume")]
    [InlineData(true, "missing-identity")]
    [InlineData(true, "damaged-identity")]
    [InlineData(true, "missing-resume")]
    [InlineData(true, "damaged-resume")]
    public async Task ServiceFileFailure_PreservesRecoveryAndAllowsSafeRetry(
        bool firstInstall, string failure)
    {
        if (!OperatingSystem.IsWindows())
            return;

        var root = Path.Combine(Path.GetTempPath(), "Dvinholm-service-file-tests",
            Guid.NewGuid().ToString("N"));
        var destination = Path.Combine(root, "installed");
        var backup = Path.Combine(root, "backup");
        var state = backup + ".state";
        var marker = Path.Combine(state,
            firstInstall ? "first-install.pending" : "previous.pending");
        var identity = Path.Combine(state, "helper.identity");
        var resume = Path.Combine(state, "resume.ps1");
        var script = Path.Combine(root, "helper.ps1");
        var localData = Path.Combine(root, "LocalData");
        var userFile = Path.Combine(root, "unrelated-user-file.txt");
        var recoveryLog = Path.Combine(localData, "Dvinholm", "update.log");

        Directory.CreateDirectory(destination);
        Directory.CreateDirectory(backup);
        Directory.CreateDirectory(state);
        Directory.CreateDirectory(Path.Combine(localData, "Dvinholm"));
        File.WriteAllText(marker, "pending");
        File.WriteAllText(userFile, "my data stays");
        File.WriteAllText(Path.Combine(destination, "partial.tmp"), "failed update");
        if (!firstInstall)
        {
            File.WriteAllText(Path.Combine(backup, "Dvinholm.App.exe"), "good previous build");
            File.WriteAllBytes(Path.Combine(backup, "settings.bin"),
                new byte[] { 0, 255, 9, 254 });
        }
        var originalInstalled = Snapshot(destination);
        var previousBackup = Snapshot(backup);

        var generated = LatestBuildUpdater.BuildReplacementScript(
            Path.Combine(root, "staging"), destination, -1, script, backup);
        const string recoveredLaunch =
            "$restoredProcess = Start-Process -FilePath $restoredExe -WorkingDirectory $destination -PassThru";
        Assert.Contains(recoveredLaunch, generated);
        Assert.Contains(
            "if ((GetSha256File $scriptPath) -ne (GetSha256File $recoveryScript))",
            generated);
        generated = generated.Replace(recoveredLaunch,
                "$restoredProcess = [pscustomobject]@{ HasExited = $false }",
                StringComparison.Ordinal)
            .Replace("Start-Sleep -Seconds 5", "Start-Sleep -Milliseconds 1",
                StringComparison.Ordinal);
        File.WriteAllText(script, generated, new UTF8Encoding(false));

        switch (failure)
        {
            case "missing-identity":
                File.WriteAllText(resume, generated, new UTF8Encoding(false));
                break;
            case "damaged-identity":
                File.WriteAllText(identity, "truncated|not-a-timestamp");
                File.WriteAllText(resume, generated, new UTF8Encoding(false));
                break;
            case "missing-resume":
                File.WriteAllText(identity, "2147483647|1");
                break;
            case "damaged-resume":
                File.WriteAllText(identity, "2147483647|1");
                File.WriteAllText(resume, "}\n", new UTF8Encoding(false));
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(failure));
        }

        try
        {
            if (failure == "damaged-identity")
            {
                // A torn identity may belong to a live helper: sign-in must
                // not infer that it is safe to delete or overwrite anything.
                var blocked = await ExecuteAsync(script, localData, watch: false);
                Assert.Equal(0, blocked.Code);
                Assert.True(File.Exists(marker));
                AssertSnapshot(originalInstalled, Snapshot(destination));
                AssertSnapshot(previousBackup, Snapshot(backup));
                Assert.Contains("checkpoint preserved", File.ReadAllText(recoveryLog),
                    StringComparison.OrdinalIgnoreCase);
                Assert.Contains("identity is malformed", File.ReadAllText(recoveryLog),
                    StringComparison.OrdinalIgnoreCase);

                // A repaired/missing helper identity may be retried safely.
                File.Delete(identity);
            }
            else if (failure.EndsWith("-resume", StringComparison.Ordinal))
            {
                // If the sign-in recovery file was lost or corrupted, Windows
                // PowerShell cannot execute it. The live watcher, running
                // its *own valid script*, can still finish the transaction.
                var unavailable = await ExecuteAsync(resume, localData, watch: false);
                Assert.NotEqual(0, unavailable.Code);
                Assert.True(File.Exists(marker));
                AssertSnapshot(originalInstalled, Snapshot(destination));
                AssertSnapshot(previousBackup, Snapshot(backup));
            }

            var usingGuard = failure.EndsWith("-resume", StringComparison.Ordinal);
            var recovered = await ExecuteAsync(script, localData, usingGuard);

            Assert.Equal(0, recovered.Code);
            Assert.False(File.Exists(marker));
            Assert.False(Directory.Exists(state));
            Assert.False(Directory.Exists(backup));
            Assert.Equal("my data stays", File.ReadAllText(userFile));
            if (firstInstall)
                Assert.False(Directory.Exists(destination));
            else
            {
                AssertSnapshot(previousBackup, Snapshot(destination));
                Assert.False(File.Exists(Path.Combine(destination, "partial.tmp")));
            }

            // Any redundant sign-in recovery must be idempotent.
            var repeated = await ExecuteAsync(script, localData, watch: false);
            Assert.Equal(0, repeated.Code);
            Assert.False(Directory.Exists(state));
            Assert.Equal("my data stays", File.ReadAllText(userFile));
            if (firstInstall)
                Assert.False(Directory.Exists(destination));
            else
                AssertSnapshot(previousBackup, Snapshot(destination));
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

    private static async Task<(int Code, string Output)> ExecuteAsync(
        string script, string localData, bool watch)
    {
        var powerShell = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            "System32", "WindowsPowerShell", "v1.0", "powershell.exe");
        var start = new ProcessStartInfo
        {
            FileName = powerShell,
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
        if (watch)
        {
            start.ArgumentList.Add("-Watch");
            start.ArgumentList.Add("-HelperPid");
            start.ArgumentList.Add(int.MaxValue.ToString());
        }
        else
            start.ArgumentList.Add("-RecoverOnLogon");
        start.Environment["LOCALAPPDATA"] = localData;
        using var process = Process.Start(start)
            ?? throw new InvalidOperationException("Could not start PowerShell service-file test.");
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(35));
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
