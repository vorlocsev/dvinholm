using System.Diagnostics;
using System.Text;

namespace Dvinholm.App.Tests;

/// <summary>
/// Eight isolated Windows PowerShell 5.1 checks of the exact two-file
/// Development distribution checksum manifest. Uses harmless fixture bytes,
/// not published builds, and does not start WinUI or access any real adapter.
/// </summary>
public sealed class WindowsDistributionChecksumTests
{
    [Theory]
    [InlineData("valid-both", true, "")]
    [InlineData("missing-lite", false, "Missing distribution ZIP")]
    [InlineData("damaged-portable", false, "SHA-256 mismatch")]
    [InlineData("damaged-lite", false, "SHA-256 mismatch")]
    [InlineData("missing-manifest", false, "Missing SHA256SUMS.txt")]
    [InlineData("duplicate-entry", false, "Duplicate checksum entry")]
    [InlineData("invalid-digest", false, "Malformed SHA256SUMS.txt")]
    [InlineData("path-traversal", false, "Unexpected checksum filename")]
    public async Task ChecksumManifest_RejectsMissingAlteredOrUnsafeAssets(
        string scenario, bool valid, string expectedError)
    {
        if (!OperatingSystem.IsWindows()) return;

        var root = Path.Combine(Path.GetTempPath(), "Dvinholm-checksums",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var portable = Path.Combine(root, "Dvinholm-Windows-x86.zip");
        var lite = Path.Combine(root, "Dvinholm-Windows-x86-Lite.zip");
        var manifest = Path.Combine(root, "SHA256SUMS.txt");
        try
        {
            File.WriteAllBytes(portable, [1, 2, 3, 4]);
            File.WriteAllBytes(lite, [5, 6, 7]);

            var script = FindScript();
            if (scenario == "missing-lite")
            {
                File.Delete(lite);
                var missing = await RunPowerShellAsync(script, root, generate: true);
                Assert.NotEqual(0, missing.ExitCode);
                Assert.Contains(expectedError, missing.Output);
                Assert.False(File.Exists(manifest));
                return;
            }

            var created = await RunPowerShellAsync(script, root, generate: true);
            Assert.True(created.ExitCode == 0, created.Output);
            Assert.True(File.Exists(manifest));

            switch (scenario)
            {
                case "damaged-portable":
                    File.AppendAllText(portable, "tamper");
                    break;
                case "damaged-lite":
                    File.AppendAllText(lite, "tamper");
                    break;
                case "missing-manifest":
                    File.Delete(manifest);
                    break;
                case "duplicate-entry":
                {
                    var lines = File.ReadAllLines(manifest);
                    lines[1] = lines[0];
                    File.WriteAllLines(manifest, lines, Encoding.ASCII);
                    break;
                }
                case "invalid-digest":
                {
                    var lines = File.ReadAllLines(manifest);
                    lines[0] = "z" + lines[0][1..];
                    File.WriteAllLines(manifest, lines, Encoding.ASCII);
                    break;
                }
                case "path-traversal":
                {
                    var lines = File.ReadAllLines(manifest);
                    lines[1] = lines[1].Replace("Dvinholm-Windows-x86-Lite.zip",
                        "../outside.zip", StringComparison.Ordinal);
                    File.WriteAllLines(manifest, lines, Encoding.ASCII);
                    break;
                }
            }

            var verified = await RunPowerShellAsync(script, root, generate: false);
            if (valid)
            {
                Assert.Equal(0, verified.ExitCode);
                Assert.Contains("SHA-256 checksums verified", verified.Output);
                Assert.Equal(2, File.ReadAllLines(manifest).Length);
            }
            else
            {
                Assert.NotEqual(0, verified.ExitCode);
                Assert.Contains(expectedError, verified.Output);
            }
        }
        finally
        {
            await WindowsTestFixtureCleanup.RemoveBestEffortAsync(root);
        }
    }

    private static async Task<(int ExitCode, string Output)> RunPowerShellAsync(
        string script, string directory, bool generate)
    {
        var powershell = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            "System32", "WindowsPowerShell", "v1.0", "powershell.exe");
        var start = new ProcessStartInfo
        {
            FileName = powershell, WorkingDirectory = directory,
            UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true
        };
        foreach (var arg in new[]
            { "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass",
              "-File", script, "-Directory", directory })
            start.ArgumentList.Add(arg);
        if (generate) start.ArgumentList.Add("-Generate");

        using var process = Process.Start(start)
            ?? throw new InvalidOperationException("PowerShell checksum checker did not start.");
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        await process.WaitForExitAsync(timeout.Token);
        return (process.ExitCode, await stdout + await stderr);
    }

    private static string FindScript()
    {
        for (DirectoryInfo? dir = new(AppContext.BaseDirectory);
             dir is not null; dir = dir.Parent)
        {
            var path = Path.Combine(dir.FullName, "eng",
                "Test-WindowsDistributionChecksums.ps1");
            if (File.Exists(path)) return path;
        }
        throw new FileNotFoundException("Distribution checksum PowerShell verifier was not found.");
    }
}
