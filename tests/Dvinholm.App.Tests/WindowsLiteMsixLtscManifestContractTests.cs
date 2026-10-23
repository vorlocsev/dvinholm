using System.Diagnostics;
using System.IO.Compression;

namespace Dvinholm.App.Tests;

/// <summary>
/// Eight synthetic test cases for the experimental x86 MSIX identity and
/// Windows 11 LTSC compatibility metadata. No signature, install or launch.
/// </summary>
public sealed class WindowsLiteMsixLtscManifestContractTests
{
    [Theory]
    [InlineData("valid", 0)]
    [InlineData("wrong-publisher", 9)]
    [InlineData("wrong-family", 10)]
    [InlineData("too-new-minimum", 11)]
    [InlineData("missing-fulltrust", 12)]
    [InlineData("wrong-entrypoint", 13)]
    [InlineData("extra-application", 13)]
    [InlineData("nested-only-executable", 14)]
    public async Task LiteMsix_RejectsUnusableLtscMetadata(string scenario, int rejectionCheck)
    {
        if (!OperatingSystem.IsWindows()) return;
        var root = Path.Combine(Path.GetTempPath(), "Dvinholm-msix-ltsc",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var packagePath = Path.Combine(root, "fixture.msix");
            CreateFixture(packagePath, scenario);
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
                "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass",
                "-File", FindValidator(), "-PackagePath", packagePath
            }) start.ArgumentList.Add(arg);

            using var process = Process.Start(start)
                ?? throw new InvalidOperationException("Cannot start Lite MSIX validator.");
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            await process.WaitForExitAsync(timeout.Token);
            var output = await stdout + await stderr;
            if (rejectionCheck == 0)
            {
                Assert.True(process.ExitCode == 0, output);
                Assert.Contains("All Lite MSIX checks passed", output);
                Assert.Contains("UNSIGNED test package", output);
            }
            else
            {
                Assert.NotEqual(0, process.ExitCode);
                Assert.Contains($"MSIX check {rejectionCheck}:", output);
                Assert.DoesNotContain("All Lite MSIX checks passed", output);
            }
        }
        finally
        {
            await WindowsTestFixtureCleanup.RemoveBestEffortAsync(root);
        }
    }

    private static void CreateFixture(string path, string scenario)
    {
        var publisher = scenario == "wrong-publisher"
            ? "CN=Unexpected Publisher" : "CN=Dvinholm Development";
        var family = scenario == "wrong-family"
            ? "Windows.Mobile" : "Windows.Desktop";
        var minOs = scenario == "too-new-minimum"
            ? "10.0.27000.0" : "10.0.17763.0";
        var entryPoint = scenario == "wrong-entrypoint"
            ? "Other.EntryPoint" : "Windows.FullTrustApplication";
        var fullTrust = scenario == "missing-fulltrust" ? "" :
            """<Capabilities><rescap:Capability Name="runFullTrust" /></Capabilities>""";
        var extraApplication = scenario == "extra-application" ? 
            """<Application Id="Other" Executable="Dvinholm.App.exe" EntryPoint="Windows.FullTrustApplication" />""" : "";

        var manifest = $$"""
            <?xml version="1.0" encoding="utf-8"?>
            <Package xmlns="http://schemas.microsoft.com/appx/manifest/foundation/windows10"
                     xmlns:rescap="http://schemas.microsoft.com/appx/manifest/foundation/windows10/restrictedcapabilities">
              <Identity Name="Dvinholm.Lite" Publisher="{{publisher}}"
                        ProcessorArchitecture="x86" Version="0.2.2.0" />
              <Dependencies>
                <TargetDeviceFamily Name="{{family}}" MinVersion="{{minOs}}" MaxVersionTested="10.0.26100.0" />
                <PackageDependency Name="Microsoft.WindowsAppRuntime.2" MinVersion="2.5.1.0" Publisher="CN=Microsoft Corporation, O=Microsoft Corporation, L=Redmond, S=Washington, C=US" />
              </Dependencies>
              <Applications>
                <Application Id="App" Executable="Dvinholm.App.exe" EntryPoint="{{entryPoint}}" />
                {{extraApplication}}
              </Applications>
              {{fullTrust}}
            </Package>
            """;
        using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
        Write(archive, "AppxManifest.xml", manifest);
        Write(archive, "Dvinholm.App.runtimeconfig.json",
            """{"runtimeOptions":{"framework":{"name":"Microsoft.NETCore.App","version":"10.0.0"}}}""");
        var exe = scenario == "nested-only-executable"
            ? "payload/Dvinholm.App.exe" : "Dvinholm.App.exe";
        Write(archive, exe, "Synthetic placeholder, never executed.");
        foreach (var asset in new[] {
            "MsixSquare44x44Logo.png", "MsixSquare150x150Logo.png",
            "MsixWide310x150Logo.png", "MsixSplash620x300.png",
            "MsixStoreLogo.png"
        }) Write(archive, "Assets/" + asset, "Synthetic visual placeholder.");
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
        throw new FileNotFoundException("Lite MSIX archive validator not found.");
    }
}
