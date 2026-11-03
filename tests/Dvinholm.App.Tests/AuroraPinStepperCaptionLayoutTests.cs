using System.Xml.Linq;

namespace Dvinholm.App.Tests;

/// <summary>
/// Eight offline checks for bounded RU/EN CEM PIN stepper labels at narrow widths.
/// These do not render native Windows pixels or imply operational PIN support.
/// </summary>
public sealed class AuroraPinStepperCaptionLayoutTests
{
    private static readonly XNamespace X =
        "http://schemas.microsoft.com/winfx/2006/xaml";

    private static XDocument Layout()
    {
        using var stream = typeof(AuroraPinStepperCaptionLayoutTests).Assembly
            .GetManifestResourceStream("Dvinholm.App.Tests.MainWindow.xaml")
            ?? throw new InvalidOperationException("Embedded MainWindow.xaml missing.");
        return XDocument.Load(stream);
    }

    private static XElement Named(XDocument doc, string id) =>
        doc.Descendants().Single(e => (string?)e.Attribute(X + "Name") == id);

    private static XElement StepperGrid(XDocument doc) =>
        Named(doc, "CemPinStepper").Elements()
            .Single(e => e.Name.LocalName == "Grid");

    private static string Source(string relative)
    {
        for (DirectoryInfo? directory = new(AppContext.BaseDirectory);
             directory is not null; directory = directory.Parent)
        {
            var file = Path.Combine(directory.FullName,
                relative.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(file)) return File.ReadAllText(file);
        }
        throw new FileNotFoundException(relative);
    }

    [Fact]
    public void PinStepperKeepsThreeEqualAdaptiveColumns()
    {
        var doc = Layout();
        var grid = StepperGrid(doc);
        var columns = grid.Elements()
            .Single(e => e.Name.LocalName == "Grid.ColumnDefinitions").Elements().ToArray();
        Assert.Equal(3, columns.Length);
        Assert.All(columns, column => Assert.Equal("*", (string?)column.Attribute("Width")));
        Assert.Equal("8", (string?)grid.Attribute("ColumnSpacing"));
        Assert.Null(grid.Attribute("MinWidth"));
    }

    [Fact]
    public void EveryStageNowStretchesWithinOwnFiniteGridColumn()
    {
        var grid = StepperGrid(Layout());
        var stages = grid.Elements().Where(e => e.Name.LocalName == "StackPanel").ToArray();
        Assert.Equal(3, stages.Length);
        Assert.Equal(new[] { "0", "1", "2" },
            stages.Select(e => (string?)e.Attribute("Grid.Column")));
        Assert.All(stages, stage =>
        {
            Assert.Equal("Stretch", (string?)stage.Attribute("HorizontalAlignment"));
            Assert.Equal("8", (string?)stage.Attribute("Spacing"));
            Assert.Single(stage.Elements().Where(e => e.Name.LocalName == "TextBlock"));
        });
    }

    [Fact]
    public void ThreeRuEnCaptionsWrapInsideTheirOwnColumn()
    {
        var doc = Layout();
        foreach (var id in new[] { "CemPinStepIdentity", "CemPinStepResearch",
            "CemPinStepResult" })
        {
            var label = Named(doc, id);
            Assert.Equal("Wrap", (string?)label.Attribute("TextWrapping"));
            Assert.Equal("Stretch", (string?)label.Attribute("HorizontalAlignment"));
            Assert.Equal("4,0", (string?)label.Attribute("Margin"));
            Assert.Equal("Center", (string?)label.Attribute("TextAlignment"));
            Assert.Equal("12", (string?)label.Attribute("FontSize"));
            Assert.Null(label.Attribute("MaxLines"));
        }
    }

    [Fact]
    public void ActiveAndInactiveStageBadgesStayCenteredAndNeverClaimCompletion()
    {
        var stages = StepperGrid(Layout()).Elements()
            .Where(e => e.Name.LocalName == "StackPanel").ToArray();
        Assert.Equal(new[] { "1", "2", "3" },
            stages.Select(stage => (string?)stage.Elements()
                .Single(e => e.Name.LocalName == "Border")
                .Descendants().Single(e => e.Name.LocalName == "TextBlock")
                .Attribute("Text")));
        Assert.All(stages, stage =>
        {
            var border = stage.Elements().Single(e => e.Name.LocalName == "Border");
            Assert.Equal("40", (string?)border.Attribute("Width"));
            Assert.Equal("40", (string?)border.Attribute("Height"));
            Assert.Equal("Center", (string?)border.Attribute("HorizontalAlignment"));
        });
        Assert.DoesNotContain("100%", Named(Layout(), "CemPinStepper").ToString());
    }

    [Fact]
    public void DecorativeTrackRemainsBehindAllThreeStagesAndCannotCaptureClicks()
    {
        var doc = Layout();
        var grid = StepperGrid(doc);
        var children = grid.Elements().ToArray();
        var track = Named(doc, "CemPinStepperTrack");
        Assert.Equal("False", (string?)track.Attribute("IsHitTestVisible"));
        Assert.Equal("3", (string?)track.Attribute("Grid.ColumnSpan"));
        Assert.Equal("2", (string?)track.Attribute("Height"));
        Assert.True(Array.IndexOf(children, track) <
            Array.FindIndex(children, e => e.Name.LocalName == "StackPanel"));
        Assert.Empty(grid.Descendants().Where(e => e.Attribute("Click") is not null));
    }

    [Fact]
    public void RussianAndEnglishCaptionsPreserveTruthfulUnavailableStatus()
    {
        var locale = Source("src/Dvinholm.App/MainWindow.AuroraScreenLocalization.cs");
        Assert.Contains("CemPinStepIdentity.Text = L(\"Идентификация\", \"Identification\");", locale);
        Assert.Contains("CemPinStepResearch.Text = L(\"Поиск PIN недоступен\", \"PIN search unavailable\");", locale);
        Assert.Contains("CemPinStepResult.Text = L(\"Нет результата\", \"No result\");", locale);
        Assert.DoesNotContain("ReadCemPin_Click(", locale);
        Assert.DoesNotContain("PassThru", locale);
    }

    [Fact]
    public void CompactStepperDoesNotEnableUnverifiedPinExtraction()
    {
        var doc = Layout();
        var page = Named(doc, "CemPinPage");
        Assert.Contains(page, Named(doc, "CemPinStepper").Ancestors());
        Assert.Equal("Collapsed", (string?)Named(doc, "CemPinSearchCard")
            .Attribute("Visibility"));
        Assert.Equal("Collapsed", (string?)Named(doc, "CemPinLivePanel")
            .Attribute("Visibility"));
        Assert.Equal("False", (string?)Named(doc, "CemPinReadButton")
            .Attribute("IsEnabled"));
        Assert.DoesNotContain("4387", Named(doc, "CemPinStepper").ToString());
    }

    [Fact]
    public void SafeVisualChangePreservesSixRoutesAndVerticalWorkspaceScroll()
    {
        var doc = Layout();
        Assert.Equal("Disabled", (string?)Named(doc, "WorkspaceScroll")
            .Attribute("HorizontalScrollBarVisibility"));
        Assert.Equal(6, Named(doc, "SidebarNavPanel").Elements()
            .Count(e => e.Name.LocalName == "Button"));
        Assert.DoesNotContain(doc.Descendants(), e =>
            (string?)e.Attribute(X + "Name") is "SettingsPage" or "LauncherPage");
        var named = doc.Descendants()
            .Select(e => (string?)e.Attribute(X + "Name"))
            .Where(n => n is not null).ToArray();
        Assert.Equal(named.Length, named.Distinct(StringComparer.Ordinal).Count());
    }
}
