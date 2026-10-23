using System.Diagnostics;
using System.IO.Compression;
using System.Text;

namespace Dvinholm.App.Tests;

/// <summary>
/// Read-only, explicitly simulated Windows LTSC MSIX prerequisite checks.
/// Does not mutate AppX registration, .NET runtimes, certificates or the OS.
/// </summary>
public sealed class WindowsLiteMsixOfflinePrerequisiteTests
{
    [Theory]
    [InlineData("valid", true, "SIMULATED INPUT")]
    [InlineData("missing-netcore", false, "Microsoft.NETCore.App")]
    [InlineData("dotnet9-only", false, "Microsoft.NETCore.App")]
    [InlineData("missing-desktop", false, "Microsoft.WindowsDesktop.App")]
    [InlineData("missing-appsdk", false, "Microsoft.WindowsAppRuntime.2")]
    [InlineData("appsdk-x64-only", false, "Microsoft.WindowsAppRuntime.2")]
    [InlineData("appsdk-too-old", false, "Microsoft.WindowsAppRuntime.2")]
    [InlineData("partial-fixtures", false, "Provide both fixture files")]
    [InlineData("wrong-identity", false, "Package identity")]
    [InlineData("package-x64", false, "Package identity")]
    [InlineData("sdk-wrong-publisher", false, "publisher does not match")]
    [InlineData("sdk-min-too-old", false, "older than 2.5.1.0")]
    [InlineData("runtimeconfig-net9", false, "valid .NET 10")]
    [InlineData("runtimeconfig-rollforward-major", false, "supports only default")]
    [InlineData("runtimeconfig-missing", false, "missing package-root")]
    [InlineData("runtimeconfig-patch-too-new", false, "Microsoft.NETCore.App")]
    [InlineData("frameworks-both-valid", true, "SIMULATED INPUT")]
    [InlineData("frameworks-desktop-patch-too-new", false, "Microsoft.WindowsDesktop.App")]
    [InlineData("frameworks-core-patch-too-new", false, "Microsoft.NETCore.App")]
    [InlineData("frameworks-unknown-name", false, "unsupported framework")]
    [InlineData("frameworks-duplicate", false, "duplicate framework")]
    [InlineData("frameworks-missing-core", false, "Microsoft.NETCore.App")]
    [InlineData("frameworks-reference-rollforward", false, "supports only default")]
    [InlineData("frameworks-reference-applypatches", false, "supports only default")]
    public async Task OfflineInspector_IsFailClosed(
        string scenario, bool expectedSuccess, string expectedText)
    {
        if (!OperatingSystem.IsWindows()) return;
        var root = Path.Combine(Path.GetTempPath(), "Dvinholm-msix-offline",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var packagePath = Path.Combine(root, "fixture.msix");
            using (var archive = ZipFile.Open(packagePath, ZipArchiveMode.Create))
            {
                var manifest = """
                    <Package xmlns="http://schemas.microsoft.com/appx/manifest/foundation/windows10">
                      <Identity Name="Dvinholm.Lite" Publisher="CN=Dvinholm Development"
                                ProcessorArchitecture="x86" Version="0.2.2.0" />
                      <Dependencies>
                        <PackageDependency Name="Microsoft.WindowsAppRuntime.2"
                          MinVersion="2.5.1.0"
                          Publisher="CN=Microsoft Corporation, O=Microsoft Corporation, L=Redmond, S=Washington, C=US"/>
                      </Dependencies>
                    </Package>
                    """;
                if (scenario == "wrong-identity")
                    manifest = manifest.Replace("Name=\"Dvinholm.Lite\"",
                        "Name=\"Dvinholm.Invalid\"");
                if (scenario == "package-x64")
                    manifest = manifest.Replace("ProcessorArchitecture=\"x86\"",
                        "ProcessorArchitecture=\"x64\"");
                if (scenario == "sdk-wrong-publisher")
                    manifest = manifest.Replace("Publisher=\"CN=Microsoft Corporation,",
                        "Publisher=\"CN=Unknown Corporation,");
                if (scenario == "sdk-min-too-old")
                    manifest = manifest.Replace("MinVersion=\"2.5.1.0\"",
                        "MinVersion=\"2.4.0.0\"");
                Write(archive, "AppxManifest.xml", manifest);
                if (scenario != "runtimeconfig-missing")
                {
                    var runtimeConfig = """
                        {"runtimeOptions":{"framework":{"name":"Microsoft.NETCore.App","version":"10.0.0"}}}
                        """;
                    if (scenario == "runtimeconfig-net9")
                        runtimeConfig = runtimeConfig.Replace("10.0.0", "9.0.0");
                    if (scenario == "runtimeconfig-patch-too-new")
                        runtimeConfig = runtimeConfig.Replace("10.0.0", "10.0.3");
                    if (scenario == "runtimeconfig-rollforward-major")
                        runtimeConfig = runtimeConfig.Replace("\"runtimeOptions\":{",
                            "\"runtimeOptions\":{\"rollForward\":\"Major\",");
                    if (scenario.StartsWith("frameworks-", StringComparison.Ordinal))
                    {
                        runtimeConfig = """
                            {"runtimeOptions":{"frameworks":[
                              {"name":"Microsoft.NETCore.App","version":"10.0.0"},
                              {"name":"Microsoft.WindowsDesktop.App","version":"10.0.0"}
                            ]}}
                            """;
                        if (scenario == "frameworks-desktop-patch-too-new")
                            runtimeConfig = runtimeConfig.Replace(
                                "\"Microsoft.WindowsDesktop.App\",\"version\":\"10.0.0\"",
                                "\"Microsoft.WindowsDesktop.App\",\"version\":\"10.0.3\"");
                        if (scenario == "frameworks-core-patch-too-new")
                            runtimeConfig = runtimeConfig.Replace(
                                "\"Microsoft.NETCore.App\",\"version\":\"10.0.0\"",
                                "\"Microsoft.NETCore.App\",\"version\":\"10.0.3\"");
                        if (scenario == "frameworks-unknown-name")
                            runtimeConfig = runtimeConfig.Replace(
                                "Microsoft.WindowsDesktop.App", "Unsupported.Framework");
                        if (scenario == "frameworks-duplicate")
                            runtimeConfig = runtimeConfig.Replace(
                                "Microsoft.WindowsDesktop.App", "Microsoft.NETCore.App");
                        if (scenario == "frameworks-missing-core")
                            runtimeConfig = runtimeConfig.Replace(
                                "{\"name\":\"Microsoft.NETCore.App\",\"version\":\"10.0.0\"},", "");
                        if (scenario == "frameworks-reference-rollforward")
                            runtimeConfig = runtimeConfig.Replace(
                                "\"Microsoft.NETCore.App\",\"version\":\"10.0.0\"",
                                "\"Microsoft.NETCore.App\",\"version\":\"10.0.0\",\"rollForward\":\"Disable\"");
                        if (scenario == "frameworks-reference-applypatches")
                            runtimeConfig = runtimeConfig.Replace(
                                "\"Microsoft.NETCore.App\",\"version\":\"10.0.0\"",
                                "\"Microsoft.NETCore.App\",\"version\":\"10.0.0\",\"applyPatches\":false");
                    }
                    Write(archive, "Dvinholm.App.runtimeconfig.json", runtimeConfig);
                }
            }
            var runtimeLines =
                "Microsoft.NETCore.App 10.0.2 [C:\\dotnet-x86\\shared\\Microsoft.NETCore.App]\n" +
                "Microsoft.WindowsDesktop.App 10.0.2 [C:\\dotnet-x86\\shared\\Microsoft.WindowsDesktop.App]\n";
            if (scenario == "missing-netcore")
                runtimeLines = runtimeLines.Split('\n')[1];
            if (scenario == "dotnet9-only")
                runtimeLines = runtimeLines.Replace("Microsoft.NETCore.App 10.0.2",
                    "Microsoft.NETCore.App 9.0.10");
            if (scenario == "missing-desktop")
                runtimeLines = runtimeLines.Split('\n')[0] + "\n";
            var sdkLine =
                "Microsoft.WindowsAppRuntime.2|X86|2.5.1.0|CN=Microsoft Corporation, O=Microsoft Corporation, L=Redmond, S=Washington, C=US\n";
            if (scenario == "missing-appsdk") sdkLine = "";
            if (scenario == "appsdk-x64-only")
                sdkLine = sdkLine.Replace("|X86|", "|X64|");
            if (scenario == "appsdk-too-old")
                sdkLine = sdkLine.Replace("|2.5.1.0|", "|2.4.0.0|");
            var runtimes = Path.Combine(root, "runtimes.txt");
            var packages = Path.Combine(root, "framework-packages.txt");
            File.WriteAllText(runtimes, runtimeLines, Encoding.UTF8);
            File.WriteAllText(packages, sdkLine, Encoding.UTF8);

            var ps = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                "System32", "WindowsPowerShell", "v1.0", "powershell.exe");
            var start = new ProcessStartInfo
            {
                FileName = ps,
                WorkingDirectory = root,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            foreach (var arg in new[] {
                "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass",
                "-File", FindScript(), "-PackagePath", packagePath,
                "-RuntimeListFile", runtimes
            }) start.ArgumentList.Add(arg);
            if (scenario != "partial-fixtures")
            {
                start.ArgumentList.Add("-FrameworkPackageListFile");
                start.ArgumentList.Add(packages);
            }
            using var process = Process.Start(start)
                ?? throw new InvalidOperationException("PowerShell could not start.");
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            await process.WaitForExitAsync(timeout.Token);
            var output = await stdout + await stderr;
            if (expectedSuccess)
            {
                Assert.True(process.ExitCode == 0, output);
                Assert.Contains("NOT INSTALL-READY", output);
            }
            else Assert.NotEqual(0, process.ExitCode);
            Assert.Contains(expectedText, output);
        }
        finally { await WindowsTestFixtureCleanup.RemoveBestEffortAsync(root); }
    }

    private static void Write(ZipArchive archive, string path, string content)
    {
        using var writer = new StreamWriter(archive.CreateEntry(path).Open());
        writer.Write(content);
    }

    private static string FindScript()
    {
        for (DirectoryInfo? dir = new(AppContext.BaseDirectory);
             dir is not null; dir = dir.Parent)
        {
            var script = Path.Combine(dir.FullName, "eng",
                "Test-LiteMsixOfflinePrerequisites.ps1");
            if (File.Exists(script)) return script;
        }
        throw new FileNotFoundException("Offline MSIX inspector missing.");
    }
}
