using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using Dvinholm.App;

namespace Dvinholm.App.Tests;

/// <summary>
/// Executes the PRODUCTION replacement helper against the real published
/// v0.2.3 and separately verified staged v0.2.4 Portable ZIPs. Both artifacts
/// are checksum-verified in the same workflow before this test runs. Only the watcher, HKCU Run I/O and GUI process launches
/// are mocked; the real helper performs backup, Robocopy /MIR and rollback.
/// </summary>
public sealed class Staged024UpdaterHelperIntegrationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RealPublishedPayload_UsesUpdaterHelperAndPreservesUserData(bool startupFails)
    {
        if (!OperatingSystem.IsWindows()) return;

        // The v0.2.4 staging workflow MUST provide both paths.
        // Ordinary CI workflows have no artifacts and do not run this integration fixture.
        var oldZip = Environment.GetEnvironmentVariable("DVINHOLM_PUBLISHED_V023_ZIP");
        var newZip = Environment.GetEnvironmentVariable("DVINHOLM_STAGED_V024_ZIP");
        if (oldZip is null && newZip is null) return;
        Assert.True(!string.IsNullOrWhiteSpace(oldZip) && File.Exists(oldZip),
            "The staging job must download the real published v0.2.3 ZIP.");
        Assert.True(!string.IsNullOrWhiteSpace(newZip) && File.Exists(newZip),
            "The staging job must provide the downloaded, checksum-verified v0.2.4 Portable ZIP.");

        var root = Path.Combine(Path.GetTempPath(), "Dvinholm-staged024-updater", Guid.NewGuid().ToString("N"));
        var oldPayload = Path.Combine(root, "published-v023");
        var newPayload = Path.Combine(root, "staged-v024");
        var installed = Path.Combine(root, "disposable-installed");
        var backup = Path.Combine(root, "backup");
        var script = Path.Combine(root, "replacement.ps1");
        var localData = Path.Combine(root, "private-localappdata");
        var launchLog = Path.Combine(root, "mock-launches.txt");
        Directory.CreateDirectory(oldPayload);
        Directory.CreateDirectory(newPayload);
        Directory.CreateDirectory(Path.Combine(localData, "Dvinholm"));

        static Dictionary<string, string> DigestTree(string path) =>
            Directory.GetFiles(path, "*", SearchOption.AllDirectories)
                .ToDictionary(
                    file => Path.GetRelativePath(path, file),
                    file => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file))),
                    StringComparer.OrdinalIgnoreCase);

        static void SameFiles(Dictionary<string, string> expected, Dictionary<string, string> actual)
        {
            Assert.Equal(expected.Count, actual.Count);
            foreach (var (relative, digest) in expected)
            {
                Assert.True(actual.TryGetValue(relative, out var found), "Missing " + relative);
                Assert.Equal(digest, found);
            }
        }

        try
        {
            // Gate 1: only the original published v0.2.3 and verified staged v0.2.4 ZIPs, never an installed Dvinholm tree.
            Assert.NotEqual(Path.GetFullPath(oldZip!), Path.GetFullPath(newZip!));
            ZipFile.ExtractToDirectory(oldZip!, oldPayload);
            ZipFile.ExtractToDirectory(newZip!, newPayload);

            // Gate 2: actual published 0.2.3 and staged 0.2.4 assemblies represent the expected upgrade.
            Assert.Equal("0.2.3", System.Reflection.AssemblyName.GetAssemblyName(
                Path.Combine(oldPayload, "Dvinholm.App.dll")).Version!.ToString(3));
            Assert.Equal("0.2.4", System.Reflection.AssemblyName.GetAssemblyName(
                Path.Combine(newPayload, "Dvinholm.App.dll")).Version!.ToString(3));

            // Gate 3: preserve an exact byte-level baseline of the old published payload.
            Directory.CreateDirectory(installed);
            foreach (var file in Directory.GetFiles(oldPayload, "*", SearchOption.AllDirectories))
            {
                var destination = Path.Combine(installed, Path.GetRelativePath(oldPayload, file));
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                File.Copy(file, destination);
            }
            File.WriteAllText(Path.Combine(installed, "obsolete-stable023-fixture.dll"),
                "Removed by the updater only inside this disposable tree.");
            var oldSnapshot = DigestTree(installed);
            var newSnapshot = DigestTree(newPayload);
            Assert.True(oldSnapshot.Count > 50);
            Assert.True(newSnapshot.Count > 50);

            // Gate 4: external per-user settings must survive both success and failure.
            var settings = Path.Combine(localData, "Dvinholm", "ecu-profiles.json");
            var baud = Path.Combine(localData, "Dvinholm", "can-baud-rate.txt");
            File.WriteAllText(settings, "{\"id\":\"unchanged-offline-user-profile\"}");
            File.WriteAllText(baud, "125000");
            var settingsDigest = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(settings)));
            var baudDigest = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(baud)));

            var generated = LatestBuildUpdater.BuildReplacementScript(
                newPayload, installed, -1, script, backup);
            const string guard = "$guardProcess = Start-Process -FilePath (Join-Path $PSHOME 'powershell.exe') -ArgumentList $guardArguments -PassThru -WindowStyle Hidden";
            const string addRun = "New-ItemProperty -Path $runKey -Name $runName -Value $runCommand -PropertyType String -Force -ErrorAction Stop | Out-Null";
            const string readRun = "Get-ItemProperty -Path $runKey -Name $runName -ErrorAction SilentlyContinue";
            const string deleteRun = "Remove-ItemProperty -Path $runKey -Name $runName -ErrorAction Stop";
            foreach (var required in new[] { guard, addRun, readRun, deleteRun })
                Assert.Contains(required, generated);

            generated = generated.Replace(guard,
                    "$guardProcess = [pscustomobject]@{ HasExited = $false }\n" +
                    "'ready' | Set-Content -LiteralPath $guardReady -Encoding ASCII",
                    StringComparison.Ordinal)
                .Replace(addRun, "# isolated test: no HKCU Run registration", StringComparison.Ordinal)
                .Replace(readRun, "$false", StringComparison.Ordinal)
                .Replace(deleteRun, "# isolated test: no HKCU Run removal", StringComparison.Ordinal);

            const string processMock = """
                function Start-Sleep { param([int]$Seconds, [int]$Milliseconds) }
                function Start-Process {
                    param([string]$FilePath, [string]$WorkingDirectory,
                          [string[]]$ArgumentList, [switch]$PassThru,
                          [string]$WindowStyle)
                    if ($FilePath -eq 'notepad.exe') {
                        return [pscustomobject]@{ HasExited = $false; ExitCode = 0 }
                    }
                    if ([IO.Path]::GetFullPath($FilePath) -ne
                        [IO.Path]::GetFullPath((Join-Path $env:DVINHOLM_TEST_DEST 'Dvinholm.App.exe'))) {
                        throw 'Attempt to launch outside disposable test destination.'
                    }
                    Add-Content -LiteralPath $env:DVINHOLM_TEST_LOG -Value $FilePath
                    $marker = $env:DVINHOLM_TEST_LOG + '.failed-once'
                    if ($env:DVINHOLM_TEST_FAIL -eq '1' -and
                        -not (Test-Path -LiteralPath $marker)) {
                        'failed' | Set-Content -LiteralPath $marker
                        return [pscustomobject]@{ HasExited = $true; ExitCode = 17 }
                    }
                    return [pscustomobject]@{ HasExited = $false; ExitCode = 0 }
                }
                """;
            File.WriteAllText(script, processMock + Environment.NewLine + generated,
                new UTF8Encoding(false));

            var powershell = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                "System32", "WindowsPowerShell", "v1.0", "powershell.exe");
            var info = new ProcessStartInfo
            {
                FileName = powershell,
                WorkingDirectory = root,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            foreach (var arg in new[] { "-NoProfile", "-NonInteractive",
                     "-ExecutionPolicy", "Bypass", "-File", script })
                info.ArgumentList.Add(arg);
            info.Environment["LOCALAPPDATA"] = localData;
            info.Environment["DVINHOLM_TEST_DEST"] = installed;
            info.Environment["DVINHOLM_TEST_LOG"] = launchLog;
            info.Environment["DVINHOLM_TEST_FAIL"] = startupFails ? "1" : "0";

            using (var process = Process.Start(info)
                ?? throw new InvalidOperationException("Isolated update helper did not start."))
            {
                var stdout = process.StandardOutput.ReadToEndAsync();
                var stderr = process.StandardError.ReadToEndAsync();
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(120));
                await process.WaitForExitAsync(timeout.Token);
                Assert.True(process.ExitCode == 0,
                    $"Updater helper exited {process.ExitCode}: {await stdout}\n{await stderr}");
            }

            // Gate 5: successful replacement matches the exact published 0.2.3 tree.
            // Gate 6: simulated new-app failure restores the entire published 0.2.3 tree.
            SameFiles(startupFails ? oldSnapshot : newSnapshot, DigestTree(installed));

            // Gate 7: no stale files after success, but stale old bytes survive rollback.
            Assert.Equal(startupFails,
                File.Exists(Path.Combine(installed, "obsolete-stable023-fixture.dll")));
            Assert.False(Directory.Exists(backup));
            Assert.False(Directory.Exists(backup + ".state"));

            // Gate 8: both stored user settings remain byte-for-byte identical.
            Assert.Equal(settingsDigest, Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(settings))));
            Assert.Equal(baudDigest, Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(baud))));
            Assert.Equal(startupFails ? 2 : 1, File.ReadAllLines(launchLog).Length);
        }
        finally
        {
            for (var attempt = 0; Directory.Exists(root); attempt++)
            {
                try { Directory.Delete(root, true); }
                catch (IOException) when (attempt < 29) { await Task.Delay(100); }
                catch (UnauthorizedAccessException) when (attempt < 29) { await Task.Delay(100); }
            }
        }
    }
}
