using System.Xml.Linq;

namespace Dvinholm.App.Tests;

/// <summary>
/// Eight offline Aurora caption-width contracts for the monitor and
/// diagnostic-result cards. Source tests only, not rendered Windows pixels.
/// </summary>
public sealed class AuroraMonitorResultCaptionLayoutTests
{
    private static readonly XNamespace X =
        "http://schemas.microsoft.com/winfx/2006/xaml";

    private static XDocument Layout()
    {
        using var resource = typeof(AuroraMonitorResultCaptionLayoutTests).Assembly
            .GetManifestResourceStream("Dvinholm.App.Tests.MainWindow.xaml")
            ?? throw new InvalidOperationException("Missing embedded MainWindow.xaml.");
        return XDocument.Load(resource);
    }

    private static XElement Named(XDocument doc, string id) =>
        doc.Descendants().Single(e => (string?)e.Attribute(X + "Name") == id);

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

    [Theory]
    [InlineData("CanMonitorHeading", "CanMonitorPage")]
    [InlineData("CanMonitorIntro", "CanMonitorPage")]
    [InlineData("DiagnosticResultHeading", "DiagnosticResultsCard")]
    [InlineData("DiagnosticResultIntro", "DiagnosticResultsCard")]
    public void RealSectionHeadingAndLongIntroCanWrapWithinTheirCard(
        string label, string container)
    {
        var doc = Layout();
        var text = Named(doc, label);
        Assert.Equal("Wrap", (string?)text.Attribute("TextWrapping"));
        Assert.Contains(Named(doc, container), text.Ancestors());
        Assert.Null(text.Attribute("Width"));
        Assert.Null(text.Attribute("MinWidth"));
        Assert.Null(text.Attribute("MaxWidth"));
        Assert.Null(text.Attribute("TextTrimming"));
    }

    [Fact]
    public void MonitorPrimaryActionCanWrapRussianOrEnglishLabelWithoutChangingHandler()
    {
        var doc = Layout();
        var start = Named(doc, "StartButton");
        Assert.Equal("Start_Click", (string?)start.Attribute("Click"));
        Assert.Equal("False", (string?)start.Attribute("IsEnabled"));
        Assert.Equal("{StaticResource AuroraWrapCaption}",
            (string?)start.Attribute("ContentTemplate"));
        Assert.Contains(Named(doc, "CanMonitorPage"), start.Ancestors());
        var locale = Source("src/Dvinholm.App/MainWindow.AuroraScreenLocalization.cs");
        Assert.Contains("StartButton.Content =", locale);
        Assert.Contains("Начать мониторинг", locale);
        Assert.Contains("Start monitor", locale);
    }

    [Fact]
    public void MonitorDescriptionIsBilingualAndDoesNotTouchBusInLocaleProjection()
    {
        var doc = Layout();
        Assert.True(((string?)Named(doc, "CanMonitorIntro").Attribute("Text"))!.Length > 70);
        var locale = Source("src/Dvinholm.App/MainWindow.AuroraScreenLocalization.cs");
        Assert.Contains("CanMonitorIntro.Text = L(", locale);
        Assert.Contains("Start and stop monitoring independently", locale);
        Assert.DoesNotContain("PassThruWriteMsgs(", locale);
        Assert.DoesNotContain("_session.Bus", locale);
    }

    [Fact]
    public void DiagnosticResultDescriptionIsBilingualAndUsesExistingPrivacyGate()
    {
        var doc = Layout();
        var card = Named(doc, "DiagnosticResultsCard");
        Assert.Equal("Collapsed", (string?)card.Attribute("Visibility"));
        Assert.Contains(Named(doc, "DiagnosticResultIntro"), card.Descendants());
        var locale = Source("src/Dvinholm.App/MainWindow.AuroraScreenLocalization.cs");
        Assert.Contains("DiagnosticResultIntro.Text = L(", locale);
        Assert.Contains("Data stays local. Review it before sharing.", locale);
        Assert.Contains("DiagnosticResultExpander.Header = L(", locale);
    }

    [Fact]
    public void WrappingDoesNotEnableUnsafeButtonsOrHorizontalWorkspaceScroll()
    {
        var doc = Layout();
        Assert.Equal("Disabled", (string?)Named(doc, "WorkspaceScroll")
            .Attribute("HorizontalScrollBarVisibility"));
        Assert.Equal("False", (string?)Named(doc, "HardwareFilterButton")
            .Attribute("IsEnabled"));
        Assert.Equal("False", (string?)Named(doc, "DtcClearButton")
            .Attribute("IsEnabled"));
        Assert.Null(Named(doc, "DtcClearButton").Attribute("Click"));
        Assert.Equal(6, Named(doc, "SidebarNavPanel").Elements()
            .Count(e => e.Name.LocalName == "Button"));
    }
}
