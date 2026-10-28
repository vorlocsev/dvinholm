using System.Diagnostics;
using System.IO.Compression;
using System.Text;

namespace Dvinholm.App.Tests;

/// <summary>
/// Eight Windows PowerShell 5.1 checks for the actual Lite self-check bundle
/// archive gate. Only synthetic harmless ZIPs; never starts WinUI.
/// </summary>
public sealed class WindowsLiteSelfCheckBundleTests
{
    [Theory]
    [InlineData("valid-lite", "Lite", true)]
    [InlineData("valid-portable", "Portable", true)]
    [InlineData("missing-launcher", "Lite", false)]
    [InlineData("missing-script", "Lite", false)]
    [InlineData("modified-launcher", "Lite", false)]
    [InlineData("modified-script", "Lite", false)]
    [InlineData("nested-script", "Lite", false)]
    [InlineData("portable-with-launcher", "Portable", false)]
    public async Task PackageGate_RequiresExactCheckerFiles(
        string scenario, string flavor, bool shouldPass)
    {
        if (!OperatingSystem.IsWindows()) return;
        var root = Path.Combine(Path.GetTempPath(), "Dvinholm-lite-selfcheck",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var archive = Path.Combine(root, "test.zip");
            using (var zip = ZipFile.Open(archive, ZipArchiveMode.Create))
            {
                static void Add(ZipArchive z, string name, byte[] data)
                {
                    var entry = z.CreateEntry(name, CompressionLevel.Optimal);
                    using var stream = entry.Open();
                    stream.Write(data);
                }
                if (scenario is not "missing-launcher" and not "valid-portable")
                {
                    var bytes = File.ReadAllBytes(FindSource("Check-Lite-Prerequisites.cmd"));
                    if (scenario == "modified-launcher")
                        bytes = Encoding.UTF8.GetBytes("@echo off\r\nexit /b 0\r\n");
                    Add(zip, "Check-Lite-Prerequisites.cmd", bytes);
                }
                // A valid Lite distribution now carries two additional
                // byte-for-byte source-matched setup entrypoints. Negative
                // scenarios below remain invalid for their original reason.
                if (flavor == "Lite")
                {
                    Add(zip, "Manage-Lite-Dependencies.ps1",
                        File.ReadAllBytes(FindSource("Manage-Lite-Dependencies.ps1")));
                    Add(zip, "Start-Dvinholm-Lite.cmd",
                        File.ReadAllBytes(FindSource("Start-Dvinholm-Lite.cmd")));
                }
                if (scenario is not "missing-script" and not "valid-portable"
                    and not "portable-with-launcher")
                {
                    var bytes = File.ReadAllBytes(FindSource("Test-LitePrerequisites.ps1"));
                    if (scenario == "modified-script")
                        bytes = Encoding.UTF8.GetBytes("Write-Host 'not the checker'\r\n");
                    Add(zip, scenario == "nested-script"
                        ? "Tools/Check-Lite-Prerequisites.ps1"
                        : "Check-Lite-Prerequisites.ps1", bytes);
                }
            }

            var ps = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                "System32", "WindowsPowerShell", "v1.0", "powershell.exe");
            var start = new ProcessStartInfo
            {
                FileName = ps, WorkingDirectory = root, UseShellExecute = false,
                CreateNoWindow = true, RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            foreach (var arg in new[] {
                "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass",
                "-File", FindSource("Test-LiteBundledPreflight.ps1"),
                "-Flavor", flavor, "-ZipPath", archive
            }) start.ArgumentList.Add(arg);
            using var process = Process.Start(start)
                ?? throw new InvalidOperationException("Could not start Lite ZIP checker gate.");
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            await process.WaitForExitAsync(timeout.Token);
            var output = await stdout + await stderr;
            if (shouldPass)
            {
                Assert.Equal(0, process.ExitCode);
                Assert.Contains("Verified ", output);
            }
            else
                Assert.NotEqual(0, process.ExitCode);
        }
        finally
        {
            await WindowsTestFixtureCleanup.RemoveBestEffortAsync(root);
        }
    }

    private static string FindSource(string name)
    {
        for (DirectoryInfo? dir = new(AppContext.BaseDirectory);
             dir is not null; dir = dir.Parent)
        {
            var path = Path.Combine(dir.FullName, "eng", name);
            if (File.Exists(path)) return path;
        }
        throw new FileNotFoundException("Missing Lite bundle source: " + name);
    }
}
