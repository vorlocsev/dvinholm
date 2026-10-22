using System.Diagnostics;
using System.Text;

namespace Dvinholm.App.Tests;

/// <summary>
/// Eight synthetic PowerShell 5.1 checks of registered Windows App SDK
/// framework/main/singleton/DDLM packages. No installed packages are changed.
/// </summary>
public sealed class WindowsLiteAppSdkInventoryTests
{
    [Theory]
    [InlineData("valid-x64", true)]
    [InlineData("valid-x86", true)]
    [InlineData("missing-framework", false)]
    [InlineData("missing-main", false)]
    [InlineData("missing-singleton", false)]
    [InlineData("missing-ddlm", false)]
    [InlineData("wrong-ddlm-architecture", false)]
    [InlineData("outdated-main", false)]
    public async Task LitePreflight_RequiresCompleteSdkInventory(string scenario, bool expected)
    {
        if (!OperatingSystem.IsWindows()) return;

        var root = Path.Combine(Path.GetTempPath(), "Dvinholm-sdk-inventory",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            File.WriteAllText(Path.Combine(root, "Dvinholm.App.runtimeconfig.json"),
                """{"runtimeOptions":{"framework":{"name":"Microsoft.NETCore.App","version":"10.0.0"}}}""");
            var runtimeFile = Path.Combine(root, "runtimes.txt");
            File.WriteAllText(runtimeFile,
                "Microsoft.NETCore.App 10.0.2 [C:\\dotnet-x86\\shared\\Microsoft.NETCore.App]\n" +
                "Microsoft.WindowsDesktop.App 10.0.2 [C:\\dotnet-x86\\shared\\Microsoft.WindowsDesktop.App]\n",
                Encoding.UTF8);
            var hostArch = scenario == "valid-x86" ? "X86" : "X64";
            var packages = new List<string>
            {
                "Microsoft.WindowsAppRuntime.2|X86|2.5.1.0",
                $"MicrosoftCorporationII.WinAppRuntime.Main.2|{hostArch}|2.5.1.0",
                $"MicrosoftCorporationII.WinAppRuntime.Singleton|{hostArch}|8002.5.1.0",
                "Microsoft.WinAppRuntime.DDLM.2.5.1.0-x8|X86|2.5.1.0"
            };
            switch (scenario)
            {
                case "missing-framework": packages.RemoveAt(0); break;
                case "missing-main": packages.RemoveAt(1); break;
                case "missing-singleton": packages.RemoveAt(2); break;
                case "missing-ddlm": packages.RemoveAt(3); break;
                case "wrong-ddlm-architecture":
                    packages[3] = "Microsoft.WinAppRuntime.DDLM.2.5.1.0-x6|X64|2.5.1.0";
                    break;
                case "outdated-main":
                    packages[1] = $"MicrosoftCorporationII.WinAppRuntime.Main.2|{hostArch}|2.4.0.0";
                    break;
            }
            var packageFile = Path.Combine(root, "sdk-packages.txt");
            File.WriteAllLines(packageFile, packages, Encoding.UTF8);
            var vcFile = Path.Combine(root, "vc-x86.txt");
            File.WriteAllText(vcFile, "1|14|v14.40.33816.0\n", Encoding.UTF8);

            var shell = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                "System32", "WindowsPowerShell", "v1.0", "powershell.exe");
            var info = new ProcessStartInfo
            {
                FileName = shell,
                WorkingDirectory = root,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            foreach (var arg in new[] {
                "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass",
                "-File", FindScript(), "-LiteDirectory", root,
                "-WindowsAppSdkLine", "2.5",
                "-RuntimeListFile", runtimeFile,
                "-AppSdkPackageListFile", packageFile,
                "-VcRedistStateFile", vcFile,
                "-FixtureHostArchitecture", hostArch })
                info.ArgumentList.Add(arg);

            using var process = Process.Start(info)
                ?? throw new InvalidOperationException("Cannot start SDK prerequisite inspection.");
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            await process.WaitForExitAsync(timeout.Token);
            var output = await stdout + await stderr;
            if (expected)
            {
                Assert.True(process.ExitCode == 0, output);
                Assert.Contains("SIMULATED INPUT", output);
                Assert.Contains("prerequisite inspection passed", output);
            }
            else
                Assert.True(process.ExitCode != 0,
                    $"Invalid SDK inventory {scenario} unexpectedly passed: {output}");
        }
        finally
        {
            for (var retry = 0; Directory.Exists(root); retry++)
            {
                try { Directory.Delete(root, recursive: true); }
                catch (IOException) when (retry < 19) { await Task.Delay(100); }
                catch (UnauthorizedAccessException) when (retry < 19)
                {
                    await Task.Delay(100);
                }
            }
        }
    }

    private static string FindScript()
    {
        for (DirectoryInfo? dir = new(AppContext.BaseDirectory);
             dir is not null; dir = dir.Parent)
        {
            var script = Path.Combine(dir.FullName, "eng", "Test-LitePrerequisites.ps1");
            if (File.Exists(script)) return script;
        }
        throw new FileNotFoundException("Lite prerequisite script was not found.");
    }
}
