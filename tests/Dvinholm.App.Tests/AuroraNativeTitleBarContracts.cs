namespace Dvinholm.App.Tests;

/// <summary>
/// Stage 551 offline guards for WinUI 3 native caption-bar painting.
/// These verify source constraints, not actual Windows DWM pixels.
/// </summary>
public sealed class AuroraNativeTitleBarContracts
{
    private static string Source(string relative)
    {
        for (DirectoryInfo? dir = new(AppContext.BaseDirectory);
             dir is not null; dir = dir.Parent)
        {
            var file = Path.Combine(dir.FullName,
                relative.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(file)) return File.ReadAllText(file);
        }
        throw new FileNotFoundException(relative);
    }

    private static string TitleBar() =>
        Source("src/Dvinholm.App/MainWindow.AuroraTitleBar.cs");

    private static string Main() =>
        Source("src/Dvinholm.App/MainWindow.xaml.cs");

    [Fact]
    public void NativeTitleBarIsGuardedOnUnsupportedWindows()
    {
        var code = TitleBar();
        var start = code.IndexOf("private void ConfigureAuroraNativeTitleBar()",
            StringComparison.Ordinal);
        var guard = code.IndexOf("if (!AppWindowTitleBar.IsCustomizationSupported())",
            start, StringComparison.Ordinal);
        var earlyExit = code.IndexOf("return;", guard, StringComparison.Ordinal);
        var nativeBar = code.IndexOf("var titleBar = AppWindow.TitleBar;",
            earlyExit, StringComparison.Ordinal);
        Assert.True(start >= 0 && guard > start && earlyExit > guard &&
            nativeBar > earlyExit);
    }

    [Fact]
    public void ActiveCaptionAndButtonsUseDarkNavyWithLegibleLabels()
    {
        var code = TitleBar();
        Assert.Contains("Color.FromArgb(255, 9, 29, 50)", code);
        Assert.Contains("Color.FromArgb(255, 245, 250, 255)", code);
        Assert.Contains("titleBar.BackgroundColor = midnight;", code);
        Assert.Contains("titleBar.ForegroundColor = label;", code);
        Assert.Contains("titleBar.ButtonBackgroundColor = midnight;", code);
        Assert.Contains("titleBar.ButtonForegroundColor = label;", code);
    }

    [Fact]
    public void HoverAndPressedCaptionButtonsStayLegibleAndSystemManaged()
    {
        var code = TitleBar();
        Assert.Contains("titleBar.ButtonHoverBackgroundColor = hoverNavy;", code);
        Assert.Contains("titleBar.ButtonHoverForegroundColor = label;", code);
        Assert.Contains("titleBar.ButtonPressedBackgroundColor = pressedNavy;", code);
        Assert.Contains("titleBar.ButtonPressedForegroundColor = label;", code);
        Assert.DoesNotContain("Close_Click(", code);
        Assert.DoesNotContain("WindowNative.GetWindowHandle(", code);
    }

    [Fact]
    public void InactiveCaptionRetainsDistinctNavyAndAccessibleLightText()
    {
        var code = TitleBar();
        Assert.Contains("titleBar.InactiveBackgroundColor = inactiveNavy;", code);
        Assert.Contains("titleBar.InactiveForegroundColor = inactiveLabel;", code);
        Assert.Contains("titleBar.ButtonInactiveBackgroundColor = inactiveNavy;", code);
        Assert.Contains("titleBar.ButtonInactiveForegroundColor = inactiveLabel;", code);
    }

    [Fact]
    public void NoCustomChromeThatCouldBreakDragRegionsOrFixedViewport()
    {
        var code = TitleBar();
        foreach (var forbidden in new[]
        {
            "ExtendsContentIntoTitleBar = true",
            "SetTitleBar(", "SetDragRectangles(", "PreferredHeightOption =",
            "AppWindow.Resize(", "AppWindow.Move(", "IsResizable =",
            "IsMaximizable ="
        })
            Assert.DoesNotContain(forbidden, code);
        var main = Main();
        Assert.Contains("presenter.IsResizable = false;", main);
        Assert.Contains("presenter.IsMaximizable = false;", main);
        Assert.Contains("presenter.IsMinimizable = true;", main);
    }

    [Fact]
    public void NativeColorsAreAppliedBeforeFixedDpiWindowSizing()
    {
        var main = Main();
        var ctor = main.IndexOf("public MainWindow()", StringComparison.Ordinal);
        var init = main.IndexOf("InitializeComponent();", ctor, StringComparison.Ordinal);
        var colors = main.IndexOf("ConfigureAuroraNativeTitleBar();", init,
            StringComparison.Ordinal);
        var size = main.IndexOf("ConfigureFixedAuroraWindow();", colors,
            StringComparison.Ordinal);
        var rest = main.IndexOf("AuroraLanding.UseSharedNavigation();", size,
            StringComparison.Ordinal);
        Assert.True(ctor >= 0 && init > ctor && colors > init &&
            size > colors && rest > size);
    }

    [Fact]
    public void TitleIconAndSixScreenAppRemainUnmodified()
    {
        var main = Main();
        var title = TitleBar();
        Assert.Contains("AppWindow.SetIcon(iconPath);", main);
        Assert.Contains("Title = LatestBuildUpdater.CurrentWindowTitle;", main);
        Assert.Contains("DashboardNavigate(null, AuroraDestination.J2534Connection);", main);
        foreach (var forbidden in new[]
        {
            "PassThruOpen(", "PassThruConnect(", "ReadDtc_Click(",
            "ReadCemPin_Click(", "UnifiedStageUpdate_Click(",
            "Invoke-WebRequest", "DeleteStagedArchive("
        })
            Assert.DoesNotContain(forbidden, title);
    }
}
