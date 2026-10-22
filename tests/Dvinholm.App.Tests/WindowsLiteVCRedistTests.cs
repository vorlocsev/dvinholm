using System.Diagnostics;
using System.Text;

namespace Dvinholm.App.Tests;

/// <summary>
/// Eight fixture-backed Windows PowerShell 5.1 tests for VC++ x86 detection.
/// No host registry keys are modified and no application GUI is launched.
/// </summary>
public sealed class WindowsLiteVCRedistTests
{
    [Theory]
    [InlineData("installed-v14-40", "1|14|v14.40.33816.0", true)]
    [InlineData("installed-v14-42", "1|14|14.42.34433.0", true)]
    [InlineData("no-x86-redist", "", false)]
    [InlineData("not-installed", "0|14|v14.40.33816.0", false)]
    [InlineData("legacy-vc12", "1|12|v12.0.40664.0", false)]
    [InlineData("invalid-major", "1|bad|v14.40.33816.0", false)]
    [InlineData("empty-version", "1|14|", false)]
    [InlineData("version-mismatch", "1|14|v15.0.0.0", false)]
    public async Task PrerequisiteProbe_RequiresVCX86(string scenario, string record, bool expected)
    {
        if (!OperatingSystem.IsWindows())
            return;

        var root = Path.Combine(Path.GetTempPath(), "Dvinholm-lite-vc",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            File.WriteAllText(Path.Combine(root, "Dvinholm.App.runtimeconfig.json"),
                """{"runtimeOptions":{"framework":{"name":"Microsoft.NETCore.App","version":"10.0.0"}}}""");

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
                "Microsoft.WinAppRuntime.DDLM.2.5.1.0-x8|X86|2.5.1.0\n", Encoding.UTF8);
            var vc = Path.Combine(root, "vc-x86.txt");
            File.WriteAllText(vc, record + "\n", Encoding.UTF8);

            var shell = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                "System32", "WindowsPowerShell", "v1.0", "powershell.exe");
            var psi = new ProcessStartInfo
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
                "-RuntimeListFile", runtimes, "-AppSdkPackageListFile", appSdk,
                "-VcRedistStateFile", vc })
                psi.ArgumentList.Add(arg);

            using var process = Process.Start(psi)
                ?? throw new InvalidOperationException("Could not launch VC++ prerequisite check.");
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            await process.WaitForExitAsync(timeout.Token);
            var output = await stdout + await stderr;
            if (expected)
            {
                Assert.True(process.ExitCode == 0, $"Expected {scenario} to pass: {output}");
                Assert.Contains("SIMULATED INPUT", output);
                Assert.Contains("Verified registered Microsoft Visual C++", output);
            }
            else
            {
                Assert.True(process.ExitCode != 0,
                    $"Invalid VC++ fixture {scenario} unexpectedly passed: {output}");
            }
        }
        finally
        {
            for (var attempt = 0; Directory.Exists(root); attempt++)
            {
                try { Directory.Delete(root, recursive: true); }
                catch (IOException) when (attempt < 19) { await Task.Delay(100); }
                catch (UnauthorizedAccessException) when (attempt < 19)
                {
                    await Task.Delay(100);
                }
            }
        }
    }

    private static string FindScript()
    {
        for (DirectoryInfo? current = new(AppContext.BaseDirectory);
             current is not null; current = current.Parent)
        {
            var script = Path.Combine(current.FullName, "eng", "Test-LitePrerequisites.ps1");
            if (File.Exists(script)) return script;
        }
        throw new FileNotFoundException("Lite prerequisite checker is missing.");
    }
}
