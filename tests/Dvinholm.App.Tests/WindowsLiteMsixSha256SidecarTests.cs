using System.Diagnostics;
using System.Text;

namespace Dvinholm.App.Tests;

/// <summary>
/// Synthetic checksum fixtures. The checksum does not authenticate the
/// source and the bytes are not an installable MSIX.
/// </summary>
public sealed class WindowsLiteMsixSha256SidecarTests
{
    [Theory]
    [InlineData("valid", true, "verified SHA-256")]
    [InlineData("tampered-package", false, "SHA-256 mismatch")]
    [InlineData("missing-package", false, "Cannot find path")]
    [InlineData("missing-sidecar", false, "Cannot find path")]
    [InlineData("wrong-name", false, "expected one lowercase SHA-256 entry")]
    [InlineData("uppercase-hash", false, "expected one lowercase SHA-256 entry")]
    [InlineData("extra-entry", false, "expected one lowercase SHA-256 entry")]
    [InlineData("malformed-digest", false, "expected one lowercase SHA-256 entry")]
    public async Task UnsignedMsixSha256Sidecar_RejectsCorruptOrMalformedData(
        string scenario, bool expectedSuccess, string expectedText)
    {
        if (!OperatingSystem.IsWindows()) return;
        var root = Path.Combine(Path.GetTempPath(), "Dvinholm-msix-sha256",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var package = Path.Combine(root, "Dvinholm-Lite-x86-UNSIGNED.msix");
            var checksum = package + ".sha256";
            File.WriteAllBytes(package, Encoding.UTF8.GetBytes("Synthetic unsigned package bytes."));
            var write = await RunScriptAsync(root, package, checksum, "Write");
            Assert.True(write.ExitCode == 0, $"Sidecar writer exited {write.ExitCode}: {write.Output}");
            Assert.Contains("wrote SHA-256 sidecar", write.Output);

            switch (scenario)
            {
                case "tampered-package":
                    File.AppendAllText(package, "BAD");
                    break;
                case "missing-package":
                    File.Delete(package);
                    break;
                case "missing-sidecar":
                    File.Delete(checksum);
                    break;
                case "wrong-name":
                    File.WriteAllText(checksum,
                        File.ReadAllText(checksum).Replace(
                            "Dvinholm-Lite-x86-UNSIGNED.msix",
                            "Dvinholm-Lite-x86-SIGNED.msix"), Encoding.ASCII);
                    break;
                case "uppercase-hash":
                    File.WriteAllText(checksum,
                        File.ReadAllText(checksum).ToUpperInvariant(), Encoding.ASCII);
                    break;
                case "extra-entry":
                    File.AppendAllText(checksum,
                        new string('0', 64) + "  other.msix\n");
                    break;
                case "malformed-digest":
                    File.WriteAllText(checksum, "not-a-hash  Dvinholm-Lite-x86-UNSIGNED.msix\n");
                    break;
            }
            var verify = await RunScriptAsync(root, package, checksum, "Verify");
            if (expectedSuccess) Assert.Equal(0, verify.ExitCode);
            else Assert.NotEqual(0, verify.ExitCode);
            Assert.Contains(expectedText, verify.Output);
        }
        finally { await WindowsTestFixtureCleanup.RemoveBestEffortAsync(root); }
    }

    private static async Task<(int ExitCode, string Output)> RunScriptAsync(
        string directory, string package, string checksum, string mode)
    {
        var shell = Path.Combine(Environment.GetFolderPath(
            Environment.SpecialFolder.Windows), "System32",
            "WindowsPowerShell", "v1.0", "powershell.exe");
        var start = new ProcessStartInfo
        {
            FileName = shell,
            WorkingDirectory = directory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (var arg in new[] {
            "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass",
            "-File", FindScript(), "-PackagePath", package,
            "-ChecksumPath", checksum, "-Mode", mode
        }) start.ArgumentList.Add(arg);
        using var process = Process.Start(start)
            ?? throw new InvalidOperationException("Checksum PowerShell process did not start.");
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
            var path = Path.Combine(d.FullName, "eng", "Test-LiteMsixSha256.ps1");
            if (File.Exists(path)) return path;
        }
        throw new FileNotFoundException("MSIX SHA-256 sidecar script not found.");
    }
}
