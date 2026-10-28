using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace Dvinholm.App.Tests;

/// <summary>
/// Uploaded artifact pair validation using disposable synthetic bytes.
/// Tests neither GitHub access nor MSIX installability or publisher trust.
/// </summary>
[Trait("Category", "ArchivedWorkflow")]
public sealed class WindowsLiteMsixArtifactRoundtripTests
{
    [Theory]
    [InlineData("valid", true, "downloaded bytes match the original build")]
    [InlineData("empty", false, "expected exactly two root files")]
    [InlineData("missing-msix", false, "expected exactly two root files")]
    [InlineData("missing-checksum", false, "expected exactly two root files")]
    [InlineData("extra-file", false, "expected exactly two root files")]
    [InlineData("nested-file", false, "unexpected nested directory")]
    [InlineData("modified-package", false, "differs from original build")]
    [InlineData("modified-sidecar", false, "SHA-256 mismatch")]
    public async Task DownloadedMsixArtifact_RequiresTwoExactFilesAndBuildDigest(
        string scenario, bool shouldPass, string expectedMessage)
    {
        if (!OperatingSystem.IsWindows()) return;
        var root = Path.Combine(Path.GetTempPath(),
            "Dvinholm-msix-roundtrip", Guid.NewGuid().ToString("N"));
        var downloadDir = Path.Combine(root, "download");
        Directory.CreateDirectory(downloadDir);
        try
        {
            var original = Path.Combine(root, "original.msix");
            var name = "Dvinholm-Lite-x86-UNSIGNED.msix";
            var package = Path.Combine(downloadDir, name);
            var sidecar = package + ".sha256";
            var bytes = Encoding.ASCII.GetBytes("Disposable synthetic MSIX artifact bytes.");
            File.WriteAllBytes(original, bytes);
            File.WriteAllBytes(package, bytes);
            var digest = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
            File.WriteAllText(sidecar, $"{digest}  {name}\n", Encoding.ASCII);
            switch (scenario)
            {
                case "empty":
                    File.Delete(package);
                    File.Delete(sidecar);
                    break;
                case "missing-msix":
                    File.Delete(package);
                    break;
                case "missing-checksum":
                    File.Delete(sidecar);
                    break;
                case "extra-file":
                    File.WriteAllText(Path.Combine(downloadDir, "other.txt"), "unexpected");
                    break;
                case "nested-file":
                    Directory.CreateDirectory(Path.Combine(downloadDir, "subfolder"));
                    break;
                case "modified-package":
                    File.AppendAllText(package, "changed");
                    break;
                case "modified-sidecar":
                    File.WriteAllText(sidecar, $"{new string('0', 64)}  {name}\n", Encoding.ASCII);
                    break;
            }
            var (exitCode, output) = await RunCheckAsync(root, downloadDir, original);
            if (shouldPass) Assert.True(exitCode == 0, output);
            else Assert.NotEqual(0, exitCode);
            Assert.Contains(expectedMessage, output);
        }
        finally
        {
            await WindowsTestFixtureCleanup.RemoveBestEffortAsync(root);
        }
    }

    private static async Task<(int, string)> RunCheckAsync(
        string root, string downloadDir, string original)
    {
        var powershell = Path.Combine(Environment.GetFolderPath(
            Environment.SpecialFolder.Windows), "System32",
            "WindowsPowerShell", "v1.0", "powershell.exe");
        var start = new ProcessStartInfo
        {
            FileName = powershell, WorkingDirectory = root,
            UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true
        };
        foreach (var arg in new[] {
            "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass",
            "-File", FindScript(), "-ArtifactDirectory", downloadDir,
            "-OriginalPackagePath", original
        }) start.ArgumentList.Add(arg);
        using var process = Process.Start(start)
            ?? throw new InvalidOperationException("Roundtrip test process failed to start.");
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        try { await process.WaitForExitAsync(timeout.Token); }
        catch (OperationCanceledException)
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            throw;
        }
        return (process.ExitCode, await stdout + await stderr);
    }

    private static string FindScript()
    {
        for (DirectoryInfo? d = new(AppContext.BaseDirectory);
             d is not null; d = d.Parent)
        {
            var p = Path.Combine(d.FullName, "eng", "Test-LiteMsixDownloadedArtifact.ps1");
            if (File.Exists(p)) return p;
        }
        throw new FileNotFoundException("MSIX artifact roundtrip script not found.");
    }
}
