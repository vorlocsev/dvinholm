using System.Diagnostics;
using System.IO.Compression;

namespace Dvinholm.App.Tests;

/// <summary>
/// An archived validator starts a separate Windows PowerShell 5.1 host per
/// scenario. These subprocess fixtures must not compete with other xUnit
/// collections on the same CI runner.
/// </summary>
[CollectionDefinition("Archived MSIX validator process isolation", DisableParallelization = true)]
public sealed class WindowsLiteMsixNegativeProcessCollection
{
}

/// <summary>
/// Eight deterministic invalid MSIX archives exercise all eight rejection
/// contracts in the experimental, unsigned Lite MSIX package validator.
/// This checks only archive metadata; no package is installed or launched.
/// </summary>
[Collection("Archived MSIX validator process isolation")]
[Trait("Category", "ArchivedWorkflow")]
public sealed class WindowsLiteMsixNegativeContractTests
{
    [Theory]
    [InlineData("no-manifest", 1)]
    [InlineData("x64-manifest", 2)]
    [InlineData("wrong-identity", 3)]
    [InlineData("wrong-app-executable", 4)]
    [InlineData("missing-exe", 5)]
    [InlineData("bundled-runtime", 6)]
    [InlineData("missing-visual-asset", 7)]
    [InlineData("debug-payload", 8)]
    public async Task MsixValidator_RejectsMalformedOrUnsafePackages(
        string scenario, int expectedCheck)
    {
        if (!OperatingSystem.IsWindows()) return;
        var root = Path.Combine(Path.GetTempPath(), "Dvinholm-msix-negative",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var package = Path.Combine(root, "invalid-lite.msix");
            CreateFixture(package, scenario);

            var powershell = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                "System32", "WindowsPowerShell", "v1.0", "powershell.exe");
            var info = new ProcessStartInfo
            {
                FileName = powershell,
                WorkingDirectory = root,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            foreach (var arg in new[] {
                "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass",
                "-File", FindValidator(), "-PackagePath", package
            }) info.ArgumentList.Add(arg);

            using var process = Process.Start(info)
                ?? throw new InvalidOperationException("Cannot start Lite MSIX validator.");
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            try
            {
                await process.WaitForExitAsync(timeout.Token);
            }
            catch (OperationCanceledException) when (timeout.IsCancellationRequested)
            {
                // A hung PowerShell process must not leak into later tests or
                // keep the disposable fixture locked for the cleanup retries.
                try
                {
                    if (!process.HasExited)
                        process.Kill(entireProcessTree: true);
                }
                catch (InvalidOperationException)
                {
                    // It exited while we were requesting termination.
                }

                using var cleanupTimeout = new CancellationTokenSource(
                    TimeSpan.FromSeconds(5));
                try
                {
                    await process.WaitForExitAsync(cleanupTimeout.Token);
                }
                catch (OperationCanceledException)
                {
                    // Preserve the original timeout instead of waiting without
                    // a bound on an unhealthy Windows runner.
                }

                throw new TimeoutException(
                    $"Lite MSIX negative validator did not exit within 60 seconds (scenario: {scenario}).");
            }

            var output = await stdout + await stderr;
            Assert.NotEqual(0, process.ExitCode);
            Assert.Contains($"MSIX check {expectedCheck}:", output);
            Assert.DoesNotContain("All eight Lite MSIX checks passed", output);
        }
        finally
        {
            await WindowsTestFixtureCleanup.RemoveBestEffortAsync(root);
        }
    }

    private static void CreateFixture(string path, string scenario)
    {
        using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
        if (scenario != "no-manifest")
        {
            var architecture = scenario == "x64-manifest" ? "x64" : "x86";
            var name = scenario == "wrong-identity" ? "Dvinholm.Other" : "Dvinholm.Lite";
            var executable = scenario == "wrong-app-executable" ?
                "NotDvinholm.exe" : "Dvinholm.App.exe";
            var manifest = $$"""
                <?xml version="1.0" encoding="utf-8"?>
                <Package xmlns="http://schemas.microsoft.com/appx/manifest/foundation/windows10">
                  <Identity Name="{{name}}" Publisher="CN=Dvinholm Development"
                            Version="0.2.2.0" ProcessorArchitecture="{{architecture}}"/>
                  <Applications>
                    <Application Id="App" Executable="{{executable}}" EntryPoint="Windows.FullTrustApplication"/>
                  </Applications>
                </Package>
                """;
            Write(archive, "AppxManifest.xml", manifest);
        }

        if (scenario != "missing-exe")
            Write(archive, "Dvinholm.App.exe", "Synthetic test fixture, not an executable.");
        foreach (var asset in new[] {
            "MsixSquare44x44Logo.png", "MsixSquare150x150Logo.png",
            "MsixWide310x150Logo.png", "MsixSplash620x300.png",
            "MsixStoreLogo.png"
        })
        {
            if (scenario == "missing-visual-asset" && asset == "MsixStoreLogo.png")
                continue;
            Write(archive, "Assets/" + asset, "Synthetic image placeholder.");
        }
        if (scenario == "bundled-runtime")
            Write(archive, "coreclr.dll", "Bundled runtime must be rejected.");
        if (scenario == "debug-payload")
            Write(archive, "symbols/Dvinholm.App.pdb", "Debug files must be rejected.");
    }

    private static void Write(ZipArchive archive, string name, string content)
    {
        using var writer = new StreamWriter(archive.CreateEntry(name).Open());
        writer.Write(content);
    }

    private static string FindValidator()
    {
        for (DirectoryInfo? dir = new(AppContext.BaseDirectory);
             dir is not null; dir = dir.Parent)
        {
            var script = Path.Combine(dir.FullName, "eng", "Test-LiteMsix.ps1");
            if (File.Exists(script)) return script;
        }
        throw new FileNotFoundException("Lite MSIX archive validator was not found.");
    }
}
