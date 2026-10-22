using System.Diagnostics;
using System.Text;

namespace Dvinholm.App.Tests;

/// <summary>
/// Eight fixture-only Windows PowerShell 5.1 checks for strict .NET 10 Lite
/// runtimeconfig references. No real host packages or GUI are touched.
/// </summary>
public sealed class WindowsLiteRuntimeConfigContractTests
{
    [Theory]
    [InlineData("core-only", true)]
    [InlineData("core-and-desktop", true)]
    [InlineData("unsupported-extra", false)]
    [InlineData("desktop-dotnet9", false)]
    [InlineData("duplicate-core", false)]
    [InlineData("mixed-fields-duplicate", false)]
    [InlineData("invalid-version", false)]
    [InlineData("missing-version", false)]
    public async Task Preflight_RejectsUnknownOrMalformedFrameworks(
        string scenario, bool expectedSuccess)
    {
        if (!OperatingSystem.IsWindows()) return;
        var root = Path.Combine(Path.GetTempPath(), "Dvinholm-lite-frameworks",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var config = scenario switch
            {
                "core-and-desktop" =>
                    """{"runtimeOptions":{"frameworks":[{"name":"Microsoft.NETCore.App","version":"10.0.0"},{"name":"Microsoft.WindowsDesktop.App","version":"10.0.0"}]}}""",
                "unsupported-extra" =>
                    """{"runtimeOptions":{"frameworks":[{"name":"Microsoft.NETCore.App","version":"10.0.0"},{"name":"Unknown.Custom.App","version":"10.0.0"}]}}""",
                "desktop-dotnet9" =>
                    """{"runtimeOptions":{"frameworks":[{"name":"Microsoft.NETCore.App","version":"10.0.0"},{"name":"Microsoft.WindowsDesktop.App","version":"9.0.0"}]}}""",
                "duplicate-core" =>
                    """{"runtimeOptions":{"frameworks":[{"name":"Microsoft.NETCore.App","version":"10.0.0"},{"name":"Microsoft.NETCore.App","version":"10.0.2"}]}}""",
                "mixed-fields-duplicate" =>
                    """{"runtimeOptions":{"framework":{"name":"Microsoft.NETCore.App","version":"10.0.0"},"frameworks":[{"name":"Microsoft.NETCore.App","version":"10.0.0"}]}}""",
                "invalid-version" =>
                    """{"runtimeOptions":{"framework":{"name":"Microsoft.NETCore.App","version":"10.invalid.0"}}}""",
                "missing-version" =>
                    """{"runtimeOptions":{"framework":{"name":"Microsoft.NETCore.App"}}}""",
                _ =>
                    """{"runtimeOptions":{"framework":{"name":"Microsoft.NETCore.App","version":"10.0.0"}}}"""
            };
            File.WriteAllText(Path.Combine(root, "Dvinholm.App.runtimeconfig.json"), config);
            var runtimes = Path.Combine(root, "runtimes.txt");
            File.WriteAllText(runtimes,
                "Microsoft.NETCore.App 10.0.2 [C:\\dotnet-x86\\shared\\Microsoft.NETCore.App]\n" +
                "Microsoft.WindowsDesktop.App 10.0.2 [C:\\dotnet-x86\\shared\\Microsoft.WindowsDesktop.App]\n",
                Encoding.UTF8);
            var appSdk = Path.Combine(root, "appsdk.txt");
            File.WriteAllText(appSdk,
                "Microsoft.WindowsAppRuntime.2|X86|2.5.1.0\n" +
                "MicrosoftCorporationII.WinAppRuntime.Main.2|X64|2.5.1.0\n" +
                "MicrosoftCorporationII.WinAppRuntime.Singleton|X64|8002.5.1.0\n" +
                "Microsoft.WinAppRuntime.DDLM.2.5.1.0-x8|X86|2.5.1.0\n",
                Encoding.UTF8);
            var vc = Path.Combine(root, "vc-x86.txt");
            File.WriteAllText(vc, "1|14|v14.40.33816.0\n", Encoding.UTF8);
            var shell = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                "System32", "WindowsPowerShell", "v1.0", "powershell.exe");
            var info = new ProcessStartInfo
            {
                FileName = shell, WorkingDirectory = root, UseShellExecute = false,
                CreateNoWindow = true, RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            foreach (var arg in new[] {
                "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass",
                "-File", FindScript(), "-LiteDirectory", root,
                "-WindowsAppSdkLine", "2.5",
                "-RuntimeListFile", runtimes,
                "-AppSdkPackageListFile", appSdk,
                "-VcRedistStateFile", vc })
                info.ArgumentList.Add(arg);

            using var process = Process.Start(info)
                ?? throw new InvalidOperationException("Lite contract check did not start.");
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            await process.WaitForExitAsync(timeout.Token);
            var output = await stdout + await stderr;
            if (expectedSuccess)
            {
                Assert.True(process.ExitCode == 0, output);
                Assert.Contains("SIMULATED INPUT", output);
                Assert.Contains("prerequisite inspection passed", output);
            }
            else
                Assert.True(process.ExitCode != 0,
                    $"Invalid framework fixture {scenario} passed: {output}");
        }
        finally
        {
            for (var retry = 0; Directory.Exists(root) && retry < 40; retry++)
            {
                try { Directory.Delete(root, recursive: true); }
                catch (IOException ex)
                {
                    if (retry == 39) Console.Error.WriteLine(ex.Message);
                    else await Task.Delay(150);
                }
                catch (UnauthorizedAccessException ex)
                {
                    if (retry == 39) Console.Error.WriteLine(ex.Message);
                    else await Task.Delay(150);
                }
            }
        }
    }

    private static string FindScript()
    {
        for (DirectoryInfo? dir = new(AppContext.BaseDirectory);
             dir is not null; dir = dir.Parent)
        {
            var file = Path.Combine(dir.FullName, "eng", "Test-LitePrerequisites.ps1");
            if (File.Exists(file)) return file;
        }
        throw new FileNotFoundException("Lite prerequisite checker was not found.");
    }
}
