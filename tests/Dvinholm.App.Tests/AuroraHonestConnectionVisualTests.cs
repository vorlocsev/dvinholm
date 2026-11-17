using System.Xml.Linq;

namespace Dvinholm.App.Tests;

/// <summary>
/// Eight offline contracts: decorative Aurora connection indicators must
/// reflect a real established J2534 session, never a selected driver alone.
/// These do not prove physical adapter behavior or a Windows screenshot.
/// </summary>
public sealed class AuroraHonestConnectionVisualTests
{
    private static readonly XNamespace X =
        "http://schemas.microsoft.com/winfx/2006/xaml";

    private static XDocument Layout()
    {
        using var stream = typeof(AuroraHonestConnectionVisualTests).Assembly
            .GetManifestResourceStream("Dvinholm.App.Tests.MainWindow.xaml")
            ?? throw new InvalidOperationException("MainWindow.xaml missing.");
        return XDocument.Load(stream);
    }

    private static XElement Named(XDocument document, string id) =>
        document.Descendants().Single(element => (string?)element.Attribute(X + "Name") == id);

    private static string Source(string relative)
    {
        for (DirectoryInfo? dir = new(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var path = Path.Combine(dir.FullName,
                relative.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(path)) return File.ReadAllText(path);
        }
        throw new FileNotFoundException(relative);
    }

    [Fact]
    public void HeaderIndicatorStartsNeutralRatherThanFalselyConnected()
    {
        var doc = Layout();
        var dot = Named(doc, "ShellConnectionDot");
        Assert.Equal("●", (string?)dot.Attribute("Text"));
        Assert.Equal("#728FA8", (string?)dot.Attribute("Foreground"));
        Assert.Contains(Named(doc, "WorkspaceHeaderActions"), dot.Ancestors());
        Assert.NotEqual("#2ADE93", (string?)dot.Attribute("Foreground"));
    }

    [Fact]
    public void FooterAndConnectionCardAlsoStartInTheSameNeutralState()
    {
        var doc = Layout();
        Assert.Equal("#728FA8", (string?)Named(doc, "StatusFooterDot").Attribute("Foreground"));
        var card = Named(doc, "ConnectionSessionState");
        Assert.Equal("#728FA8", (string?)card.Attribute("Foreground"));
        Assert.Equal("{Binding Text, ElementName=ShellConnectionText}",
            (string?)card.Attribute("Text"));
        Assert.Equal("Не подключено", (string?)Named(doc, "ShellConnectionText").Attribute("Text"));
    }

    [Fact]
    public void HeaderStatusButtonCanOnlyNavigateToConnectionSettings()
    {
        var doc = Layout();
        var button = Named(doc, "HeaderConnectionStatusButton");
        Assert.Equal("J2534Connection", (string?)button.Attribute("Tag"));
        Assert.Equal("WorkspaceNavigate_Click", (string?)button.Attribute("Click"));
        Assert.Contains(Named(doc, "ShellConnectionText"), button.Descendants());
        Assert.Contains(Named(doc, "ShellConnectionDot"), button.Descendants());
        Assert.DoesNotContain("Connect_Click", button.ToString());
        Assert.DoesNotContain("PassThru", button.ToString());
    }

    [Fact]
    public void OnlyEstablishedJ2534SessionCanColorAllIndicatorsGreen()
    {
        var source = Source("src/Dvinholm.App/MainWindow.xaml.cs");
        var start = source.IndexOf("private void RefreshDashboardSummary()", StringComparison.Ordinal);
        var end = source.IndexOf("private static string ProfileCatalogPath", start, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start);
        var presenter = source[start..end];
        Assert.Contains("var j2534SessionConnected = _connected && _session.IsConnected;", presenter);
        Assert.Contains("j2534SessionConnected", presenter);
        Assert.Contains("Windows.UI.Color.FromArgb(255, 42, 222, 147)", presenter);
        Assert.Contains("Windows.UI.Color.FromArgb(255, 114, 143, 168)", presenter);
        Assert.DoesNotContain("if (adapter is not null)", presenter);
        Assert.DoesNotContain("adapter != null", presenter);
    }

    [Fact]
    public void HeaderFooterAndConnectionCardUseExactlyTheSameBrush()
    {
        var source = Source("src/Dvinholm.App/MainWindow.xaml.cs");
        var start = source.IndexOf("private void RefreshDashboardSummary()", StringComparison.Ordinal);
        var end = source.IndexOf("private static string ProfileCatalogPath", start, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start);
        var presenter = source[start..end];
        Assert.Contains("var connectionBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(", presenter);
        Assert.Contains("StatusFooterDot.Foreground = connectionBrush;", presenter);
        Assert.Contains("ShellConnectionDot.Foreground = connectionBrush;", presenter);
        Assert.Contains("ConnectionSessionState.Foreground = connectionBrush;", presenter);
        Assert.Contains("ShellConnectionText.Text = j2534SessionConnected", presenter);
        Assert.DoesNotContain("ShellConnectionDot.Foreground = new", presenter);
    }

    [Fact]
    public void RussianAndEnglishHeaderStatusHintsStayExplicitAboutNavigation()
    {
        var locale = Source("src/Dvinholm.App/MainWindow.UnifiedUpdates.cs");
        Assert.Contains("ToolTipService.SetToolTip(HeaderConnectionStatusButton, statusNavigationHint);",
            locale);
        Assert.Contains("AutomationProperties.SetName(HeaderConnectionStatusButton, statusNavigationHint);",
            locale);
        Assert.Contains("Open J2534 connection settings. This does not connect to the vehicle.", locale);
        Assert.Contains("Открыть параметры J2534. Это не подключает автомобиль.", locale);
        Assert.Contains("AutomationProperties.SetName(ShellConnectionDot,", locale);
        Assert.Contains("J2534 connection indicator", locale);
    }

    [Fact]
    public void LocalizationNeverChangesOrInventsJ2534Connection()
    {
        var locale = Source("src/Dvinholm.App/MainWindow.UnifiedUpdates.cs");
        Assert.Contains("ShellConnectionText.Text = _connected && _session.IsConnected", locale);
        Assert.DoesNotContain("Connect_Click(", locale);
        Assert.DoesNotContain("PassThruConnect(", locale);
        Assert.DoesNotContain("ReadDtc_Click(", locale);
        var xaml = Layout();
        Assert.Equal("Collapsed", (string?)Named(xaml, "CancelUdsButton").Attribute("Visibility"));
        Assert.Equal("False", (string?)Named(xaml, "CancelUdsButton").Attribute("IsEnabled"));
    }

    [Fact]
    public void HonestStatusRetainsAllSixRoutesAndRealConnectionControls()
    {
        var xml = Layout();
        Assert.Equal(6, Named(xml, "SidebarNavPanel").Elements()
            .Count(e => e.Name.LocalName == "Button"));
        Assert.Contains(Named(xml, "ConnectionPage"),
            Named(xml, "J2534Devices").Ancestors());
        Assert.Contains(Named(xml, "ConnectionPage"),
            Named(xml, "BaudRate").Ancestors());
        Assert.Contains(Named(xml, "ConnectionPage"),
            Named(xml, "ConnectButton").Ancestors());
        Assert.Equal("Connect_Click", (string?)Named(xml, "ConnectButton").Attribute("Click"));
        Assert.DoesNotContain(xml.Descendants(), element =>
            (string?)element.Attribute(X + "Name") is "LauncherPage" or "SettingsPage");
        var names = xml.Descendants().Select(e => (string?)e.Attribute(X + "Name"))
            .Where(name => name is not null).ToArray();
        Assert.Equal(names.Length, names.Distinct(StringComparer.Ordinal).Count());
    }

    // Stage 71: eight source contracts for the bounded, truthful J2534 chip.
    // Layout only; native screenshots and real adapter tests are separate gates.
    [Theory]
    [InlineData("ShellConnectionDot", "0")]
    [InlineData("ShellConnectionText", "1")]
    public void RealStatusGlyphAndTextShareTheSameBoundedGrid(string name, string column)
    {
        var doc = Layout();
        var chip = Named(doc, "HeaderConnectionStatusButton");
        var grid = chip.Elements().Single();
        Assert.Equal("Grid", grid.Name.LocalName);
        var field = Named(doc, name);
        Assert.Contains(grid, field.Ancestors());
        Assert.Equal(column, (string?)field.Attribute("Grid.Column") ?? "0");
        Assert.Null(field.Attribute("Visibility"));
    }

    [Fact]
    public void CompactHeaderStatusChipHasFiniteWidthAndStretchedContent()
    {
        var doc = Layout();
        var chip = Named(doc, "HeaderConnectionStatusButton");
        Assert.Equal("184", (string?)chip.Attribute("MaxWidth"));
        Assert.Equal("Stretch", (string?)chip.Attribute("HorizontalContentAlignment"));
        Assert.Null(chip.Attribute("MinWidth"));
        Assert.Equal("Bottom", (string?)chip.Attribute("VerticalAlignment"));
        Assert.Contains(Named(doc, "WorkspaceHeaderActions"), chip.Ancestors());
    }

    [Fact]
    public void SessionGlyphKeepsAutoColumnAndTextUsesRemainingStarWidth()
    {
        var doc = Layout();
        var grid = Named(doc, "HeaderConnectionStatusButton").Elements().Single();
        Assert.Equal("8", (string?)grid.Attribute("ColumnSpacing"));
        var defs = grid.Elements()
            .Single(element => element.Name.LocalName == "Grid.ColumnDefinitions")
            .Elements().Select(element => (string?)element.Attribute("Width")).ToArray();
        Assert.Equal(new[] { "Auto", "*" }, defs);
        Assert.Equal("Center",
            (string?)Named(doc, "ShellConnectionDot").Attribute("VerticalAlignment"));
    }

    [Fact]
    public void J2534TextWrapsWithinFiniteWidthWithoutEllipsis()
    {
        var status = Named(Layout(), "ShellConnectionText");
        Assert.Equal("Не подключено", (string?)status.Attribute("Text"));
        Assert.Equal("Wrap", (string?)status.Attribute("TextWrapping"));
        Assert.Equal("128", (string?)status.Attribute("MaxWidth"));
        Assert.Equal("Center", (string?)status.Attribute("VerticalAlignment"));
        Assert.Null(status.Attribute("TextTrimming"));
        Assert.Null(status.Attribute("MaxLines"));
    }

    [Fact]
    public void RealConnectionStateStillGatesHeaderTextAndColor()
    {
        var app = Source("src/Dvinholm.App/MainWindow.xaml.cs");
        var locale = Source("src/Dvinholm.App/MainWindow.UnifiedUpdates.cs");
        Assert.Contains("var j2534SessionConnected = _connected && _session.IsConnected;", app);
        Assert.Contains("ShellConnectionText.Text = j2534SessionConnected", app);
        Assert.Contains("ShellConnectionDot.Foreground = connectionBrush;", app);
        Assert.Contains("StatusFooterDot.Foreground = connectionBrush;", app);
        Assert.Contains("ShellConnectionText.Text = _connected && _session.IsConnected", locale);
        Assert.Contains("L(\"Не подключено\", \"Disconnected\")", locale);
        Assert.Contains("L(\"J2534 подключён\", \"J2534 connected\")", locale);
    }

    [Fact]
    public void WrappingDoesNotTurnHeaderNavigationIntoConnectOperation()
    {
        var doc = Layout();
        var chip = Named(doc, "HeaderConnectionStatusButton");
        Assert.Equal("J2534Connection", (string?)chip.Attribute("Tag"));
        Assert.Equal("WorkspaceNavigate_Click", (string?)chip.Attribute("Click"));
        Assert.Equal("DvinholmHeaderConnectionStatus",
            (string?)chip.Attribute("AutomationProperties.AutomationId"));
        Assert.DoesNotContain("Connect_Click", chip.ToString());
        Assert.DoesNotContain("PassThruConnect", chip.ToString());
        var actions = Named(doc, "WorkspaceHeaderActions").Elements()
            .Select(element => (string?)element.Attribute(X + "Name")).ToArray();
        Assert.Equal(new[] { "HeaderConnectionStatusButton",
            "UnifiedLanguageSelector", "CancelUdsButton" }, actions);
    }

    [Fact]
    public void CompactAndWideHeaderStillPreserveLanguageAndCancellationGates()
    {
        var doc = Layout();
        var compact = Named(doc, "ShellCompact").Descendants()
            .Where(element => element.Name.LocalName == "Setter")
            .ToDictionary(element => (string)element.Attribute("Target")!,
                element => (string)element.Attribute("Value")!);
        Assert.Equal("Horizontal", compact["WorkspaceHeaderActions.Orientation"]);
        Assert.Equal("1", compact["WorkspaceHeaderActions.(Grid.Row)"]);
        Assert.Equal("1280", (string?)Named(doc, "HeaderActionsInline").Descendants()
            .Single(element => element.Name.LocalName == "AdaptiveTrigger")
            .Attribute("MinWindowWidth"));
        Assert.Equal("ru", (string?)Named(doc, "UnifiedLanguageSelector")
            .Elements().First().Attribute("Tag"));
        Assert.Equal("Collapsed",
            (string?)Named(doc, "CancelUdsButton").Attribute("Visibility"));
        Assert.Equal("False",
            (string?)Named(doc, "CancelUdsButton").Attribute("IsEnabled"));
        Assert.Equal(6, Named(doc, "SidebarNavPanel").Elements()
            .Count(element => element.Name.LocalName == "Button"));
    }
}
