using System.Diagnostics;
using System.Text;

namespace Dvinholm.App.Tests;

/// <summary>
/// Eight synthetic Windows PowerShell 5.1 host prerequisite checks.
/// Neither installed packages nor WinUI/J2534 are touched.
/// </summary>
public sealed class WindowsLitePrerequisiteTests
{
    [Theory]
    [InlineData("valid", true)]
    [InlineData("missing-manifest", false)]
    [InlineData("malformed-manifest", false)]
    [InlineData("bundled-coreclr", false)]
    [InlineData("dotnet9-only", false)]
    [InlineData("missing-desktop-runtime", false)]
    [InlineData("wrong-appsdk-line", false)]
    [InlineData("appsdk-x64-only", false)]
    public async Task Inspection_RejectsWrongRuntimeOrArchitecture(string scenario, bool success)
    {
        if (!OperatingSystem.IsWindows())
            return;

        var folder = Path.Combine(Path.GetTempPath(), "Dvinholm-lite-preflight",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var manifest = Path.Combine(folder, "Dvinholm.App.runtimeconfig.json");
            File.WriteAllText(manifest,
                """{"runtimeOptions":{"framework":{"name":"Microsoft.NETCore.App","version":"10.0.0"}}}""");
            if (scenario == "missing-manifest") File.Delete(manifest);
            if (scenario == "malformed-manifest") File.WriteAllText(manifest, "{");
            if (scenario == "bundled-coreclr")
                File.WriteAllBytes(Path.Combine(folder, "coreclr.dll"), [1]);

            var installed =
                "Microsoft.NETCore.App 10.0.2 [C:\\dotnet-x86\\shared\\Microsoft.NETCore.App]\n" +
                "Microsoft.WindowsDesktop.App 10.0.2 [C:\\dotnet-x86\\shared\\Microsoft.WindowsDesktop.App]\n";
            if (scenario == "dotnet9-only") installed = installed.Replace("10.0.2", "9.0.10");
            if (scenario == "missing-desktop-runtime")
                installed = installed.Split('\n')[0] + "\n";
            var runtimes = Path.Combine(folder, "runtimes.txt");
            File.WriteAllText(runtimes, installed, Encoding.UTF8);

            var package =
                "Microsoft.WindowsAppRuntime.2|X86|2.5.1.0\n" +
                "MicrosoftCorporationII.WinAppRuntime.Main.2|X64|2.5.1.0\n" +
                "MicrosoftCorporationII.WinAppRuntime.Singleton|X64|8002.5.1.0\n" +
                "Microsoft.WinAppRuntime.DDLM.2.5.1.0-x8|X86|2.5.1.0\n";
            if (scenario == "wrong-appsdk-line")
                package = package.Replace("Microsoft.WindowsAppRuntime.2|X86|2.5.1.0",
                    "Microsoft.WindowsAppRuntime.2|X86|2.4.0.0");
            if (scenario == "appsdk-x64-only")
                package = package.Replace("Microsoft.WindowsAppRuntime.2|X86|2.5.1.0",
                    "Microsoft.WindowsAppRuntime.2|X64|2.5.1.0");
            var packages = Path.Combine(folder, "packages.txt");
            File.WriteAllText(packages, package, Encoding.UTF8);
            var vcState = Path.Combine(folder, "vc-x86.txt");
            File.WriteAllText(vcState, "1|14|v14.40.33816.0\n", Encoding.UTF8);

            var shell = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                "System32", "WindowsPowerShell", "v1.0", "powershell.exe");
            var start = new ProcessStartInfo
            {
                FileName = shell,
                WorkingDirectory = folder,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            foreach (var arg in new[] {
                "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass",
                "-File", FindScript(), "-LiteDirectory", folder,
                "-WindowsAppSdkLine", "2.5",
                "-RuntimeListFile", runtimes, "-AppSdkPackageListFile", packages,
                "-VcRedistStateFile", vcState })
                start.ArgumentList.Add(arg);

            using var process = Process.Start(start)
                ?? throw new InvalidOperationException("Cannot launch PowerShell prereq checker.");
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            await process.WaitForExitAsync(timeout.Token);
            var output = await stdout + await stderr;
            if (success)
            {
                Assert.True(process.ExitCode == 0, output);
                Assert.Contains("SIMULATED INPUT", output);
                Assert.Contains("prerequisite inspection passed", output);
            }
            else Assert.True(process.ExitCode != 0, $"Invalid fixture {scenario} passed: {output}");
        }
        finally
        {
            await WindowsTestFixtureCleanup.RemoveBestEffortAsync(folder);
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
        throw new FileNotFoundException("Lite prerequisite script was not found.");
    }
}