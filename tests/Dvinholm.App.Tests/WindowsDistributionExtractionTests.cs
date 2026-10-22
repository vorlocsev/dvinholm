using System.Diagnostics;
using System.IO.Compression;
using System.Text;

namespace Dvinholm.App.Tests;

/// <summary>
/// Eight isolated Windows PowerShell checks of the actual ZIP extraction
/// smoke gate. Synthetic executable fixtures carry a minimal PE header.
/// No WinUI process, installed app or J2534 adapter is started.
/// </summary>
public sealed class WindowsDistributionExtractionTests
{
    [Theory]
    [InlineData("portable-valid", "Portable", true)]
    [InlineData("lite-valid", "Lite", true)]
    [InlineData("portable-x64", "Portable", false)]
    [InlineData("lite-x64", "Lite", false)]
    [InlineData("bad-pe-signature", "Portable", false)]
    [InlineData("missing-app-dll", "Portable", false)]
    [InlineData("missing-deps", "Lite", false)]
    [InlineData("missing-icon", "Lite", false)]
    public async Task ExtractedZip_RequiresCorrectWinX86ApplicationLayout(
        string scenario, string flavor, bool valid)
    {
        if (!OperatingSystem.IsWindows())
            return;

        var root = Path.Combine(Path.GetTempPath(), "Dvinholm-extraction-tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var archivePath = Path.Combine(root, "app.zip");
        try
        {
            using (var zip = ZipFile.Open(archivePath, ZipArchiveMode.Create))
            {
                static void Add(ZipArchive archive, string path, byte[] bytes)
                {
                    var entry = archive.CreateEntry(path, CompressionLevel.Optimal);
                    using var stream = entry.Open();
                    stream.Write(bytes);
                }

                var executable = new byte[256];
                executable[0] = 0x4D;
                executable[1] = 0x5A;
                BitConverter.GetBytes(128).CopyTo(executable, 0x3C);
                executable[128] = 0x50; executable[129] = 0x45;
                var machine = scenario is "portable-x64" or "lite-x64"
                    ? (ushort)0x8664 : (ushort)0x014C;
                BitConverter.GetBytes(machine).CopyTo(executable, 132);
                if (scenario == "bad-pe-signature")
                    executable[128] = 0;
                Add(zip, "Dvinholm.App.exe", executable);

                var text = Encoding.UTF8.GetBytes("synthetic");
                if (scenario != "missing-app-dll")
                    Add(zip, "Dvinholm.App.dll", text);
                if (scenario != "missing-deps")
                    Add(zip, "Dvinholm.App.deps.json", Encoding.UTF8.GetBytes("{}"));
                if (scenario != "missing-icon")
                    Add(zip, "Assets/Dvinholm.ico", text);

                const string sharedRuntime =
                    """{"runtimeOptions":{"framework":{"name":"Microsoft.NETCore.App","version":"10.0.0"}}}""";
                Add(zip, "Dvinholm.App.runtimeconfig.json",
                    Encoding.UTF8.GetBytes(sharedRuntime));

                if (flavor == "Portable")
                {
                    Add(zip, "coreclr.dll", text);
                    Add(zip, "hostfxr.dll", text);
                }
            }

            var script = FindScript();
            var shell = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                "System32", "WindowsPowerShell", "v1.0", "powershell.exe");
            var start = new ProcessStartInfo
            {
                FileName = shell,
                WorkingDirectory = root,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            foreach (var arg in new[] {
                         "-NoProfile", "-NonInteractive", "-ExecutionPolicy",
                         "Bypass", "-File", script, "-Flavor", flavor, "-ZipPath", archivePath })
                start.ArgumentList.Add(arg);

            using var process = Process.Start(start)
                ?? throw new InvalidOperationException("Could not start x86 ZIP smoke.");
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            await process.WaitForExitAsync(timeout.Token);
            var output = await stdout + await stderr;
            if (valid)
            {
                Assert.True(process.ExitCode == 0,
                    $"Expected {scenario} to pass extracted ZIP smoke: {output}");
                Assert.Contains("Verified extracted", output);
            }
            else
                Assert.NotEqual(0, process.ExitCode);
        }
        finally
        {
            await WindowsTestFixtureCleanup.RemoveBestEffortAsync(root);
        }
    }

    private static string FindScript()
    {
        for (DirectoryInfo? directory = new(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            var path = Path.Combine(directory.FullName, "eng",
                "Test-WindowsDistributionExtraction.ps1");
            if (File.Exists(path))
                return path;
        }

        throw new FileNotFoundException("Distribution extraction smoke script is absent.");
    }
}
