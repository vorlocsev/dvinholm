using System.Diagnostics;
using System.IO.Compression;
using System.Text;

namespace Dvinholm.App.Tests;

/// <summary>
/// Eight synthetic project SDK version drift checks integrated with the real
/// Lite extraction + simulated prerequisite gate. No GUI or real host probes.
/// </summary>
public sealed class WindowsLiteSdkContractTests
{
    [Theory]
    [InlineData("supported-sdk-251", true)]
    [InlineData("supported-sdk-252", true)]
    [InlineData("unsupported-sdk-240", false)]
    [InlineData("unsupported-sdk-260", false)]
    [InlineData("unsupported-sdk-300", false)]
    [InlineData("missing-reference", false)]
    [InlineData("duplicate-reference", false)]
    [InlineData("unparseable-version", false)]
    public async Task SimulatedHost_RequiresCurrentProjectSdkLine(
        string scenario, bool expectedSuccess)
    {
        if (!OperatingSystem.IsWindows()) return;
        var temp = Path.Combine(Path.GetTempPath(), "Dvinholm-sdk-contract",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        try
        {
            var project = Path.Combine(temp, "LiteFixture.csproj");
            var version = scenario switch
            {
                "supported-sdk-252" => "2.5.2",
                "unsupported-sdk-240" => "2.4.0",
                "unsupported-sdk-260" => "2.6.0",
                "unsupported-sdk-300" => "3.0.0",
                "unparseable-version" => "2.5-preview",
                _ => "2.5.1"
            };
            var package = $"<PackageReference Include=\"Microsoft.WindowsAppSDK\" Version=\"{version}\" />";
            if (scenario == "missing-reference") package = "";
            if (scenario == "duplicate-reference") package += package;
            File.WriteAllText(project, $"<Project Sdk=\"Microsoft.NET.Sdk\"><ItemGroup>{package}</ItemGroup></Project>");

            var zipPath = Path.Combine(temp, "lite.zip");
            using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
            {
                static void Add(ZipArchive zip, string path, byte[] bytes)
                {
                    var entry = zip.CreateEntry(path, CompressionLevel.Fastest);
                    using var stream = entry.Open();
                    stream.Write(bytes);
                }
                var exe = new byte[256];
                exe[0] = 0x4D; exe[1] = 0x5A;
                BitConverter.GetBytes(128).CopyTo(exe, 0x3C);
                exe[128] = 0x50; exe[129] = 0x45;
                BitConverter.GetBytes((ushort)0x014C).CopyTo(exe, 132);
                Add(archive, "Dvinholm.App.exe", exe);
                Add(archive, "Dvinholm.App.dll", [1]);
                Add(archive, "Dvinholm.App.deps.json", Encoding.UTF8.GetBytes("{}"));
                Add(archive, "Assets/Dvinholm.ico", [1]);
                Add(archive, "Dvinholm.App.runtimeconfig.json",
                    Encoding.UTF8.GetBytes(
                        """{"runtimeOptions":{"framework":{"name":"Microsoft.NETCore.App","version":"10.0.0"}}}"""));
            }

            var shell = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                "System32", "WindowsPowerShell", "v1.0", "powershell.exe");
            var start = new ProcessStartInfo
            {
                FileName = shell, WorkingDirectory = temp, UseShellExecute = false,
                CreateNoWindow = true, RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            foreach (var arg in new[] {
                "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass",
                "-File", FindScript(), "-Flavor", "Lite", "-ZipPath", zipPath,
                "-SimulatedLiteHost", "-SdkProjectFile", project })
                start.ArgumentList.Add(arg);
            using var process = Process.Start(start)
                ?? throw new InvalidOperationException("Could not launch the Lite SDK contract check.");
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            await process.WaitForExitAsync(timeout.Token);
            var output = await stdout + await stderr;
            if (expectedSuccess)
            {
                Assert.True(process.ExitCode == 0, output);
                Assert.Contains("Verified simulated-host Windows App SDK contract", output);
                Assert.Contains("SIMULATED INPUT", output);
            }
            else
                Assert.True(process.ExitCode != 0,
                    $"Invalid SDK project scenario {scenario} unexpectedly passed: {output}");
        }
        finally
        {
            await WindowsTestFixtureCleanup.RemoveBestEffortAsync(temp);
        }
    }

    private static string FindScript()
    {
        for (DirectoryInfo? dir = new(AppContext.BaseDirectory);
             dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "eng",
                "Test-WindowsDistributionExtraction.ps1");
            if (File.Exists(candidate)) return candidate;
        }
        throw new FileNotFoundException("Missing Lite distribution extraction checker.");
    }
}
