using System.Reflection;

namespace Dvinholm.App.Tests;

/// <summary>
/// Opted-in Development retains offline smoke controls; the actual Stable
/// artifact is separately checked for their absence by the RC verifier.
/// </summary>
public sealed class DevelopmentOnlyControlSurfaceTests
{
#if DVINHOLM_DEVELOPMENT_FEATURES
    [Theory]
    [InlineData("InitializeDevelopmentControls")]
    [InlineData("AddTestProfiles_Click")]
    [InlineData("PreviewUdsSample_Click")]
    [InlineData("ExportOfflineUdsTxt_Click")]
    [InlineData("DemoMonitor_Click")]
    [InlineData("StopDemoMonitor")]
    [InlineData("EmitDemoFrames")]
    [InlineData("ClearDemoData")]
    public void DevelopmentBuild_RetainsOfflineTestUtilities(string method)
    {
        Assert.NotNull(typeof(MainWindow).GetMethod(
            method, BindingFlags.Instance | BindingFlags.NonPublic));
    }
#endif
}
