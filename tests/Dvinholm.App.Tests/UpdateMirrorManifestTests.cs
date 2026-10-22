using System.Diagnostics;
using System.Text;
using Dvinholm.App;

namespace Dvinholm.App.Tests;

/// <summary>
/// Exercises the exact production PowerShell SHA-256 and mirror-manifest
/// functions without touching the installed application or recovery registry.
/// </summary>
public sealed class UpdateMirrorManifestTests
{
    [Theory]
    [InlineData("empty", true)]
    [InlineData("matching", true)]
    [InlineData("same-metadata-stale-bytes", true)]
    [InlineData("missing-file", true)]
    [InlineData("unexpected-file", false)]
    [InlineData("missing-empty-directory", false)]
    [InlineData("unexpected-empty-directory", false)]
    [InlineData("apostrophe-nested-binary", true)]
    public async Task VerifyMirror_RepairsFilesAndRejectsUnexpectedDirectoryTrees(
        string scenario, bool succeeds)
    {
        if (!OperatingSystem.IsWindows())
            return;

        var root = Path.Combine(Path.GetTempPath(), "Dvinholm-manifest-tests",
            Guid.NewGuid().ToString("N"), scenario == "apostrophe-nested-binary" ? "O'Neil's files" : "case");
        var source = Path.Combine(root, "staging");
        var destination = Path.Combine(root, "installed");
        var scriptPath = Path.Combine(root, "verify.ps1");
        var backup = Path.Combine(root, "unused-backup");
        Directory.CreateDirectory(source);
        Directory.CreateDirectory(destination);

        try
        {
            switch (scenario)
            {
                case "matching":
                case "same-metadata-stale-bytes":
                case "missing-file":
                case "unexpected-file":
                    File.WriteAllText(Path.Combine(source, "Dvinholm.App.exe"), "fresh bytes");
                    if (scenario != "missing-file")
                        File.WriteAllText(Path.Combine(destination, "Dvinholm.App.exe"),
                            scenario == "same-metadata-stale-bytes" ? "stale bytes" : "fresh bytes");
                    if (scenario == "same-metadata-stale-bytes")
                        File.SetLastWriteTimeUtc(Path.Combine(destination, "Dvinholm.App.exe"),
                            File.GetLastWriteTimeUtc(Path.Combine(source, "Dvinholm.App.exe")));
                    if (scenario == "unexpected-file")
                        File.WriteAllText(Path.Combine(destination, "orphan.dat"), "unexpected");
                    break;
                case "missing-empty-directory":
                    Directory.CreateDirectory(Path.Combine(source, "Assets", "empty"));
                    break;
                case "unexpected-empty-directory":
                    Directory.CreateDirectory(Path.Combine(destination, "Assets", "orphan"));
                    break;
                case "apostrophe-nested-binary":
                    Directory.CreateDirectory(Path.Combine(source, "Assets", "nested"));
                    Directory.CreateDirectory(Path.Combine(destination, "Assets", "nested"));
                    File.WriteAllBytes(Path.Combine(source, "Assets", "nested", "data.bin"),
                        new byte[] { 0, 255, 24, 18 });
                    File.WriteAllBytes(Path.Combine(destination, "Assets", "nested", "data.bin"),
                        new byte[] { 0, 254, 24, 18 });
                    break;
            }

            var generated = LatestBuildUpdater.BuildReplacementScript(
                source, destination, -1, scriptPath, backup);
            const string startMarker = "function GetSha256File {";
            const string endMarker = "function ClearStartupRecovery {";
            var startIndex = generated.IndexOf(startMarker, StringComparison.Ordinal);
            var endIndex = generated.IndexOf(endMarker, startIndex, StringComparison.Ordinal);
            Assert.True(startIndex >= 0 && endIndex > startIndex,
                "Could not extract production integrity functions.");
            static string Quote(string path) => "'" + path.Replace("'", "''", StringComparison.Ordinal) + "'";
            var isolatedScript = "$ErrorActionPreference = 'Stop'\n"
                + generated[startIndex..endIndex]
                + "\nEnsureMirroredBytes " + Quote(source) + " " + Quote(destination) + "\n";
            File.WriteAllText(scriptPath, isolatedScript, new UTF8Encoding(false));

            var powerShell = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                "System32", "WindowsPowerShell", "v1.0", "powershell.exe");
            var psi = new ProcessStartInfo
            {
                FileName = powerShell, WorkingDirectory = root,
                UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, RedirectStandardError = true
            };
            foreach (var argument in new[]
                     { "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-File", scriptPath })
                psi.ArgumentList.Add(argument);
            using (var process = Process.Start(psi)
                ?? throw new InvalidOperationException("PowerShell mirror verifier did not start."))
            {
                var outputTask = process.StandardOutput.ReadToEndAsync();
                var errorTask = process.StandardError.ReadToEndAsync();
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                await process.WaitForExitAsync(timeout.Token);
                var output = await outputTask + await errorTask;
                Assert.True(succeeds ? process.ExitCode == 0 : process.ExitCode != 0,
                    $"Case {scenario}, exit={process.ExitCode}: {output}");
                if (!succeeds)
                    Assert.Contains(scenario == "unexpected-file"
                        ? "missing or unexpected files"
                        : "missing or unexpected directories", output);
            }

            if (succeeds)
            {
                var sourceFiles = Directory.GetFiles(source, "*", SearchOption.AllDirectories)
                    .Select(p => Path.GetRelativePath(source, p))
                    .OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToArray();
                var destinationFiles = Directory.GetFiles(destination, "*", SearchOption.AllDirectories)
                    .Select(p => Path.GetRelativePath(destination, p))
                    .OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToArray();
                Assert.Equal(sourceFiles, destinationFiles);
                foreach (var path in sourceFiles)
                    Assert.Equal(File.ReadAllBytes(Path.Combine(source, path)),
                        File.ReadAllBytes(Path.Combine(destination, path)));
            }
            if (scenario == "missing-empty-directory")
                Assert.False(Directory.Exists(Path.Combine(destination, "Assets", "empty")));
            if (scenario == "unexpected-empty-directory")
                Assert.True(Directory.Exists(Path.Combine(destination, "Assets", "orphan")));
        }
        finally
        {
            // Windows Defender and filesystem indexers can briefly hold freshly
            // created test files after the PowerShell child has exited.
            // Cleanup is best-effort; the test assertions above determine
            // success or failure, not a transient teardown-only file lock.
            for (var attempt = 0; Directory.Exists(root) && attempt < 60; attempt++)
            {
                try { Directory.Delete(root, recursive: true); }
                catch (IOException ex)
                {
                    if (attempt == 59)
                        Console.Error.WriteLine($"Test fixture cleanup deferred due to file lock: {ex.Message}");
                    else
                        await Task.Delay(200);
                }
                catch (UnauthorizedAccessException ex)
                {
                    if (attempt == 59)
                        Console.Error.WriteLine($"Test fixture cleanup deferred due to access lock: {ex.Message}");
                    else
                        await Task.Delay(200);
                }
            }
        }
    }
}
