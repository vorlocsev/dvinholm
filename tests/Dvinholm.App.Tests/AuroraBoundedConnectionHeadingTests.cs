using System.Xml.Linq;

namespace Dvinholm.App.Tests;

/// <summary>
/// Eight offline source contracts: RU/EN Connection headings must be measured
/// inside bounded Grid star columns, not unconstrained horizontal StackPanels.
/// They do not replace native Windows visual screenshot acceptance.
/// </summary>
public sealed class AuroraBoundedConnectionHeadingTests
{
    private static readonly XNamespace X =
        "http://schemas.microsoft.com/winfx/2006/xaml";

    private static readonly (string Pane, string Header, string Text, string Glyph)[] Groups =
    {
        ("ConnectionAdapterPane", "ConnectionAdapterHeadingGrid", "ConnectionPageHeading", "&#xE950;"),
        ("ConnectionBusPane", "ConnectionBusHeadingGrid", "ConnectionBusHeading", "&#xE713;"),
        ("ConnectionStatusPane", "ConnectionDriverHeadingGrid", "ConnectionDriverHeading", "&#xE753;")
    };

    private static XDocument Layout()
    {
        using var stream = typeof(AuroraBoundedConnectionHeadingTests).Assembly
            .GetManifestResourceStream("Dvinholm.App.Tests.MainWindow.xaml")
            ?? throw new InvalidOperationException("Missing embedded MainWindow.xaml.");
        return XDocument.Load(stream);
    }

    private static XElement Named(XDocument document, string name) =>
        document.Descendants().Single(e => (string?)e.Attribute(X + "Name") == name);

    private static IReadOnlyDictionary<string, string> Setters(XDocument document, string state) =>
        Named(document, state).Descendants()
            .Where(e => e.Name.LocalName == "Setter")
            .ToDictionary(e => (string)e.Attribute("Target")!,
                e => (string)e.Attribute("Value")!, StringComparer.Ordinal);

    private static string Source(string relative)
    {
        for (DirectoryInfo? dir = new(AppContext.BaseDirectory);
             dir is not null; dir = dir.Parent)
        {
            var path = Path.Combine(dir.FullName,
                relative.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(path)) return File.ReadAllText(path);
        }
        throw new FileNotFoundException(relative);
    }

    [Fact]
    public void AllThreeHeadersAreOwnedByTheirRealConnectionCards()
    {
        var doc = Layout();
        foreach (var (pane, header, title, _) in Groups)
        {
            Assert.Contains(Named(doc, pane), Named(doc, header).Ancestors());
            Assert.Contains(Named(doc, header), Named(doc, title).Ancestors());
            Assert.Equal("Grid", Named(doc, header).Name.LocalName);
        }
    }

    [Fact]
    public void AllThreeUseFiniteAutoIconAndStretchTextColumns()
    {
        var doc = Layout();
        foreach (var (_, header, _, _) in Groups)
        {
            var grid = Named(doc, header);
            Assert.Equal("10", (string?)grid.Attribute("ColumnSpacing"));
            Assert.Null(grid.Attribute("MinWidth"));
            var cols = grid.Elements()
                .Single(e => e.Name.LocalName == "Grid.ColumnDefinitions")
                .Elements().ToArray();
            Assert.Equal(new[] { "Auto", "*" },
                cols.Select(e => (string?)e.Attribute("Width")));
        }
    }

    [Fact]
    public void RealLabelsLiveInConstrainedSecondColumnsAndWrapWithoutClamp()
    {
        var doc = Layout();
        foreach (var (_, _, title, _) in Groups)
        {
            var text = Named(doc, title);
            Assert.Equal("1", (string?)text.Attribute("Grid.Column"));
            Assert.Equal("Center", (string?)text.Attribute("VerticalAlignment"));
            Assert.Equal("Wrap", (string?)text.Attribute("TextWrapping"));
            Assert.Equal("19", (string?)text.Attribute("FontSize"));
            Assert.Null(text.Attribute("MaxLines"));
            Assert.Null(text.Attribute("TextTrimming"));
        }
    }

    [Fact]
    public void StatusAdapterAndBusIconsRemainNoninteractiveWithOriginalIdentity()
    {
        var doc = Layout();
        var glyphs = new[] { "\uE950", "\uE713", "\uE753" };
        for (var i = 0; i < Groups.Length; i++)
        {
            var grid = Named(doc, Groups[i].Header);
            var icon = grid.Elements().Single(e => e.Name.LocalName == "FontIcon");
            Assert.Equal(glyphs[i], (string?)icon.Attribute("Glyph"));
            Assert.Equal("21", (string?)icon.Attribute("FontSize"));
            Assert.Equal("#55CEED", (string?)icon.Attribute("Foreground"));
            Assert.Null(icon.Attribute("Click"));
            Assert.Null(grid.Attribute("Click"));
        }
    }

    [Fact]
    public void SwitchingFromThreeToTwoToOneColumnsPreservesHeadings()
    {
        var doc = Layout();
        Assert.Equal("1280", (string?)Named(doc, "ConnectionThreeColumns")
            .Descendants().Single(e => e.Name.LocalName == "AdaptiveTrigger")
            .Attribute("MinWindowWidth"));
        Assert.Equal("980", (string?)Named(doc, "ConnectionTwoColumns")
            .Descendants().Single(e => e.Name.LocalName == "AdaptiveTrigger")
            .Attribute("MinWindowWidth"));
        Assert.Equal("0", (string?)Named(doc, "ConnectionVerticalPanels")
            .Descendants().Single(e => e.Name.LocalName == "AdaptiveTrigger")
            .Attribute("MinWindowWidth"));
        var mid = Setters(doc, "ConnectionTwoColumns");
        Assert.Equal("2", mid["ConnectionStatusPane.(Grid.ColumnSpan)"]);
        Assert.Equal("1", mid["ConnectionStatusPane.(Grid.Row)"]);
        Assert.Equal("Disabled", (string?)Named(doc, "WorkspaceScroll")
            .Attribute("HorizontalScrollBarVisibility"));
    }

    [Fact]
    public void LocalizedConnectionHeadingsStillReceiveBothLanguages()
    {
        var source = Source("src/Dvinholm.App/MainWindow.AuroraScreenLocalization.cs");
        Assert.Contains("ConnectionPageHeading.Text = L(", source);
        Assert.Contains("ConnectionBusHeading.Text = L(", source);
        Assert.Contains("ConnectionDriverHeading.Text = L(", source);
        Assert.Contains("Vehicle interface", source);
        Assert.Contains("CAN parameters", source);
        Assert.Contains("Adapter information", source);
        Assert.DoesNotContain("Connect_Click(", source);
        Assert.DoesNotContain("PassThruConnect(", source);
    }

    [Fact]
    public void RealDeviceStateAndUserConnectHandlerAreNotReplacedWithArtwork()
    {
        var doc = Layout();
        Assert.Equal("J2534Device_SelectionChanged",
            (string?)Named(doc, "J2534Devices").Attribute("SelectionChanged"));
        Assert.Equal("Connect_Click",
            (string?)Named(doc, "ConnectButton").Attribute("Click"));
        Assert.Equal("{Binding Text, ElementName=ShellConnectionText}",
            (string?)Named(doc, "ConnectionSessionState").Attribute("Text"));
        Assert.Contains("disconnected",
            (string?)Named(doc, "AdapterMetadataText").Attribute("Text"),
            StringComparison.OrdinalIgnoreCase);
        Assert.Equal("#728FA8",
            (string?)Named(doc, "ConnectionSessionState").Attribute("Foreground"));
    }

    [Fact]
    public void LayoutOnlyChangeCannotEnableUnsafeVehicleOrUpdateOperations()
    {
        var doc = Layout();
        foreach (var id in new[] { "UnifiedStageUpdateButton",
            "UnifiedCancelStageButton", "UnifiedPreflightButton",
            "CemConfigWriteButton", "DtcClearButton" })
        {
            Assert.Equal("False", (string?)Named(doc, id).Attribute("IsEnabled"));
        }

        Assert.Equal(6, Named(doc, "SidebarNavPanel").Elements()
            .Count(e => e.Name.LocalName == "Button"));
        Assert.DoesNotContain(doc.Descendants(), e =>
            (string?)e.Attribute(X + "Name") is "SettingsPage" or "LauncherPage");
        var named = doc.Descendants().Select(e => (string?)e.Attribute(X + "Name"))
            .Where(s => s is not null).ToArray();
        Assert.Equal(named.Length, named.Distinct(StringComparer.Ordinal).Count());
    }
}
