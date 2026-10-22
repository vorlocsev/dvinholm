using System.Diagnostics;
using System.IO.Compression;
using System.Text;

namespace Dvinholm.App.Tests;

/// <summary>
/// Eight Windows PowerShell integration checks against the actual ZIP
/// validation gate used by CI and future Stable packaging. Archives contain
/// synthetic harmless files and never launch the WinUI application.
/// </summary>
public sealed class WindowsDistributionArchiveTests
{
    [Theory]
    [InlineData("portable-ok", "Portable", true)]
    [InlineData("lite-ok", "Lite", true)]
    [InlineData("traversal", "Portable", false)]
    [InlineData("case-duplicate", "Portable", false)]
    [InlineData("invalid-lite-runtime", "Lite", false)]
    [InlineData("pdb", "Portable", false)]
    [InlineData("debugger", "Portable", false)]
    [InlineData("portable-missing-hostfxr", "Portable", false)]
    public async Task ZipVerifier_AcceptsSafeDistributionsAndRejectsUnsafeOnes(
        string scenario, string flavor, bool expectedToPass)
    {
        if (!OperatingSystem.IsWindows())
            return;

        var root = Path.Combine(Path.GetTempPath(),
            "Dvinholm-distribution-archive-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var archivePath = Path.Combine(root, "distribution.zip");
        try
        {
            using (var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create))
            {
                static void Add(ZipArchive zip, string name, string content)
                {
                    var entry = zip.CreateEntry(name, CompressionLevel.Optimal);
                    using var stream = entry.Open();
                    var data = Encoding.UTF8.GetBytes(content);
                    stream.Write(data);
                }

                Add(archive, "Dvinholm.App.exe", "synthetic executable");
                Add(archive, "Assets/Dvinholm.ico", "synthetic test icon");
                Add(archive, "Dvinholm.App.deps.json", "{}");
                var runtime = scenario == "invalid-lite-runtime"
                    ? """
                      {"runtimeOptions":{"framework":{"name":"Microsoft.NETCore.App","version":"9.0.0"}}}
                      """
                    : """
                      {"runtimeOptions":{"framework":{"name":"Microsoft.NETCore.App","version":"10.0.0"}}}
                      """;
                Add(archive, "Dvinholm.App.runtimeconfig.json", runtime);
                if (flavor == "Portable")
                {
                    Add(archive, "coreclr.dll", "synthetic runtime binary");
                    if (scenario != "portable-missing-hostfxr")
                        Add(archive, "hostfxr.dll", "synthetic host binary");
                }

                switch (scenario)
                {
                    case "traversal":
                        Add(archive, "../outside.txt", "path traversal should be rejected");
                        break;
                    case "case-duplicate":
                        Add(archive, "assets/dvinholm.ico", "Windows path collision");
                        break;
                    case "pdb":
                        Add(archive, "symbols/Dvinholm.App.pdb", "debugging symbols");
                        break;
                    case "debugger":
                        Add(archive, "tools/createdump.exe", "debugger must not ship");
                        break;
                }
            }

            var verifier = FindVerifier();
            var powershell = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                "System32", "WindowsPowerShell", "v1.0", "powershell.exe");
            var start = new ProcessStartInfo
            {
                FileName = powershell,
                WorkingDirectory = root,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            foreach (var item in new[]
                     { "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass",
                       "-File", verifier, "-Flavor", flavor, "-ZipPath", archivePath })
                start.ArgumentList.Add(item);

            using var process = Process.Start(start)
                ?? throw new InvalidOperationException("Could not launch ZIP verifier.");
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            await process.WaitForExitAsync(timeout.Token);
            var output = await stdout + await stderr;

            if (expectedToPass)
            {
                Assert.True(process.ExitCode == 0,
                    $"Valid {scenario} distribution failed ZIP preflight: {output}");
                Assert.Contains("Verified clean", output);
            }
            else
            {
                Assert.NotEqual(0, process.ExitCode);
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

    private static string FindVerifier()
    {
        for (DirectoryInfo? directory = new(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName,
                "eng", "Test-WindowsDistribution.ps1");
            if (File.Exists(candidate))
                return candidate;
        }

        throw new FileNotFoundException(
            "Could not find the repository's Windows distribution ZIP verifier.");
    }
}
