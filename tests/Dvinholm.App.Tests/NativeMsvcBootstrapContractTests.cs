namespace Dvinholm.App.Tests;

/// <summary>
/// Eight offline source contracts for the Node-free Windows x86 MSVC
/// bootstrap. Actual native compile, icons and ZIP validation run in CI.
/// </summary>
public sealed class NativeMsvcBootstrapContractTests
{
    [Fact]
    public void CiUsesLocalBootstrapInsteadOfDeprecatedNodeAction()
    {
        var workflow = ReadSource(".github", "workflows", "ci.yml");
        Assert.Contains("Invoke-NativeLauncherBuild.ps1 -OutputDirectory artifacts/native",
            workflow);
        Assert.DoesNotContain("uses: ilammy/msvc-dev-cmd@", workflow);
        Assert.Contains("Configure MSVC and build native x86 launchers (Node-free)",
            workflow);
    }

    [Fact]
    public void WindowsPowerShellParserAlsoChecksNewBootstrap()
    {
        var workflow = ReadSource(".github", "workflows", "ci.yml");
        Assert.Contains("'eng/Invoke-NativeLauncherBuild.ps1'", workflow);
        Assert.Contains("Parse Windows PowerShell 5.1 distribution scripts", workflow);
    }

    [Fact]
    public void BootstrapRequiresInstalledVisualStudioCompilerComponents()
    {
        var script = Bootstrap();
        Assert.Contains("vswhere.exe", script);
        Assert.Contains("-latest -products '*'", script);
        Assert.Contains("Microsoft.VisualStudio.Component.VC.Tools.x86.x64", script);
        Assert.Contains("-property installationPath", script);
        Assert.Contains("No Visual Studio installation", script);
    }

    [Fact]
    public void BootstrapInvokesVsDevCmdForX86TargetFromX64Host()
    {
        var script = Bootstrap();
        Assert.Contains("VsDevCmd.bat", script);
        Assert.Contains("-no_logo -arch=x86 -host_arch=x64", script);
        Assert.Contains("cmd.exe /d /c $command", script);
        Assert.Contains("VSCMD_ARG_TGT_ARCH -ne 'x86'", script);
        Assert.Contains("VSCMD_ARG_HOST_ARCH -ne 'x64'", script);
    }

    [Fact]
    public void BootstrapRejectsMissingOrFailedSetup()
    {
        var script = Bootstrap();
        Assert.Contains("Test-Path -LiteralPath $vswhere -PathType Leaf", script);
        Assert.Contains("Test-Path -LiteralPath $vsDevCmd -PathType Leaf", script);
        Assert.Contains("if ($LASTEXITCODE -ne 0)", script);
        Assert.Contains("throw 'VsDevCmd failed", script);
    }

    [Fact]
    public void BootstrapImportsOnlyAllowlistedCompilerEnvironment()
    {
        var script = Bootstrap();
        Assert.Contains("$allowedVariables.Contains($name)", script);
        Assert.Contains("[Environment]::SetEnvironmentVariable($name, $Matches[2], 'Process')",
            script);
        Assert.Contains("'PATH', 'INCLUDE', 'LIB', 'LIBPATH'", script);
        Assert.Contains("if (-not $seen.Contains($required))", script);
        Assert.DoesNotContain("GITHUB_ENV", script);
        Assert.DoesNotContain("GITHUB_PATH", script);
    }

    [Fact]
    public void BootstrapVerifiesX86ClAndRcBeforeCompiling()
    {
        var script = Bootstrap();
        Assert.Contains("Get-Command 'cl.exe' -ErrorAction Stop", script);
        Assert.Contains("Get-Command 'rc.exe' -ErrorAction Stop", script);
        Assert.Contains("Hostx64", script);
        Assert.Contains("cl", script);
        Assert.Contains("Build-Launchers.ps1", script);
        Assert.Contains("Native x86 Launcher build returned a non-zero", script);
    }

    [Fact]
    public void ExistingNativePeAndIconGatesArePreserved()
    {
        var script = ReadSource("eng", "Build-Launchers.ps1");
        Assert.Contains("Launcher.exe", script);
        Assert.Contains("Dvinholm.exe", script);
        Assert.Contains("'/MT'", script);
        Assert.Contains("'/MACHINE:X86'", script);
        Assert.Contains("0x14c", script);
        Assert.Contains("New-LauncherIcon.ps1", script);
        var workflow = ReadSource(".github", "workflows", "ci.yml");
        Assert.Contains("Verify distinct embedded native EXE icons", workflow);
        Assert.Contains("Test-Launchable-Lite.ps1", workflow);
    }

    private static string Bootstrap() =>
        ReadSource("eng", "Invoke-NativeLauncherBuild.ps1");

    private static string ReadSource(params string[] path)
    {
        for (DirectoryInfo? directory = new(AppContext.BaseDirectory);
             directory is not null; directory = directory.Parent)
        {
            var fullPath = Path.Combine(new[] { directory.FullName }.Concat(path).ToArray());
            if (File.Exists(fullPath))
                return File.ReadAllText(fullPath);
        }

        throw new FileNotFoundException("Missing source: " + Path.Combine(path));
    }
}
