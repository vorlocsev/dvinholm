using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Dvinholm.App.Tests;

/// <summary>
/// Eight fixture-only release audit scenarios. No GitHub calls, package
/// installation, WinUI launch or physical J2534 device is involved.
/// </summary>
public sealed class WindowsPublishedDevelopmentAssetsTests
{
    [Theory]
    [InlineData("valid", true, "")]
    [InlineData("wrong-tag", false, "tag mismatch")]
    [InlineData("wrong-commit", false, "wrong commit")]
    [InlineData("not-prerelease", false, "visible prerelease")]
    [InlineData("missing-asset", false, "exactly three assets")]
    [InlineData("duplicate-asset", false, "Duplicate release asset")]
    [InlineData("size-mismatch", false, "Published asset size mismatch")]
    [InlineData("corrupt-content", false, "SHA-256 mismatch")]
    public async Task ReleaseAudit_ValidatesMetadataAndDownloadedBytes(
        string scenario, bool expectedSuccess, string failureText)
    {
        if (!OperatingSystem.IsWindows()) return;
        var root = Path.Combine(Path.GetTempPath(), "Dvinholm-postupload-audit",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            const string portable = "Dvinholm-Windows-x86.zip";
            const string lite = "Dvinholm-Windows-x86-Lite.zip";
            const string sums = "SHA256SUMS.txt";
            const string commit = "59d06f544eeac2145fd47439747216b35d72b0ca";
            File.WriteAllBytes(Path.Combine(root, portable), [1, 2, 3, 4, 5]);
            File.WriteAllBytes(Path.Combine(root, lite), [6, 7, 8, 9]);
            string Hash(string name) => Convert.ToHexString(
                SHA256.HashData(File.ReadAllBytes(Path.Combine(root, name))))
                .ToLowerInvariant();
            File.WriteAllLines(Path.Combine(root, sums), [
                $"{Hash(portable)}  {portable}", $"{Hash(lite)}  {lite}"
            ], Encoding.ASCII);

            var assets = new List<(string Name, long Size)>
            {
                (portable, new FileInfo(Path.Combine(root, portable)).Length),
                (lite, new FileInfo(Path.Combine(root, lite)).Length),
                (sums, new FileInfo(Path.Combine(root, sums)).Length)
            };
            if (scenario == "missing-asset") assets.RemoveAt(1);
            if (scenario == "duplicate-asset") assets[1] = assets[0];
            if (scenario == "size-mismatch")
                assets[1] = (lite, assets[1].Size + 1);

            var metadata = new
            {
                tag_name = scenario == "wrong-tag" ? "ci-999" : "ci-3202",
                target_commitish = scenario == "wrong-commit" ?
                    "0000000000000000000000000000000000000000" : commit,
                prerelease = scenario != "not-prerelease",
                draft = false,
                assets = assets.Select(a => new { name = a.Name, size = a.Size }).ToArray()
            };
            var jsonFile = Path.Combine(root, "release.json");
            File.WriteAllText(jsonFile, JsonSerializer.Serialize(metadata));
            if (scenario == "corrupt-content")
                File.WriteAllBytes(Path.Combine(root, lite), [10, 11, 12, 13]);

            var powershell = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                "System32", "WindowsPowerShell", "v1.0", "powershell.exe");
            var info = new ProcessStartInfo
            {
                FileName = powershell, WorkingDirectory = root,
                UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, RedirectStandardError = true
            };
            foreach (var arg in new[] {
                "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass",
                "-File", FindScript(), "-ReleaseMetadataFile", jsonFile,
                "-Directory", root, "-ExpectedTag", "ci-3202",
                "-ExpectedCommit", commit
            })
                info.ArgumentList.Add(arg);
            using var process = Process.Start(info)
                ?? throw new InvalidOperationException("Cannot start post-upload audit.");
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            await process.WaitForExitAsync(timeout.Token);
            var output = await stdout + await stderr;
            if (expectedSuccess)
            {
                Assert.True(process.ExitCode == 0, output);
                Assert.Contains("Verified three uploaded CI release assets", output);
                Assert.Contains("SHA-256 checksums verified", output);
            }
            else
            {
                Assert.NotEqual(0, process.ExitCode);
                Assert.Contains(failureText, output);
            }
        }
        finally
        {
            await WindowsTestFixtureCleanup.RemoveBestEffortAsync(root);
        }
    }

    private static string FindScript()
    {
        for (DirectoryInfo? dir = new(AppContext.BaseDirectory);
             dir is not null; dir = dir.Parent)
        {
            var path = Path.Combine(dir.FullName, "eng",
                "Test-PublishedDevelopmentRelease.ps1");
            if (File.Exists(path)) return path;
        }
        throw new FileNotFoundException("Post-upload audit script not found.");
    }
}
