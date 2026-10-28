using System.Diagnostics;
using System.IO.Compression;

namespace Dvinholm.App.Tests;

/// <summary>
/// Eight isolated unsigned MSIX metadata fixtures for offline deployment
/// dependency contracts; no certificate, package install, WinUI or adapter.
/// </summary>
[Trait("Category", "ArchivedWorkflow")]
public sealed class WindowsLiteMsixRuntimeDependencyTests
{
    [Theory]
    [InlineData("valid", 0)]
    [InlineData("missing-sdk-dependency", 15)]
    [InlineData("wrong-sdk-name", 15)]
    [InlineData("sdk-version-drift", 16)]
    [InlineData("wrong-sdk-publisher", 17)]
    [InlineData("missing-runtimeconfig", 18)]
    [InlineData("wrong-dotnet-framework", 19)]
    [InlineData("unexpected-signature", 20)]
    public async Task Validator_EnforcesPackagedRuntimeRequirements(
        string scenario, int expectedCheck)
    {
        if (!OperatingSystem.IsWindows()) return;

        var folder = Path.Combine(Path.GetTempPath(), "Dvinholm-msix-deps",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var packagePath = Path.Combine(folder, "fixture.msix");
            CreateFixture(packagePath, scenario);
            var windowsPowerShell = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                "System32", "WindowsPowerShell", "v1.0", "powershell.exe");
            var start = new ProcessStartInfo
            {
                FileName = windowsPowerShell,
                WorkingDirectory = folder,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            foreach (var part in new[] {
                "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass",
                "-File", FindValidator(), "-PackagePath", packagePath
            }) start.ArgumentList.Add(part);
            using var process = Process.Start(start)
                ?? throw new InvalidOperationException("PowerShell did not start.");
            var standard = process.StandardOutput.ReadToEndAsync();
            var errors = process.StandardError.ReadToEndAsync();
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            await process.WaitForExitAsync(deadline.Token);
            var output = await standard + await errors;
            if (expectedCheck == 0)
            {
                Assert.True(process.ExitCode == 0, output);
                Assert.Contains("All Lite MSIX checks passed", output);
            }
            else
            {
                Assert.NotEqual(0, process.ExitCode);
                Assert.Contains($"MSIX check {expectedCheck}:", output);
                Assert.DoesNotContain("All Lite MSIX checks passed", output);
            }
        }
        finally
        {
            await WindowsTestFixtureCleanup.RemoveBestEffortAsync(folder);
        }
    }

    private static void CreateFixture(string path, string scenario)
    {
        var sdkName = scenario == "wrong-sdk-name"
            ? "Microsoft.WindowsAppRuntime.1" : "Microsoft.WindowsAppRuntime.2";
        var sdkVersion = scenario == "sdk-version-drift"
            ? "2.4.0.0" : "2.5.1.0";
        var sdkPublisher = scenario == "wrong-sdk-publisher"
            ? "CN=Untrusted" :
              "CN=Microsoft Corporation, O=Microsoft Corporation, L=Redmond, S=Washington, C=US";
        var dependency = scenario == "missing-sdk-dependency" ? "" :
            $$"""<PackageDependency Name="{{sdkName}}" MinVersion="{{sdkVersion}}" Publisher="{{sdkPublisher}}" />""";
        var manifest = $$"""
            <?xml version="1.0" encoding="utf-8"?>
            <Package xmlns="http://schemas.microsoft.com/appx/manifest/foundation/windows10"
                     xmlns:rescap="http://schemas.microsoft.com/appx/manifest/foundation/windows10/restrictedcapabilities">
              <Identity Name="Dvinholm.Lite" Publisher="CN=Dvinholm Development" Version="0.2.2.0" ProcessorArchitecture="x86" />
              <Dependencies>
                <TargetDeviceFamily Name="Windows.Desktop" MinVersion="10.0.17763.0" MaxVersionTested="10.0.26100.0" />
                {{dependency}}
              </Dependencies>
              <Applications><Application Id="App" Executable="Dvinholm.App.exe" EntryPoint="Windows.FullTrustApplication" /></Applications>
              <Capabilities><rescap:Capability Name="runFullTrust" /></Capabilities>
            </Package>
            """;
        using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
        Write(archive, "AppxManifest.xml", manifest);
        Write(archive, "Dvinholm.App.exe", "Synthetic placeholder, not executable.");
        foreach (var asset in new[] {
            "MsixSquare44x44Logo.png", "MsixSquare150x150Logo.png",
            "MsixWide310x150Logo.png", "MsixSplash620x300.png", "MsixStoreLogo.png"
        }) Write(archive, "Assets/" + asset, "Synthetic placeholder.");
        if (scenario != "missing-runtimeconfig")
        {
            var framework = scenario == "wrong-dotnet-framework"
                ? "Microsoft.NETCore.App\",\"version\":\"9.0.0"
                : "Microsoft.NETCore.App\",\"version\":\"10.0.0";
            Write(archive, "Dvinholm.App.runtimeconfig.json",
                "{\"runtimeOptions\":{\"framework\":{\"name\":\"" + framework + "\"}}}");
        }
        if (scenario == "unexpected-signature")
            Write(archive, "AppxSignature.p7x", "Synthetic signature placeholder.");
    }

    private static void Write(ZipArchive archive, string name, string text)
    {
        using var writer = new StreamWriter(archive.CreateEntry(name).Open());
        writer.Write(text);
    }

    private static string FindValidator()
    {
        for (DirectoryInfo? dir = new(AppContext.BaseDirectory);
             dir is not null; dir = dir.Parent)
        {
            var path = Path.Combine(dir.FullName, "eng", "Test-LiteMsix.ps1");
            if (File.Exists(path)) return path;
        }
        throw new FileNotFoundException("Lite MSIX validator not found.");
    }
}
