using System.Xml.Linq;

namespace Dvinholm.App.Tests;

/// <summary>
/// Eight offline RU/EN checks for a passive observation list. Switching text
/// must never rescan CAN, alter an RX identifier or verify a guessed TX ID.
/// </summary>
public sealed class AuroraEcuInventoryLocalizationTests
{
    private static readonly XNamespace X =
        "http://schemas.microsoft.com/winfx/2006/xaml";

    private static XDocument Layout()
    {
        using var stream = typeof(AuroraEcuInventoryLocalizationTests).Assembly
            .GetManifestResourceStream("Dvinholm.App.Tests.MainWindow.xaml")
            ?? throw new InvalidOperationException("Missing embedded MainWindow.xaml.");
        return XDocument.Load(stream);
    }

    private static XElement Named(XDocument doc, string id) =>
        doc.Descendants().Single(e => (string?)e.Attribute(X + "Name") == id);

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
    public void EnglishObservationMetricsKeepExistingTechnicalCaptions()
    {
        Assert.Equal("Frames", AuroraEcuInventoryCaptions.Frames(true));
        Assert.Equal("Max bytes", AuroraEcuInventoryCaptions.MaxPayload(true));
    }

    [Fact]
    public void RussianObservationMetricsTranslateWithoutChangingUnitsOrEvidence()
    {
        Assert.Equal("Кадры", AuroraEcuInventoryCaptions.Frames(false));
        Assert.Equal("Макс. байт", AuroraEcuInventoryCaptions.MaxPayload(false));
        Assert.NotEqual(AuroraEcuInventoryCaptions.Frames(true),
            AuroraEcuInventoryCaptions.Frames(false));
        Assert.NotEqual(AuroraEcuInventoryCaptions.MaxPayload(true),
            AuroraEcuInventoryCaptions.MaxPayload(false));
    }

    [Fact]
    public void InferredAndUnknownRequestStatesRemainDistinguishedInBothLanguages()
    {
        Assert.Equal("Inferred", AuroraEcuInventoryCaptions.RequestBasis(true, true));
        Assert.Equal("Unknown", AuroraEcuInventoryCaptions.RequestBasis(true, false));
        Assert.Equal("Предполагается", AuroraEcuInventoryCaptions.RequestBasis(false, true));
        Assert.Equal("Неизвестно", AuroraEcuInventoryCaptions.RequestBasis(false, false));
        Assert.DoesNotContain("verified",
            AuroraEcuInventoryCaptions.RequestBasis(true, true),
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void InventoryLabelsAreBoundPerRowRatherThanHardcodedEnglish()
    {
        var doc = Layout();
        var template = Named(doc, "EcusView").Descendants()
            .Single(e => e.Name.LocalName == "DataTemplate");
        var text = template.ToString();
        foreach (var key in new[] { "FramesCaption", "MaxPayloadCaption",
            "RequestBasis" })
            Assert.Equal(1, text.Split("{Binding " + key + "}").Length - 1);
        Assert.DoesNotContain("Text=\"Frames\"", text);
        Assert.DoesNotContain("Text=\"Max bytes\"", text);
        Assert.DoesNotContain("Text=\"Unknown\"", text);
        Assert.DoesNotContain("Text=\"Inferred\"", text);
    }

    [Fact]
    public void LocalizedTemplateKeepsAllSixOriginalPassiveObservationBindings()
    {
        var doc = Layout();
        var template = Named(doc, "EcusView").Descendants()
            .Single(e => e.Name.LocalName == "DataTemplate").ToString();
        foreach (var key in new[] { "Response", "Request", "Type",
            "RequestBasis", "Frames", "MaxPayload" })
            Assert.Equal(1, template.Split("{Binding " + key + "}").Length - 1);
        Assert.Equal("Ecu_SelectionChanged",
            (string?)Named(doc, "EcusView").Attribute("SelectionChanged"));
        Assert.DoesNotContain("Click=", template);
        Assert.DoesNotContain("PassThru", template);
    }

    [Fact]
    public void EcuRowNotifiesAllThreeMutableCaptionsWithoutMutatingCanEvidence()
    {
        var code = Source("src/Dvinholm.App/MainWindow.xaml.cs");
        var start = code.IndexOf("private sealed class EcuRow", StringComparison.Ordinal);
        var end = code.IndexOf("private async void Discover_Click(", start, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start);
        var model = code[start..end];
        Assert.Contains("System.ComponentModel.INotifyPropertyChanged", model);
        Assert.Contains("PropertyChangedEventHandler? PropertyChanged", model);
        Assert.Contains("internal void SetPresentationLanguage(bool english)", model);
        Assert.Contains("if (_english == english) return;", model);
        foreach (var key in new[] { "FramesCaption", "MaxPayloadCaption", "RequestBasis" })
            Assert.Contains("new System.ComponentModel.PropertyChangedEventArgs(nameof(" + key + "))", model);
        Assert.Contains("public UdsEcuCandidate Candidate { get; }", model);
        Assert.DoesNotContain("Candidate =", model[model.IndexOf("internal void SetPresentationLanguage(", StringComparison.Ordinal)..]);
    }

    [Fact]
    public void LanguageToggleRefreshesExistingRowsWithoutResetOrPassiveRescan()
    {
        var locale = Source("src/Dvinholm.App/MainWindow.UnifiedUpdates.cs");
        var start = locale.IndexOf("private void ApplyUnifiedLocalization()", StringComparison.Ordinal);
        var end = locale.IndexOf("private void SetPrimaryNavigationLabel", start, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start);
        var method = locale[start..end];
        Assert.Contains("foreach (var row in _ecus)", method);
        Assert.Contains("row.SetPresentationLanguage(_unifiedEnglish);", method);
        Assert.DoesNotContain("_ecus.Clear(", method);
        Assert.DoesNotContain("ShowDiscoveredEcus(", method);
        Assert.DoesNotContain("Discover_Click(", method);
        Assert.DoesNotContain("PassThru", method);
        var main = Source("src/Dvinholm.App/MainWindow.xaml.cs");
        Assert.Contains("new EcuRow(candidate, _unifiedEnglish)", main);
    }

    [Fact]
    public void LocalizedReadOnlyListPreservesSixRoutesAndDangerousActionGates()
    {
        var doc = Layout();
        Assert.Equal(6, Named(doc, "SidebarNavPanel").Elements()
            .Count(e => e.Name.LocalName == "Button"));
        Assert.Equal("False", (string?)Named(doc, "EcusView").Attribute("IsEnabled"));
        Assert.Equal("False",
            (string?)Named(doc, "CemConfigWriteButton").Attribute("IsEnabled"));
        Assert.Equal("False",
            (string?)Named(doc, "DtcClearButton").Attribute("IsEnabled"));
        Assert.Equal("False",
            (string?)Named(doc, "UnifiedStageUpdateButton").Attribute("IsEnabled"));
        Assert.Equal("Disabled",
            (string?)Named(doc, "WorkspaceScroll")
                .Attribute("HorizontalScrollBarVisibility"));
        Assert.Contains(Named(doc, "EcuWorkbenchListPane"),
            Named(doc, "EcusView").Ancestors());
    }
}
