using System.Diagnostics;
using System.Text;
using Dvinholm.App;

namespace Dvinholm.App.Tests;

/// <summary>
/// Runs the generated update helper on a disposable Windows directory tree.
/// Robocopy backup, forward replacement, and rollback all run for real.
/// Only process launching, watcher acknowledgement and HKCU registration
/// are substituted. No installed Dvinholm build or physical J2534 is used.
/// </summary>
public sealed class UpdateFullReplacementIntegrationTests
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
    public async Task Replacement_UsesRealMirrorsAndCommitsOrRestoresExactBytes(
        bool firstInstall, bool startupFails, bool nestedAssets)
    {
        if (!OperatingSystem.IsWindows())
            return;

        var root = Path.Combine(Path.GetTempPath(), "Dvinholm-full-replace", Guid.NewGuid().ToString("N"));
        var source = Path.Combine(root, "staged");
        var destination = Path.Combine(root, "installed");
        var backup = Path.Combine(root, "backup");
        var state = backup + ".state";
        var scriptPath = Path.Combine(root, "update.ps1");
        var localAppData = Path.Combine(root, "LocalData");
        var launchLog = Path.Combine(root, "launch.log");
        Directory.CreateDirectory(source);
        Directory.CreateDirectory(Path.Combine(localAppData, "Dvinholm"));
        File.WriteAllText(Path.Combine(source, "Dvinholm.App.exe"), "new-build");
        File.WriteAllBytes(Path.Combine(source, "new.bin"), new byte[] { 0, 255, 1, 254, 2 });
        if (nestedAssets)
        {
            var assets = Path.Combine(source, "Assets", "nested");
            Directory.CreateDirectory(assets);
            File.WriteAllBytes(Path.Combine(assets, "changed.dat"), new byte[] { 9, 8, 0, 7 });
        }

        if (!firstInstall)
        {
            Directory.CreateDirectory(destination);
            File.WriteAllText(Path.Combine(destination, "Dvinholm.App.exe"), "old-build");
            File.WriteAllText(Path.Combine(destination, "obsolete.dll"), "keep only on rollback");
            if (nestedAssets)
            {
                var assets = Path.Combine(destination, "Assets", "nested");
                Directory.CreateDirectory(assets);
                File.WriteAllBytes(Path.Combine(assets, "changed.dat"), new byte[] { 1, 2, 3, 4 });
            }
            // This is not hypothetical: Robocopy can skip distinct same-size
            // bytes when the source and destination timestamps also match.
            File.SetLastWriteTimeUtc(Path.Combine(source, "Dvinholm.App.exe"),
                File.GetLastWriteTimeUtc(Path.Combine(destination, "Dvinholm.App.exe")));
        }

        static Dictionary<string, byte[]> Snapshot(string folder) =>
            Directory.GetFiles(folder, "*", SearchOption.AllDirectories)
                .ToDictionary(path => Path.GetRelativePath(folder, path),
                    File.ReadAllBytes, StringComparer.OrdinalIgnoreCase);

        var oldFiles = firstInstall ? null : Snapshot(destination);
        var expectedNew = Snapshot(source);
        var unrelated = Path.Combine(root, "user-data.txt");
        File.WriteAllText(unrelated, "do not modify user data");

        try
        {
            var generated = LatestBuildUpdater.BuildReplacementScript(
                source, destination, -1, scriptPath, backup);
            const string watcherLaunch =
                "$guardProcess = Start-Process -FilePath (Join-Path $PSHOME 'powershell.exe') -ArgumentList $guardArguments -PassThru -WindowStyle Hidden";
            const string registryWrite =
                "New-ItemProperty -Path $runKey -Name $runName -Value $runCommand -PropertyType String -Force -ErrorAction Stop | Out-Null";
            const string registryRead =
                "Get-ItemProperty -Path $runKey -Name $runName -ErrorAction SilentlyContinue";
            const string registryDelete =
                "Remove-ItemProperty -Path $runKey -Name $runName -ErrorAction Stop";
            Assert.Contains(watcherLaunch, generated);
            Assert.Contains(registryWrite, generated);
            Assert.Contains(registryRead, generated);
            Assert.Contains(registryDelete, generated);

            // Disable ONLY the independent guard launch and HKCU Run I/O.
            // The helper's actual backup/replacement/rollback remains untouched.
            generated = generated
                .Replace(watcherLaunch,
                    "$guardProcess = [pscustomobject]@{ HasExited = $false }\n" +
                    "'ready' | Set-Content -LiteralPath $guardReady -Encoding ASCII",
                    StringComparison.Ordinal)
                .Replace(registryWrite, "# test only: no HKCU Run registration", StringComparison.Ordinal)
                .Replace(registryRead, "$false", StringComparison.Ordinal)
                .Replace(registryDelete, "# test only: no HKCU Run deletion", StringComparison.Ordinal);

            const string processMock = """
                function Start-Sleep { param([int]$Seconds, [int]$Milliseconds) }
                function Start-Process {
                    param([string]$FilePath, [string]$WorkingDirectory,
                          [string[]]$ArgumentList, [switch]$PassThru,
                          [string]$WindowStyle)
                    if ($FilePath -eq 'notepad.exe') {
                        return [pscustomobject]@{ HasExited = $false; ExitCode = 0 }
                    }
                    Add-Content -LiteralPath $env:DVINHOLM_TEST_LAUNCH_LOG -Value $FilePath
                    if ([IO.File]::ReadAllText($FilePath) -eq 'new-build' -and
                        $env:DVINHOLM_TEST_STARTUP_FAILS -eq '1') {
                        return [pscustomobject]@{ HasExited = $true; ExitCode = 17 }
                    }
                    return [pscustomobject]@{ HasExited = $false; ExitCode = 0 }
                }
                """;
            File.WriteAllText(scriptPath, processMock + Environment.NewLine + generated,
                new UTF8Encoding(false));

            var powerShell = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                "System32", "WindowsPowerShell", "v1.0", "powershell.exe");
            var start = new ProcessStartInfo
            {
                FileName = powerShell,
                WorkingDirectory = root,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            foreach (var argument in new[]
                     { "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass",
                       "-File", scriptPath })
                start.ArgumentList.Add(argument);
            start.Environment["LOCALAPPDATA"] = localAppData;
            start.Environment["DVINHOLM_TEST_LAUNCH_LOG"] = launchLog;
            start.Environment["DVINHOLM_TEST_STARTUP_FAILS"] = startupFails ? "1" : "0";

            using (var process = Process.Start(start)
                ?? throw new InvalidOperationException("Could not start isolated PowerShell helper."))
            {
                var stdout = process.StandardOutput.ReadToEndAsync();
                var stderr = process.StandardError.ReadToEndAsync();
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
                await process.WaitForExitAsync(timeout.Token);
                Assert.True(process.ExitCode == 0,
                    $"Helper exited {process.ExitCode}: {await stdout}\n{await stderr}");
            }

            var shouldHaveInstall = !startupFails || !firstInstall;
            Assert.Equal(shouldHaveInstall, Directory.Exists(destination));
            if (shouldHaveInstall)
            {
                var expected = startupFails ? oldFiles! : expectedNew;
                var actual = Snapshot(destination);
                Assert.Equal(
                    expected.Keys.OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToArray(),
                    actual.Keys.OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToArray());
                foreach (var (name, bytes) in expected)
                    Assert.Equal(bytes, actual[name]);
            }

            Assert.Equal("do not modify user data", File.ReadAllText(unrelated));
            Assert.False(Directory.Exists(state));
            Assert.False(Directory.Exists(backup));
            Assert.True(File.Exists(launchLog));
            var launchList = File.ReadAllLines(launchLog);
            Assert.Contains(Path.Combine(destination, "Dvinholm.App.exe"), launchList);
            if (startupFails && !firstInstall)
                Assert.Equal(2, launchList.Length);
            else
                Assert.Single(launchList);
        }
        finally
        {
            for (var attempt = 0; Directory.Exists(root); attempt++)
            {
                try { Directory.Delete(root, recursive: true); }
                catch (IOException) when (attempt < 29)
                {
                    await Task.Delay(100);
                }
                catch (UnauthorizedAccessException) when (attempt < 29)
                {
                    await Task.Delay(100);
                }
            }
        }
    }
}
