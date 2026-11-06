using System.Xml.Linq;

namespace Dvinholm.App.Tests;

/// <summary>
/// Eight offline checks for passive ECU discovery presentation and selection.
/// Does not run VirtualCar, invoke J2534, or send CAN/UDS requests.
/// </summary>
public sealed class UdsPassiveEcuListPresentationTests
{
    private static readonly XNamespace XamlName =
        "http://schemas.microsoft.com/winfx/2006/xaml";

    [Fact]
    public void StandardResponseLabelsRequestAsInferredRatherThanVerified()
    {
        var candidate = new UdsEcuCandidate(0x7E8, 0x7E0, false, 4, 8);
        var label = UdsPassiveEcuListPresentation.SelectorLabel(candidate, true);

        Assert.Contains("response 0x7E8 (11-bit)", label);
        Assert.Contains("request 0x7E0 (INFERRED)", label);
        Assert.DoesNotContain("verified", label, StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith("Observed:", label);
    }

    [Fact]
    public void ExtendedIdsAreEightDigitHexAndAreNotCalledVolvoModules()
    {
        var candidate = new UdsEcuCandidate(0x18DAF110, 0x18DA10F1, true, 2, 8);
        var label = UdsPassiveEcuListPresentation.SelectorLabel(candidate, true);
        var details = UdsPassiveEcuListPresentation.SelectionDetails(candidate);

        Assert.Contains("response 0x18DAF110 (29-bit)", label);
        Assert.Contains("request 0x18DA10F1 (INFERRED)", label);
        Assert.Contains("Suggested request ID: 0x18DA10F1 (INFERRED ONLY)", details);
        Assert.DoesNotContain("CEM identified", details);
    }

    [Fact]
    public void UnknownRequestAddressRemainsExplicitlyUnknown()
    {
        var candidate = new UdsEcuCandidate(0x715, null, false, 1, 4);
        Assert.Contains("request UNKNOWN",
            UdsPassiveEcuListPresentation.SelectorLabel(candidate, true));
        var details = UdsPassiveEcuListPresentation.SelectionDetails(candidate);
        Assert.Contains("Suggested request ID: UNKNOWN", details);
        Assert.Contains("Request ID remains BLANK", details);
        Assert.DoesNotContain("0x70D", details);
    }

    [Fact]
    public void PartialScanIsIneligibleAndEmptyScanIsNotFaultFreeProof()
    {
        Assert.Contains("INCOMPLETE: 2", UdsPassiveEcuListPresentation.ScanSummary(2, false));
        Assert.Contains("Selection disabled", UdsPassiveEcuListPresentation.ScanSummary(2, false));
        Assert.Contains("No ECU identity established",
            UdsPassiveEcuListPresentation.ScanSummary(0, true));
        Assert.Contains("Choose one", UdsPassiveEcuListPresentation.ScanSummary(2, true));
        Assert.Contains("Partial observation",
            UdsPassiveEcuListPresentation.SelectorLabel(
                new UdsEcuCandidate(0x7E8, 0x7E0, false, 1, 8), false));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            UdsPassiveEcuListPresentation.ScanSummary(-1, false));
    }

    [Fact]
    public void DetailedReportDoesNotClaimEcuIdentityOrTransmit()
    {
        var candidate = new UdsEcuCandidate(0x7E9, 0x7E1, false, 5, 6);
        var text = UdsPassiveEcuListPresentation.SelectionDetails(candidate);

        Assert.Contains("Observed response ID: 0x7E9", text);
        Assert.Contains("Observed response frames: 5", text);
        Assert.Contains("Maximum observed CAN data length: 6 bytes", text);
        Assert.Contains("NOT ECU IDENTIFICATION", text);
        Assert.Contains("actual module identity and request address are UNVERIFIED", text);
        Assert.Contains("enter it manually", text);
        Assert.Contains("No active ECU request was sent", text);
        Assert.DoesNotContain("ECM confirmed", text);
    }

    [Fact]
    public void NullOrImpossibleCandidateMetadataIsNotPresentedAsEvidence()
    {
        Assert.Throws<ArgumentNullException>(() =>
            UdsPassiveEcuListPresentation.SelectorLabel(null!, true));
        Assert.Throws<ArgumentNullException>(() =>
            UdsPassiveEcuListPresentation.SelectionDetails(null!));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            UdsPassiveEcuListPresentation.SelectionDetails(
                new UdsEcuCandidate(0x7E8, 0x7E0, false, -1, 8)));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            UdsPassiveEcuListPresentation.SelectionDetails(
                new UdsEcuCandidate(0x7E8, 0x7E0, false, 1, -3)));
    }

    [Fact]
    public void ExistingListAndSelectorHaveDistinctUnverifiedAddressGuidance()
    {
        var xaml = LoadMainWindow();
        var list = Named(xaml, "EcusView");
        var selector = Named(xaml, "EcuSelector");
        var evidence = Named(xaml, "EcuSelectionInfo");
        Assert.Equal("False", (string?)list.Attribute("IsEnabled"));
        Assert.Equal("False", (string?)selector.Attribute("IsEnabled"));
        Assert.Contains("Passive response observations", (string?)list.Attribute("ToolTipService.ToolTip"));
        Assert.Contains("Selection does not send", (string?)selector.Attribute("ToolTipService.ToolTip"));
        Assert.Equal("True", (string?)evidence.Attribute("IsTextSelectionEnabled"));
        Assert.Equal("Wrap", (string?)evidence.Attribute("TextWrapping"));
        Assert.Equal("86", (string?)evidence.Parent?.Attribute("MaxHeight"));
        Assert.Equal("Disabled", (string?)evidence.Parent?.Attribute("HorizontalScrollBarVisibility"));
        var listSource = list.ToString();
        Assert.Contains("Observed RX", listSource);
        Assert.Contains("Guessed TX", listSource);
        Assert.Contains("{Binding RequestBasis}", listSource);
        Assert.NotNull(Named(xaml, "EcuInfoText")); // Profile metadata remains separate.
    }

    [Fact]
    public void NewOrIncompleteScansNeverAutomaticallyArmInferredAddresses()
    {
        var source = Source("src/Dvinholm.App/MainWindow.xaml.cs");
        var start = source.IndexOf("private void ClearPassiveDiscoverySelection(", StringComparison.Ordinal);
        var end = source.IndexOf("private void SetUdsEnabled(", start, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start);
        var segment = source[start..end];
        Assert.Contains("UdsRequestId.Text = string.Empty", segment);
        Assert.Contains("UdsResponseId.Text = string.Empty", segment);
        Assert.Contains("EcusView.SelectedIndex = -1", segment);
        Assert.Contains("EcuSelector.SelectedIndex = -1", segment);
        Assert.Contains("_ecuScanComplete = complete", segment);
        Assert.Contains("EcusView.IsEnabled = complete && found.Count > 0", segment);
        Assert.Contains("EcuSelector.IsEnabled = complete && found.Count > 0", segment);
        Assert.Contains("if (!_ecuScanComplete", segment);
        Assert.Contains("!ReferenceEquals(_ecus[index], row)", segment);
        Assert.Contains("UdsPassiveEcuListPresentation.SelectionDetails", segment);
        Assert.DoesNotContain("EcusView.SelectedIndex = 0", segment);
        Assert.DoesNotContain("EcuSelector.SelectedIndex = 0", segment);
        Assert.DoesNotContain("UdsRequestId.Text = row.Candidate.RequestIdText", segment);
        Assert.DoesNotContain("_session.", segment);
        Assert.Contains("ClearPassiveDiscoverySelection();", source);
        Assert.Contains("ShowDiscoveredEcus(ex.Candidates, complete: false)", source);
        Assert.Contains("ShowDiscoveredEcus(found, complete: true)", source);
    }

    // Stage 73: four real bilingual outcome combinations and two evidence gates.
    // No CAN/J2534 traffic is generated by these pure presentation contracts.
    [Theory]
    [InlineData(0, true, "Ответы UDS не обнаружены", "No UDS response IDs observed")]
    [InlineData(3, true, "адреса запроса не подтверждены", "request IDs are not verified")]
    [InlineData(0, false, "НЕПОЛНО: наблюдалось 0", "INCOMPLETE: 0")]
    [InlineData(3, false, "Выбор отключён", "Selection disabled")]
    public void RuAndEnScanSummariesPreserveOutcomeTruth(
        int count, bool complete, string expectedRu, string expectedEn)
    {
        var ru = UdsPassiveEcuListPresentation.ScanSummary(count, complete, english: false);
        var en = UdsPassiveEcuListPresentation.ScanSummary(count, complete, english: true);
        Assert.Contains(expectedRu, ru);
        Assert.Contains(expectedEn, en);
        Assert.NotEqual(ru, en);
        if (!complete)
        {
            Assert.Contains("Выбор отключён", ru);
            Assert.Contains("Selection disabled", en);
        }
        if (complete && count == 0)
        {
            Assert.Contains("Идентичность ECU не установлена", ru);
            Assert.Contains("No ECU identity established", en);
        }
    }

    [Fact]
    public void BilingualSelectorKeepsInferredRequestMarkedAndPartialItemsPassive()
    {
        var candidate = new UdsEcuCandidate(0x7E8, 0x7E0, false, 3, 8);
        var ru = UdsPassiveEcuListPresentation.SelectorLabel(candidate, true, english: false);
        var en = UdsPassiveEcuListPresentation.SelectorLabel(candidate, true, english: true);
        Assert.Contains("ответ 0x7E8", ru);
        Assert.Contains("ПРЕДПОЛОЖЕНИЕ", ru);
        Assert.Contains("response 0x7E8", en);
        Assert.Contains("INFERRED", en);
        Assert.StartsWith("Частичное наблюдение",
            UdsPassiveEcuListPresentation.SelectorLabel(candidate, false, english: false));
        Assert.StartsWith("Partial observation",
            UdsPassiveEcuListPresentation.SelectorLabel(candidate, false, english: true));
    }

    [Fact]
    public void BilingualSelectionDetailsDoNotClaimAuthorityOrFillGuessedTx()
    {
        var candidate = new UdsEcuCandidate(0x7E8, 0x7E0, false, 3, 8);
        var ru = UdsPassiveEcuListPresentation.SelectionDetails(candidate, english: false);
        var en = UdsPassiveEcuListPresentation.SelectionDetails(candidate, english: true);
        Assert.Contains("ID запроса остаётся ПУСТЫМ", ru);
        Assert.Contains("не отправляет запрос", ru);
        Assert.Contains("ТОЛЬКО ПРЕДПОЛОЖЕНИЕ", ru);
        Assert.Contains("Request ID remains BLANK", en);
        Assert.Contains("No active ECU request was sent", en);
        Assert.Contains("INFERRED ONLY", en);

        // The presentation is re-translated from the same selected candidate
        // instead of clearing selected ECU, or invoking a live J2534 request.
        var windowSource = Source("src/Dvinholm.App/MainWindow.xaml.cs");
        Assert.Contains("private void RefreshPassiveDiscoveryLanguage()", windowSource);
        Assert.Contains("row.Candidate, _unifiedEnglish)", windowSource);
        Assert.Contains("selected.Candidate, _unifiedEnglish)", windowSource);
        Assert.Contains("UdsRequestId.Text = string.Empty;", windowSource);
    }

    private static XDocument LoadMainWindow()
    {
        using var resource = typeof(UdsPassiveEcuListPresentationTests).Assembly
            .GetManifestResourceStream("Dvinholm.App.Tests.MainWindow.xaml")
            ?? throw new InvalidOperationException("Missing embedded MainWindow.xaml");
        return XDocument.Load(resource);
    }

    private static XElement Named(XDocument doc, string name) =>
        doc.Descendants().Single(e => (string?)e.Attribute(XamlName + "Name") == name);

    private static string Source(string relative)
    {
        for (DirectoryInfo? d = new(AppContext.BaseDirectory);
             d is not null; d = d.Parent)
        {
            var path = Path.Combine(d.FullName,
                relative.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(path)) return File.ReadAllText(path);
        }
        throw new FileNotFoundException(relative);
    }
}
