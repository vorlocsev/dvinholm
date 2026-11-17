using System.Xml.Linq;

namespace Dvinholm.App.Tests;

/// <summary>
/// Eight offline Aurora CEM visual/safety gates. These assert the actual WinUI
/// layout and label projection, not Windows screenshots or hardware capability.
/// </summary>
public sealed class AuroraCemVisualParityTests
{
    private static readonly XNamespace X =
        "http://schemas.microsoft.com/winfx/2006/xaml";

    private static XDocument Layout()
    {
        using var stream = typeof(AuroraCemVisualParityTests).Assembly
            .GetManifestResourceStream("Dvinholm.App.Tests.MainWindow.xaml")
            ?? throw new InvalidOperationException("Embedded MainWindow.xaml missing.");
        return XDocument.Load(stream);
    }

    private static XElement Named(XDocument doc, string name) =>
        doc.Descendants().Single(e => (string?)e.Attribute(X + "Name") == name);

    private static Dictionary<string, string> Setters(XDocument doc, string state)
    {
        var visual = Named(doc, state);
        return visual.Descendants().Where(e => e.Name.LocalName == "Setter")
            .ToDictionary(e => (string)e.Attribute("Target")!,
                e => (string)e.Attribute("Value")!, StringComparer.Ordinal);
    }

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
    public void PinStepperTrackIsDecorativeAndDoesNotShowPinProgress()
    {
        var doc = Layout();
        var stepper = Named(doc, "CemPinStepper");
        var track = Named(doc, "CemPinStepperTrack");
        Assert.Contains(track, stepper.Descendants());
        Assert.Equal("3", (string?)track.Attribute("Grid.ColumnSpan"));
        Assert.Equal("False", (string?)track.Attribute("IsHitTestVisible"));
        Assert.Null(track.Attribute("Click"));
        Assert.Equal("Collapsed",
            (string?)Named(doc, "CemPinSearchCard").Attribute("Visibility"));
        Assert.DoesNotContain("68%", stepper.ToString());

        // Development may explicitly reveal this panel. Long bilingual text
        // must wrap rather than clip, and a locale switch must change labels
        // without clearing a PIN result or changing the search attempt range.
        Assert.Equal("Wrap", (string?)Named(doc, "CemPinSearchHeading")
            .Attribute("TextWrapping"));
        Assert.Equal("Wrap", (string?)Named(doc, "CemPinSearchIntro")
            .Attribute("TextWrapping"));
        var locale = Source("src/Dvinholm.App/MainWindow.CemPin.cs");
        var start = locale.IndexOf("private void RefreshCemDevelopmentLabels()", StringComparison.Ordinal);
        var end = locale.IndexOf("private CemReadOnlyPrerequisiteAssessment", start, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start);
        var labelsOnly = locale[start..end];
        foreach (var name in new[] { "CemPinSearchHeading.Text", "CemPinSearchIntro.Text",
            "CemPinRangeStart.Header", "CemPinRangeCount.Header",
            "CemPinReadButton.Content", "CemPinRevealButton.Content",
            "CemPinSavedButton.Content", "CemPartNumberReadButton.Content" })
            Assert.Contains(name, labelsOnly);
        Assert.Contains("Experimental PIN search", labelsOnly);
        Assert.Contains("Результат эксперимента не подтверждает PIN", labelsOnly);
        Assert.DoesNotContain("CemPinResultText.Text", labelsOnly);
        Assert.DoesNotContain("PassThru", labelsOnly);
        Assert.DoesNotContain("SendAsync", labelsOnly);
        Assert.Contains("RefreshCemDevelopmentLabels();",
            Source("src/Dvinholm.App/MainWindow.UnifiedUpdates.cs"));
    }

    [Fact]
    public void ConfigurationResearchRetainsThreeOrderedResponsivePanes()
    {
        var doc = Layout();
        var grid = Named(doc, "CemReferenceGrid");
        Assert.Null(grid.Attribute("MinWidth"));
        var panes = grid.Elements().Where(e => e.Name.LocalName == "Border").ToArray();
        Assert.Equal(new[] { "CemReferenceCategoryPane", "CemReferenceValuesPane",
            "CemReferenceDetailPane" },
            panes.Select(e => (string?)e.Attribute(X + "Name")));
        Assert.Equal(new[] { "0", "1", "2" },
            panes.Select(e => (string?)e.Attribute("Grid.Column")));
    }

    [Fact]
    public void TableShowsFourRealResearchColumnsNotInventedVehicleFields()
    {
        var doc = Layout();
        var table = Named(doc, "CemConfigResults");
        var header = table.Descendants().Single(e => e.Name.LocalName == "ListView.Header");
        var grid = header.Elements().Single(e => e.Name.LocalName == "Grid");
        Assert.Equal(4, grid.Descendants()
            .Count(e => e.Name.LocalName == "ColumnDefinition"));
        foreach (var (name, col) in new[] {
            ("CemConfigListParameterHeader", "0"),
            ("CemConfigListInitialHeader", "1"),
            ("CemConfigListCurrentHeader", "2"),
            ("CemConfigListProposedHeader", "3") })
        {
            var field = Named(doc, name);
            Assert.Contains(field, header.Descendants());
            Assert.Equal(col, (string?)field.Attribute("Grid.Column") ?? "0");
        }
        var template = table.Descendants()
            .Single(e => e.Name.LocalName == "DataTemplate").ToString();
        Assert.Contains("{Binding Initial}", template);
        Assert.Contains("{Binding Current}", template);
        Assert.Contains("{Binding Proposed}", template);
        Assert.DoesNotContain("{Binding Vin}", template);
    }

    [Fact]
    public void BaselineAndCurrentUseOnlyExplicitUnknownValues()
    {
        var projection = Source("src/Dvinholm.App/MainWindow.AuroraReference.cs");
        Assert.Contains("public string Initial => English ? \"No backup\" : \"Нет копии\";", projection);
        Assert.Contains("public string Current => English ? \"Not read\" : \"Не прочитано\";", projection);
        Assert.Contains("public string Proposed => \"—\";", projection);
        Assert.DoesNotContain("PassThru", projection);
        Assert.DoesNotContain("SendAsync(", projection);
        Assert.DoesNotContain("WriteMemory(", projection);
    }

    [Fact]
    public void ActionsFollowResearchPanesAsFooterNotTopToolbar()
    {
        var doc = Layout();
        var page = Named(doc, "CemConfigPage");
        var children = page.ToString();
        Assert.True(children.IndexOf("x:Name=\"CemReferenceGrid\"", StringComparison.Ordinal) <
                    children.IndexOf("x:Name=\"CemConfigActionRail\"", StringComparison.Ordinal));
        Assert.True(children.IndexOf("x:Name=\"CemConfigActionRail\"", StringComparison.Ordinal) <
                    children.IndexOf("x:Name=\"CemConfigReadOnlyNotice\"", StringComparison.Ordinal));
        Assert.Contains(Named(doc, "CemConfigActionsGrid"),
            Named(doc, "CemConfigActionRail").Descendants());
    }

    [Fact]
    public void ReadWriteBackupCompareAndPrepareButtonsAreActuallyInert()
    {
        var doc = Layout();
        foreach (var name in new[] { "CemConfigReadButton", "CemConfigBackupButton",
            "CemConfigCompareButton", "CemConfigPrepareButton", "CemConfigWriteButton" })
        {
            var button = Named(doc, name);
            Assert.Equal("Button", button.Name.LocalName);
            Assert.Equal("False", (string?)button.Attribute("IsEnabled"));
            Assert.Null(button.Attribute("Click"));
            Assert.NotNull(button.Attribute("ToolTipService.ToolTip"));
        }
    }

    [Fact]
    public void OnlyResearchSourcesRetainAnEnabledHandler()
    {
        var doc = Layout();
        var grid = Named(doc, "CemConfigActionsGrid");
        var actions = grid.Elements().Where(e => e.Name.LocalName == "Button").ToArray();
        Assert.Equal(6, actions.Length);
        Assert.Single(actions, e => e.Attribute("Click") is not null);
        Assert.Equal("P2ConfigCatalog_Click",
            (string?)Named(doc, "P2ConfigCatalogButton").Attribute("Click"));
        Assert.DoesNotContain(actions, e =>
            (string?)e.Attribute("Content") is "Install" or "Erase");
    }

    [Fact]
    public void CompactFooterAndRuEnLabelsKeepSafetyInvariant()
    {
        var doc = Layout();
        var compact = Setters(doc, "CemReferenceStacked");
        Assert.Equal("0", compact["CemConfigActionSixthColumn.Width"]);
        Assert.Equal("1", compact["CemConfigCompareButton.(Grid.Row)"]);
        Assert.Equal("2", compact["CemConfigPrepareButton.(Grid.Row)"]);
        Assert.Equal("2", compact["CemConfigWriteButton.(Grid.Row)"]);
        var locale = Source("src/Dvinholm.App/MainWindow.AuroraReference.cs");
        foreach (var control in new[] { "CemConfigListInitialHeader",
            "CemConfigListCurrentHeader", "CemConfigListProposedHeader",
            "CemConfigReadButton", "CemConfigWriteButton",
            "CemConfigActionsHeading" })
            Assert.Contains(control + ".", locale);
        Assert.Contains("ApplyAuroraCemConfigLocalization();",
            Source("src/Dvinholm.App/MainWindow.UnifiedUpdates.cs"));
        Assert.DoesNotContain("PassThru", locale);
    }

    [Theory]
    [InlineData("CemPartNumberReadButton")]
    [InlineData("CemPartPrerequisiteButton")]
    [InlineData("CemPinReadButton")]
    [InlineData("CemPinRevealButton")]
    [InlineData("CemPinSavedButton")]
    [InlineData("P2PinReviewButton")]
    public void LongCemPinActionCaptionsWrapAtTightAuroraWidths(string controlName)
    {
        var doc = Layout();
        var button = Named(doc, controlName);
        Assert.Equal("Button", button.Name.LocalName);
        Assert.Equal("{StaticResource AuroraWrapCaption}",
            (string?)button.Attribute("ContentTemplate"));
        var template = doc.Descendants().Single(e =>
            e.Name.LocalName == "DataTemplate" &&
            (string?)e.Attribute(X + "Key") == "AuroraWrapCaption");
        Assert.Equal("Wrap", (string?)template.Descendants()
            .Single(e => e.Name.LocalName == "TextBlock").Attribute("TextWrapping"));
    }

    [Fact]
    public void CemExperimentalTooltipsAreBilingualAndDoNotChangeVehicleState()
    {
        var source = Source("src/Dvinholm.App/MainWindow.CemPin.cs");
        var begin = source.IndexOf("private void RefreshCemDevelopmentLabels()", StringComparison.Ordinal);
        var end = source.IndexOf("private CemReadOnlyPrerequisiteAssessment", begin, StringComparison.Ordinal);
        Assert.True(begin >= 0 && end > begin);
        var projection = source[begin..end];
        foreach (var name in new[] { "CemPartNumberReadButton",
            "CemPinReadButton", "CemPinRevealButton", "CemPinSavedButton" })
            Assert.Contains("ToolTipService.SetToolTip(" + name, projection);
        Assert.Contains("Физическая совместимость", projection);
        Assert.Contains("Physical compatibility", projection);
        Assert.DoesNotContain("CemPinResultText.Text", projection);
        Assert.DoesNotContain("PassThru", projection);
        Assert.DoesNotContain("ReadCemPin_Click(", projection);
        Assert.DoesNotContain("SendAsync(", projection);
    }

    [Fact]
    public void LongRussianCemCategoryLabelsWrapWithoutChangingTheCatalogFilter()
    {
        var doc = Layout();
        var grid = Named(doc, "CemReferenceGrid");
        var definitions = grid.Elements().Single(e => e.Name.LocalName == "Grid.ColumnDefinitions");
        var columns = definitions.Elements().ToArray();
        Assert.Equal("220", (string?)columns[0].Attribute("Width"));
        Assert.Equal("*", (string?)columns[1].Attribute("Width"));
        Assert.Equal("240", (string?)columns[2].Attribute("Width"));

        var combo = Named(doc, "CemConfigCategory");
        Assert.Equal("CemConfigCategory_SelectionChanged",
            (string?)combo.Attribute("SelectionChanged"));
        Assert.Equal("Stretch", (string?)combo.Attribute("HorizontalAlignment"));
        var template = combo.Descendants().Single(e => e.Name.LocalName == "DataTemplate");
        var caption = template.Descendants().Single(e => e.Name.LocalName == "TextBlock");
        Assert.Equal("{Binding Label}", (string?)caption.Attribute("Text"));
        Assert.Equal("Wrap", (string?)caption.Attribute("TextWrapping"));

        // Localized items change only their label; the stable canonical
        // category still drives filtering and the selected research ID.
        var source = Source("src/Dvinholm.App/MainWindow.AuroraReference.cs");
        Assert.Contains("selectedCategory = (CemConfigCategory.SelectedItem as", source);
        Assert.Contains("?.CanonicalCategory;", source);
        Assert.Contains("CemConfigCategory.ItemsSource = categories;", source);
        Assert.Contains("CemConfigCategory.SelectedIndex = index < 0 ? 0 : index;", source);
        Assert.Contains("AuroraCemConfigReferenceFilter.FindLocalized(query, category, tier)", source);
    }

    [Fact]
    public void CemEvidenceReferenceIsScrollableAndNeverEllipsized()
    {
        var doc = Layout();
        var evidence = Named(doc, "CemConfigDetailEvidence");
        Assert.Equal("TextBlock", evidence.Name.LocalName);
        Assert.Null(evidence.Attribute("MaxLines"));
        Assert.Null(evidence.Attribute("TextTrimming"));
        Assert.Equal("Wrap", (string?)evidence.Attribute("TextWrapping"));
        Assert.Equal("True", (string?)evidence.Attribute("IsTextSelectionEnabled"));

        var scroll = Named(doc, "CemConfigEvidenceScroll");
        Assert.Equal("ScrollViewer", scroll.Name.LocalName);
        Assert.Equal("Auto", (string?)scroll.Attribute("VerticalScrollBarVisibility"));
        Assert.Equal("Disabled", (string?)scroll.Attribute("HorizontalScrollBarVisibility"));
        Assert.Equal("112", (string?)scroll.Attribute("MaxHeight"));
        Assert.Contains(scroll, evidence.Ancestors());

        var projection = Source("src/Dvinholm.App/MainWindow.AuroraReference.cs");
        Assert.Contains("CemConfigDetailEvidence.Text = L(", projection);
        Assert.Contains("row.EvidenceReference;", projection);
        Assert.Contains("CemConfigDetailEvidence.Text = string.Empty;", projection);
        Assert.DoesNotContain("row.EvidenceReference[..", projection);
    }
}
