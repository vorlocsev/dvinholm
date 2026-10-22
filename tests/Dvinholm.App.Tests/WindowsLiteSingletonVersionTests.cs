using System.Diagnostics;
using System.Text;

namespace Dvinholm.App.Tests;

/// <summary>
/// Eight simulated Windows x64 Singleton-registration scenarios for Lite.
/// Does not inspect packages on the CI runner, install software or launch WinUI.
/// </summary>
public sealed class WindowsLiteSingletonVersionTests
{
    [Theory]
    [InlineData("valid-required", true)]
    [InlineData("valid-newer-sdk", true)]
    [InlineData("missing-singleton", false)]
    [InlineData("outdated-minor", false)]
    [InlineData("outdated-major", false)]
    [InlineData("wrong-architecture", false)]
    [InlineData("malformed-version", false)]
    [InlineData("preview-only", false)]
    public async Task LitePreflight_RequiresCompatibleSingletonRegistration(
        string scenario, bool expectedSuccess)
    {
        if (!OperatingSystem.IsWindows()) return;
        var directory = Path.Combine(Path.GetTempPath(),
            "Dvinholm-lite-singleton", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            File.WriteAllText(
                Path.Combine(directory, "Dvinholm.App.runtimeconfig.json"),
                """{"runtimeOptions":{"framework":{"name":"Microsoft.NETCore.App","version":"10.0.0"}}}""");

            var runtimeList = Path.Combine(directory, "runtimes.txt");
            File.WriteAllText(runtimeList,
                "Microsoft.NETCore.App 10.0.2 [C:\\dotnet-x86\\shared\\Microsoft.NETCore.App]\n" +
                "Microsoft.WindowsDesktop.App 10.0.2 [C:\\dotnet-x86\\shared\\Microsoft.WindowsDesktop.App]\n",
                Encoding.UTF8);

            var singletonName = scenario == "preview-only"
                ? "MicrosoftCorporationII.WinAppRuntime.Singleton-p3"
                : "MicrosoftCorporationII.WinAppRuntime.Singleton";
            var singletonArch = scenario == "wrong-architecture" ? "X86" : "X64";
            var singletonVersion = scenario switch
            {
                "valid-newer-sdk" => "8003.0.0.0",
                "outdated-minor" => "8002.4.99.0",
                "outdated-major" => "8001.99.99.0",
                "malformed-version" => "not-a-version",
                _ => "8002.5.1.0"
            };
            var packages = new List<string>
            {
                "Microsoft.WindowsAppRuntime.2|X86|2.5.1.0",
                "MicrosoftCorporationII.WinAppRuntime.Main.2|X64|2.5.1.0",
                "Microsoft.WinAppRuntime.DDLM.2.5.1.0-x8|X86|2.5.1.0"
            };
            if (scenario != "missing-singleton")
                packages.Add($"{singletonName}|{singletonArch}|{singletonVersion}");
            var sdkList = Path.Combine(directory, "packages.txt");
            File.WriteAllLines(sdkList, packages, Encoding.UTF8);
            var vcState = Path.Combine(directory, "vc-state.txt");
            File.WriteAllText(vcState, "1|14|v14.40.33816.0\n", Encoding.UTF8);

            var shell = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                "System32", "WindowsPowerShell", "v1.0", "powershell.exe");
            var start = new ProcessStartInfo
            {
                FileName = shell, WorkingDirectory = directory,
                UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, RedirectStandardError = true
            };
            foreach (var arg in new[] {
                "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass",
                "-File", FindScript(), "-LiteDirectory", directory,
                "-WindowsAppSdkLine", "2.5",
                "-RuntimeListFile", runtimeList,
                "-AppSdkPackageListFile", sdkList,
                "-VcRedistStateFile", vcState,
                "-FixtureHostArchitecture", "X64"
            })
                start.ArgumentList.Add(arg);

            using var process = Process.Start(start)
                ?? throw new InvalidOperationException("Cannot start Lite Singleton inspection.");
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            await process.WaitForExitAsync(timeout.Token);
            var output = await stdout + await stderr;
            if (expectedSuccess)
            {
                Assert.True(process.ExitCode == 0, output);
                Assert.Contains("SIMULATED INPUT", output);
                Assert.Contains("prerequisite inspection passed", output);
            }
            else
            {
                Assert.NotEqual(0, process.ExitCode);
                Assert.Contains("Singleton", output);
            }
        }
        finally
        {
            await WindowsTestFixtureCleanup.RemoveBestEffortAsync(directory);
        }
    }

    private static string FindScript()
    {
        for (DirectoryInfo? dir = new(AppContext.BaseDirectory);
             dir is not null; dir = dir.Parent)
        {
            var path = Path.Combine(dir.FullName, "eng", "Test-LitePrerequisites.ps1");
            if (File.Exists(path)) return path;
        }
        throw new FileNotFoundException("Lite prerequisite PowerShell script is absent.");
    }
}
