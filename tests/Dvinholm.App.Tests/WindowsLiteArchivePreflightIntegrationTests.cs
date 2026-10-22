using System.Diagnostics;
using System.IO.Compression;
using System.Text;

namespace Dvinholm.App.Tests;

/// <summary>
/// Eight synthetic archive tests coupling the Lite extraction gate to
/// simulated prerequisite checks. No WinUI or installed packages touched.
/// </summary>
public sealed class WindowsLiteArchivePreflightIntegrationTests
{
    [Theory]
    [InlineData("lite-valid-core", true)]
    [InlineData("lite-valid-desktop", true)]
    [InlineData("lite-dotnet9", false)]
    [InlineData("lite-empty-framework", false)]
    [InlineData("lite-bundled-coreclr", false)]
    [InlineData("lite-x64-apphost", false)]
    [InlineData("lite-malformed-json", false)]
    [InlineData("portable-reject-simulation", false)]
    public async Task ExtractedLite_IsCheckedWithExplicitSyntheticHost(
        string scenario, bool expected)
    {
        if (!OperatingSystem.IsWindows()) return;
        var root = Path.Combine(Path.GetTempPath(), "Dvinholm-lite-integration",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var zipPath = Path.Combine(root, "app.zip");
        try
        {
            using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
            {
                static void Add(ZipArchive z, string path, byte[] payload)
                {
                    var entry = z.CreateEntry(path, CompressionLevel.Optimal);
                    using var output = entry.Open();
                    output.Write(payload);
                }
                var executable = new byte[256];
                executable[0] = 0x4D; executable[1] = 0x5A;
                BitConverter.GetBytes(128).CopyTo(executable, 0x3C);
                executable[128] = 0x50; executable[129] = 0x45;
                BitConverter.GetBytes(scenario == "lite-x64-apphost"
                    ? (ushort)0x8664 : (ushort)0x014C).CopyTo(executable, 132);
                Add(zip, "Dvinholm.App.exe", executable);
                var data = Encoding.UTF8.GetBytes("synthetic");
                Add(zip, "Dvinholm.App.dll", data);
                Add(zip, "Dvinholm.App.deps.json", Encoding.UTF8.GetBytes("{}"));
                Add(zip, "Assets/Dvinholm.ico", data);

                var manifest = scenario switch
                {
                    "lite-valid-desktop" =>
                        """{"runtimeOptions":{"frameworks":[{"name":"Microsoft.NETCore.App","version":"10.0.0"},{"name":"Microsoft.WindowsDesktop.App","version":"10.0.0"}]}}""",
                    "lite-dotnet9" =>
                        """{"runtimeOptions":{"framework":{"name":"Microsoft.NETCore.App","version":"9.0.0"}}}""",
                    "lite-empty-framework" => """{"runtimeOptions":{}}""",
                    "lite-malformed-json" => "{",
                    _ => """{"runtimeOptions":{"framework":{"name":"Microsoft.NETCore.App","version":"10.0.0"}}}"""
                };
                Add(zip, "Dvinholm.App.runtimeconfig.json", Encoding.UTF8.GetBytes(manifest));
                if (scenario is "lite-bundled-coreclr" or "portable-reject-simulation")
                    Add(zip, "coreclr.dll", data);
                if (scenario == "portable-reject-simulation")
                    Add(zip, "hostfxr.dll", data);
            }
            var flavor = scenario == "portable-reject-simulation" ? "Portable" : "Lite";
            var shell = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                "System32", "WindowsPowerShell", "v1.0", "powershell.exe");
            var launch = new ProcessStartInfo
            {
                FileName = shell, WorkingDirectory = root, UseShellExecute = false,
                CreateNoWindow = true, RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            foreach (var arg in new[] {
                "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass",
                "-File", FindScript(), "-Flavor", flavor, "-ZipPath", zipPath,
                "-SimulatedLiteHost" })
                launch.ArgumentList.Add(arg);
            using var process = Process.Start(launch)
                ?? throw new InvalidOperationException("Could not invoke Lite ZIP inspector.");
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            await process.WaitForExitAsync(timeout.Token);
            var output = await stdout + await stderr;
            if (expected)
            {
                Assert.True(process.ExitCode == 0, output);
                Assert.Contains("SIMULATED INPUT", output);
                Assert.Contains("real Lite ZIP metadata against SIMULATED", output);
                Assert.Contains("Verified extracted Lite x86 PE", output);
            }
            else
                Assert.True(process.ExitCode != 0,
                    $"Invalid synthetic ZIP {scenario} unexpectedly passed: {output}");
        }
        finally
        {
            await WindowsTestFixtureCleanup.RemoveBestEffortAsync(root);
        }
    }

    private static string FindScript()
    {
        for (DirectoryInfo? dir = new(AppContext.BaseDirectory);
             dir is not null; dir = dir.Parent)
        {
            var script = Path.Combine(dir.FullName, "eng",
                "Test-WindowsDistributionExtraction.ps1");
            if (File.Exists(script)) return script;
        }
        throw new FileNotFoundException("Lite ZIP smoke script not found.");
    }
}
