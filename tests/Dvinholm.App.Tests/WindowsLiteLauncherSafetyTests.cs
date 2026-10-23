using System.Diagnostics;
using System.Text;

namespace Dvinholm.App.Tests;

/// <summary>
/// Eight Windows CMD integration fixtures for the actual packaged Lite
/// launcher. Harmless fake payload and PowerShell inspector, no GUI launch.
/// </summary>
public sealed class WindowsLiteLauncherSafetyTests
{
    [Theory]
    [InlineData("spaces-in-path", 0)]
    [InlineData("different-working-directory", 0)]
    [InlineData("missing-exe", 10)]
    [InlineData("missing-runtimeconfig", 11)]
    [InlineData("missing-inspector", 12)]
    [InlineData("inspector-error", 7)]
    [InlineData("path-hijack", 0)]
    [InlineData("quiet-scripted-launch", 0)]
    public async Task CmdLauncher_OnlyUsesSystemPowerShellAndPreservesFailures(
        string scenario, int expectedCode)
    {
        if (!OperatingSystem.IsWindows()) return;

        var root = Path.Combine(Path.GetTempPath(), "Dvinholm Lite cmd fixtures",
            Guid.NewGuid().ToString("N"));
        var lite = Path.Combine(root, "Lite with spaces");
        Directory.CreateDirectory(lite);
        try
        {
            var launcher = Path.Combine(lite, "Check-Lite-Prerequisites.cmd");
            var executable = Path.Combine(lite, "Dvinholm.App.exe");
            var runtimeconfig = Path.Combine(lite, "Dvinholm.App.runtimeconfig.json");
            var inspector = Path.Combine(lite, "Check-Lite-Prerequisites.ps1");
            var invocation = Path.Combine(lite, "invocation.txt");

            File.Copy(FindLauncherSource(), launcher);
            if (scenario != "missing-exe")
                File.WriteAllBytes(executable, [0x4d, 0x5a]);
            if (scenario != "missing-runtimeconfig")
                File.WriteAllText(runtimeconfig, "{\"runtimeOptions\":{}}");

            if (scenario != "missing-inspector")
            {
                var exitCode = scenario == "inspector-error" ? 7 : 0;
                // Fixture script deliberately has no real SDK/runtime queries.
                File.WriteAllText(inspector, """
                    param([string]$LiteDirectory, [string]$WindowsAppSdkLine)
                    [IO.File]::WriteAllText(
                      (Join-Path $PSScriptRoot 'invocation.txt'),
                      ($LiteDirectory + [Environment]::NewLine + $WindowsAppSdkLine))
                    exit EXIT_CODE
                    """.Replace("EXIT_CODE", exitCode.ToString(), StringComparison.Ordinal),
                    new UTF8Encoding(false));
            }

            // An untrusted PowerShell executable in the extraction folder or
            // PATH must never be launched instead of System32's interpreter.
            if (scenario == "path-hijack")
                File.WriteAllBytes(Path.Combine(lite, "powershell.exe"),
                    [0x4d, 0x5a, 0, 0, 0, 0]);

            var cmd = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                "System32", "cmd.exe");
            var start = new ProcessStartInfo
            {
                FileName = cmd,
                Arguments = $"/d /c call \"{launcher}\"",
                WorkingDirectory = scenario == "different-working-directory" ? root : lite,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            start.Environment["DVINHOLM_LITE_NO_PAUSE"] = "1";
            if (scenario == "path-hijack")
                start.Environment["PATH"] = lite + ";" + start.Environment["PATH"];

            using var process = Process.Start(start)
                ?? throw new InvalidOperationException("Could not start Lite CMD launcher.");
            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            await process.WaitForExitAsync(deadline.Token);
            var output = await outputTask + await errorTask;
            Assert.Equal(expectedCode, process.ExitCode);
            Assert.DoesNotContain("Press any key", output, StringComparison.OrdinalIgnoreCase);

            if (expectedCode is 10 or 11 or 12)
            {
                Assert.Contains("ERROR:", output);
                Assert.False(File.Exists(invocation));
            }
            else
            {
                Assert.True(File.Exists(invocation), output);
                var args = File.ReadAllLines(invocation);
                Assert.Equal(2, args.Length);
                Assert.Equal(lite.TrimEnd('\\'), args[0].TrimEnd('\\'),
                    StringComparer.OrdinalIgnoreCase);
                Assert.Equal("2.5", args[1]);
                if (expectedCode != 0)
                    Assert.Contains("prerequisites are missing", output);
            }
        }
        finally
        {
            await WindowsTestFixtureCleanup.RemoveBestEffortAsync(root);
        }
    }

    private static string FindLauncherSource()
    {
        for (DirectoryInfo? dir = new(AppContext.BaseDirectory);
             dir is not null; dir = dir.Parent)
        {
            var source = Path.Combine(dir.FullName, "eng",
                "Check-Lite-Prerequisites.cmd");
            if (File.Exists(source)) return source;
        }
        throw new FileNotFoundException("Missing Lite CMD launcher source.");
    }
}
