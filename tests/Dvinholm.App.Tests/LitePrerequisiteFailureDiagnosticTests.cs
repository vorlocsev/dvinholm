namespace Dvinholm.App.Tests;

/// <summary>
/// Eight failure-classification cases. No real machine probes or update
/// helper launch; never reflect raw PowerShell error text in the UI.
/// </summary>
public sealed class LitePrerequisiteFailureDiagnosticTests
{
    [Theory]
    [InlineData("Missing .NET 10 Desktop Runtime x86: no x86 dotnet host at C:\\Private\\dotnet.exe",
        ".NET 10 Desktop Runtime x86")]
    [InlineData("Missing .NET 10 x86 shared runtime Microsoft.WindowsDesktop.App for requested 10.0.0",
        ".NET 10 Desktop Runtime x86")]
    [InlineData("Missing .NET 10 x86 shared runtime Microsoft.NETCore.App for requested 10.0.0",
        ".NET 10 x86 runtime")]
    [InlineData("Missing Windows App SDK package Microsoft.WindowsAppRuntime.2 for X86",
        "Windows App SDK 2.5 runtime package")]
    [InlineData("Missing Windows App SDK DDLM x86 package matching release 2.5",
        "Windows App SDK 2.5 runtime package")]
    [InlineData("Missing or invalid Microsoft Visual C++ Redistributable x86 (14.x or later)",
        "Visual C++ Redistributable x86")]
    [InlineData("Get-AppxPackage : The term cannot be loaded due to AppX registration",
        "registration could not be inspected")]
    [InlineData("UNKNOWN PRIVATE PATH C:\\Secrets\\VEHICLE-SECRET",
        "prerequisite check did not complete successfully")]
    public void FailedCheckIdentifiesSingleSafeCauseWithoutLeakingRawOutput(
        string probeError, string expectedReason)
    {
        var message = LatestBuildUpdater.ExplainLitePrerequisiteFailure(
            "machine name: PRIVATE-COMPUTER-NAME", probeError);
        Assert.Contains(expectedReason, message);
        Assert.Contains("Installed application files were not replaced", message);
        Assert.Contains("Check-Lite-Prerequisites.cmd", message);
        Assert.DoesNotContain("PRIVATE", message);
        Assert.DoesNotContain("C:\\", message);
        Assert.DoesNotContain("install .NET 10 Desktop Runtime x86, Windows App SDK", message);
        Assert.True(message.Length < 350);
    }
}
