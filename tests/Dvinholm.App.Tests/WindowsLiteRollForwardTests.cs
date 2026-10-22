using System.Diagnostics;
using System.Text;

namespace Dvinholm.App.Tests;

/// <summary>
/// Eight deterministic .NET 10 roll-forward fixture checks. The Windows host
/// is not inspected and WinUI is not launched.
/// </summary>
public sealed class WindowsLiteRollForwardTests
{
    [Theory]
    [InlineData("default-patch", true, "")]
    [InlineData("minor-higher-minor", true, "")]
    [InlineData("latest-patch", true, "")]
    [InlineData("latest-patch-wrong-minor", false, "Missing .NET 10 x86 shared runtime")]
    [InlineData("disable-exact", true, "")]
    [InlineData("disable-patch-mismatch", false, "Missing .NET 10 x86 shared runtime")]
    [InlineData("unknown-policy", false, "cannot certify .NET rollForward")]
    [InlineData("patches-disabled", false, "cannot certify runtimeconfig applyPatches=false")]
    public async Task Preflight_RespectsDeclaredRollForwardPolicy(
        string scenario, bool shouldPass, string expectedError)
    {
        if (!OperatingSystem.IsWindows()) return;
        var dir = Path.Combine(Path.GetTempPath(), "Dvinholm-lite-rollforward",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var rollForward = scenario switch
            {
                "minor-higher-minor" => "\"rollForward\":\"Minor\",",
                "latest-patch" or "latest-patch-wrong-minor" =>
                    "\"rollForward\":\"LatestPatch\",",
                "disable-exact" or "disable-patch-mismatch" =>
                    "\"rollForward\":\"Disable\",",
                "unknown-policy" => "\"rollForward\":\"UnlistedPolicy\",",
                _ => ""
            };
            var applyPatches = scenario == "patches-disabled" ?
                "\"applyPatches\":false," : "";
            File.WriteAllText(Path.Combine(dir, "Dvinholm.App.runtimeconfig.json"),
                "{\"runtimeOptions\":{" + rollForward + applyPatches +
                "\"framework\":{\"name\":\"Microsoft.NETCore.App\",\"version\":\"10.0.0\"}}}");

            var installed = scenario switch
            {
                "minor-higher-minor" or "latest-patch-wrong-minor" =>
                    "10.1.0",
                "disable-exact" => "10.0.0",
                _ => "10.0.2"
            };
            var runtimePath = Path.Combine(dir, "runtime-list.txt");
            File.WriteAllText(runtimePath,
                $"Microsoft.NETCore.App {installed} [C:\\dotnet-x86\\shared\\Microsoft.NETCore.App]\n" +
                $"Microsoft.WindowsDesktop.App 10.0.2 [C:\\dotnet-x86\\shared\\Microsoft.WindowsDesktop.App]\n",
                Encoding.UTF8);
            var sdkPath = Path.Combine(dir, "sdk-packages.txt");
            File.WriteAllText(sdkPath,
                "Microsoft.WindowsAppRuntime.2|X86|2.5.1.0\n" +
                "MicrosoftCorporationII.WinAppRuntime.Main.2|X64|2.5.1.0\n" +
                "MicrosoftCorporationII.WinAppRuntime.Singleton|X64|8002.5.1.0\n" +
                "Microsoft.WinAppRuntime.DDLM.2.5.1.0-x8|X86|2.5.1.0\n",
                Encoding.UTF8);
            var vcPath = Path.Combine(dir, "vc.txt");
            File.WriteAllText(vcPath, "1|14|v14.40.33816.0\n");

            var powershell = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                "System32", "WindowsPowerShell", "v1.0", "powershell.exe");
            var p = new ProcessStartInfo
            {
                FileName = powershell, WorkingDirectory = dir,
                UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, RedirectStandardError = true
            };
            foreach (var arg in new[] {
                "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass",
                "-File", FindScript(), "-LiteDirectory", dir,
                "-WindowsAppSdkLine", "2.5",
                "-RuntimeListFile", runtimePath, "-AppSdkPackageListFile", sdkPath,
                "-VcRedistStateFile", vcPath })
                p.ArgumentList.Add(arg);
            using var process = Process.Start(p)
                ?? throw new InvalidOperationException("Lite roll-forward probe failed to start.");
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            await process.WaitForExitAsync(timeout.Token);
            var output = await stdout + await stderr;
            if (shouldPass)
            {
                Assert.True(process.ExitCode == 0, output);
                Assert.Contains("SIMULATED INPUT", output);
                Assert.Contains("prerequisite inspection passed", output);
            }
            else
            {
                Assert.NotEqual(0, process.ExitCode);
                Assert.Contains(expectedError, output);
            }
        }
        finally
        {
            await WindowsTestFixtureCleanup.RemoveBestEffortAsync(dir);
        }
    }

    private static string FindScript()
    {
        for (DirectoryInfo? directory = new(AppContext.BaseDirectory);
             directory is not null; directory = directory.Parent)
        {
            var path = Path.Combine(directory.FullName,
                "eng", "Test-LitePrerequisites.ps1");
            if (File.Exists(path)) return path;
        }
        throw new FileNotFoundException("Missing Lite prerequisite PowerShell script.");
    }
}
