using System.Xml.Linq;

namespace Dvinholm.App.Tests;

/// <summary>
/// Eight offline contracts for bilingual passive ECU scan evidence. All values
/// remain inferred or observed, never verified ECU identities or active TX IDs.
/// </summary>
public sealed class AuroraPassiveDiscoveryBilingualTests
{
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
    public void RussianKnownStandardResponsePreservesUnverifiedInferredTx()
    {
        var candidate = new UdsEcuCandidate(0x7E8, 0x7E0, false, 4, 8);
        var ru = UdsPassiveEcuListPresentation.SelectorLabel(candidate, true, english: false);
        var en = UdsPassiveEcuListPresentation.SelectorLabel(candidate, true, english: true);
        Assert.Contains("ответ 0x7E8 (11-bit)", ru);
        Assert.Contains("запрос 0x7E0 (ПРЕДПОЛОЖЕНИЕ)", ru);
        Assert.DoesNotContain("подтвержд", ru, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("request 0x7E0 (INFERRED)", en);
        Assert.Equal(en, UdsPassiveEcuListPresentation.SelectorLabel(candidate, true));
    }

    [Fact]
    public void RussianPartialScanAndMissingTxRemainExplicitlyUnselectable()
    {
        var candidate = new UdsEcuCandidate(0x18DAF110, null, true, 1, 4);
        var ru = UdsPassiveEcuListPresentation.SelectorLabel(candidate, false, english: false);
        Assert.Contains("Частичное наблюдение", ru);
        Assert.Contains("ответ 0x18DAF110 (29-bit)", ru);
        Assert.Contains("запрос НЕИЗВЕСТЕН", ru);
        Assert.DoesNotContain("0x18DA10F1", ru);
        Assert.Contains("Partial observation",
            UdsPassiveEcuListPresentation.SelectorLabel(candidate, false));
    }

    [Fact]
    public void ScanSummaryTranslatesCompletePartialAndEmptyOutcomesWithoutSuccessClaims()
    {
        Assert.Contains("НЕПОЛНО: наблюдалось 2",
            UdsPassiveEcuListPresentation.ScanSummary(2, false, english: false));
        Assert.Contains("Выбор отключён",
            UdsPassiveEcuListPresentation.ScanSummary(2, false, english: false));
        Assert.Contains("Идентичность ECU не установлена",
            UdsPassiveEcuListPresentation.ScanSummary(0, true, english: false));
        Assert.Contains("адреса запроса не подтверждены",
            UdsPassiveEcuListPresentation.ScanSummary(2, true, english: false));
        Assert.Equal(UdsPassiveEcuListPresentation.ScanSummary(2, false),
            UdsPassiveEcuListPresentation.ScanSummary(2, false, english: true));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            UdsPassiveEcuListPresentation.ScanSummary(-1, true, english: false));
    }

    [Fact]
    public void RussianSelectionDetailsPreserveRealHexFrameEvidenceAndBlankTxWarning()
    {
        var candidate = new UdsEcuCandidate(0x18DAF110, 0x18DA10F1, true, 5, 6);
        var ru = UdsPassiveEcuListPresentation.SelectionDetails(candidate, english: false);
        Assert.Contains("НЕ ИДЕНТИФИКАЦИЯ ECU", ru);
        Assert.Contains("Наблюдаемый ID ответа: 0x18DAF110", ru);
        Assert.Contains("Предполагаемый ID запроса: 0x18DA10F1 (ТОЛЬКО ПРЕДПОЛОЖЕНИЕ)", ru);
        Assert.Contains("Наблюдаемых кадров: 5", ru);
        Assert.Contains("Максимальная длина данных CAN: 6 байт", ru);
        Assert.Contains("адрес запроса НЕ ПОДТВЕРЖДЕНЫ", ru);
        Assert.Contains("ID запроса остаётся ПУСТЫМ", ru);
        Assert.Contains("выбор записи не отправляет запрос", ru);
        Assert.DoesNotContain("PIN:", ru);
    }

    [Fact]
    public void UnknownTxAndImpossibleEvidenceRemainGuardedInRussian()
    {
        var candidate = new UdsEcuCandidate(0x715, null, false, 1, null);
        var ru = UdsPassiveEcuListPresentation.SelectionDetails(candidate, english: false);
        Assert.Contains("Наблюдаемый ID ответа: 0x715", ru);
        Assert.Contains("Предполагаемый ID запроса: НЕИЗВЕСТЕН", ru);
        Assert.Contains("неизвестно байт", ru);
        Assert.DoesNotContain("0x70D", ru);
        Assert.Throws<ArgumentNullException>(() =>
            UdsPassiveEcuListPresentation.SelectorLabel(null!, true, english: false));
        Assert.Throws<ArgumentNullException>(() =>
            UdsPassiveEcuListPresentation.SelectionDetails(null!, english: false));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            UdsPassiveEcuListPresentation.SelectionDetails(
                new UdsEcuCandidate(0x7E8, 0x7E0, false, -1, 8), english: false));
    }

    [Fact]
    public void RealScanStatusCachesBothLocalesWithoutChangingCtsOrCanGate()
    {
        var code = Source("src/Dvinholm.App/MainWindow.xaml.cs");
        var a = code.IndexOf("private async void Discover_Click(", StringComparison.Ordinal);
        var b = code.IndexOf("private void ClearPassiveDiscoverySelection(", a, StringComparison.Ordinal);
        Assert.True(a >= 0 && b > a);
        var scan = code[a..b];
        Assert.Contains("SetPassiveDiscoveryStatus(", scan);
        Assert.Contains("потеряно кадров", scan);
        Assert.Contains("frame(s) dropped", scan);
        Assert.Contains("SetStatus(UdsPassiveDiscoveryActivity.Message(", scan);
        Assert.Contains("_udsGate.WaitAsync(0)", scan);
        Assert.Contains("_udsGate.Release()", scan);
        Assert.DoesNotContain("UdsRequestId.Text = row.Candidate.Request", scan);
        Assert.DoesNotContain("SendAsync(", scan);
    }

    [Fact]
    public void ExistingComboBoxItemsAreRelabeledInPlaceWithoutReselectingOrRescanning()
    {
        var code = Source("src/Dvinholm.App/MainWindow.xaml.cs");
        var a = code.IndexOf("private void RefreshPassiveDiscoveryLanguage(", StringComparison.Ordinal);
        var b = code.IndexOf("private void CancelDiscovery_Click(", a, StringComparison.Ordinal);
        Assert.True(a >= 0 && b > a);
        var refresh = code[a..b];
        Assert.Contains("EcuSelector.Items[i] is ComboBoxItem item", refresh);
        Assert.Contains("item.Content = UdsPassiveEcuListPresentation.SelectorLabel(", refresh);
        Assert.Contains("selected.Candidate, _unifiedEnglish", refresh);
        Assert.Contains("_hasEcuScanOutcome", refresh);
        Assert.DoesNotContain("EcuSelector.Items.Clear(", refresh);
        Assert.DoesNotContain("EcuSelector.Items.Add(", refresh);
        Assert.DoesNotContain("SelectedIndex =", refresh);
        Assert.DoesNotContain("Discover_Click(", refresh);
        Assert.DoesNotContain("ApplyEcuSelection(", refresh);
        var locale = Source("src/Dvinholm.App/MainWindow.UnifiedUpdates.cs");
        Assert.Contains("RefreshPassiveDiscoveryLanguage();", locale);
        Assert.Contains("row.SetPresentationLanguage(_unifiedEnglish);", locale);
    }

    [Fact]
    public void DefaultEnglishPresentationAndSixProtectedUiRoutesStayUnchanged()
    {
        var sample = new UdsEcuCandidate(0x7E8, 0x7E0, false, 3, 8);
        Assert.Equal(UdsPassiveEcuListPresentation.SelectionDetails(sample),
            UdsPassiveEcuListPresentation.SelectionDetails(sample, english: true));
        var code = Source("src/Dvinholm.App/MainWindow.xaml.cs");
        Assert.Contains("EcuSelector.Items.Add(new ComboBoxItem", code);
        Assert.Contains("EcuSelectionInfo.Text = UdsPassiveEcuListPresentation.SelectionDetails(", code);
        using var stream = typeof(AuroraPassiveDiscoveryBilingualTests).Assembly
            .GetManifestResourceStream("Dvinholm.App.Tests.MainWindow.xaml")
            ?? throw new InvalidOperationException("Embedded XAML missing.");
        var doc = XDocument.Load(stream);
        var name = XNamespace.Get("http://schemas.microsoft.com/winfx/2006/xaml");
        var nav = doc.Descendants().Single(e =>
            (string?)e.Attribute(name + "Name") == "SidebarNavPanel");
        Assert.Equal(6, nav.Elements().Count(e => e.Name.LocalName == "Button"));
        foreach (var id in new[] { "CemConfigWriteButton", "DtcClearButton",
            "UnifiedStageUpdateButton" })
            Assert.Equal("False", (string?)doc.Descendants().Single(e =>
                (string?)e.Attribute(name + "Name") == id).Attribute("IsEnabled"));
    }
}
