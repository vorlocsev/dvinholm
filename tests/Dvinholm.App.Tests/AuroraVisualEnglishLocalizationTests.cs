using System.Xml.Linq;

namespace Dvinholm.App.Tests;

/// <summary>
/// Eight offline visual-layout and bilingual-display acceptance gates.
/// No WinUI launch, screenshots, vendor DLL calls, CAN requests or ECU writes.
/// </summary>
public sealed class AuroraVisualEnglishLocalizationTests
{
    private static readonly XNamespace X =
        "http://schemas.microsoft.com/winfx/2006/xaml";

    private static XDocument Layout()
    {
        using var stream = typeof(AuroraVisualEnglishLocalizationTests).Assembly
            .GetManifestResourceStream("Dvinholm.App.Tests.MainWindow.xaml")
            ?? throw new InvalidOperationException("Missing WinUI XAML fixture.");
        return XDocument.Load(stream);
    }

    private static XElement Named(XDocument document, string name) =>
        document.Descendants().Single(element =>
            (string?)element.Attribute(X + "Name") == name);

    private static XElement State(XDocument doc, string name) =>
        doc.Descendants().Single(element =>
            element.Name.LocalName == "VisualState" &&
            (string?)element.Attribute(X + "Name") == name);

    private static Dictionary<string, string> Setters(XDocument doc, string name) =>
        State(doc, name).Descendants().Where(e => e.Name.LocalName == "Setter")
            .ToDictionary(e => (string)e.Attribute("Target")!,
                e => (string)e.Attribute("Value")!, StringComparer.Ordinal);

    private static string Source(string relative)
    {
        for (DirectoryInfo? dir = new(AppContext.BaseDirectory);
            dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName,
                relative.Replace('/', Path.DirectorySeparatorChar));
            // Windows runners use CRLF; compare source semantics, not checkout EOLs.
            if (File.Exists(candidate))
                return File.ReadAllText(candidate).Replace("\r\n", "\n").Replace('\r', '\n');
        }
        throw new FileNotFoundException(relative);
    }

    [Fact]
    public void AuroraHasSoftGradientAndAllNamedControlsAreUnique()
    {
        var doc = Layout();
        var root = Named(doc, "RootLayout");
        var background = root.Elements()
            .Single(e => e.Name.LocalName == "Grid.Background");
        Assert.Contains(background.Descendants(),
            e => e.Name.LocalName == "GradientStop" &&
                 (string?)e.Attribute("Color") == "#13152F");
        var names = doc.Descendants()
            .Select(e => (string?)e.Attribute(X + "Name"))
            .Where(name => name is not null).ToArray();
        Assert.Equal(names.Length, names.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal("12", (string?)doc.Descendants()
            .First(e => e.Name.LocalName == "Style" &&
                (string?)e.Attribute(X + "Key") == "AuroraCard")
            .Descendants().Single(e => (string?)e.Attribute("Property") == "CornerRadius")
            .Attribute("Value"));
    }

    [Fact]
    public void CemReferenceUsesTrueWideThreePaneButNoMinimum730PixelClip()
    {
        var doc = Layout();
        var grid = Named(doc, "CemReferenceGrid");
        Assert.Null(grid.Attribute("MinWidth"));
        Assert.Contains(Named(doc, "CemConfigPage"), grid.Ancestors());
        var panes = grid.Elements().Where(e => e.Name.LocalName == "Border").ToArray();
        Assert.Equal(new[] { "CemReferenceCategoryPane", "CemReferenceValuesPane",
            "CemReferenceDetailPane" },
            panes.Select(e => (string?)e.Attribute(X + "Name")!));
        Assert.Equal(new[] { "0", "1", "2" },
            panes.Select(e => (string?)e.Attribute("Grid.Column")!));
        Assert.Equal("1280", (string?)State(doc, "CemReferenceWide")
            .Descendants().Single(e => e.Name.LocalName == "AdaptiveTrigger")
            .Attribute("MinWindowWidth"));
    }

    [Fact]
    public void CemCompactStacksPanelsAndDisabledActionsInsteadOfClipping()
    {
        var doc = Layout();
        var setters = Setters(doc, "CemReferenceStacked");
        Assert.Equal("0", (string?)State(doc, "CemReferenceStacked")
            .Descendants().Single(e => e.Name.LocalName == "AdaptiveTrigger")
            .Attribute("MinWindowWidth"));
        Assert.Equal("*", setters["CemReferenceCategoryColumn.Width"]);
        Assert.Equal("0", setters["CemReferenceValuesColumn.Width"]);
        Assert.Equal("0", setters["CemReferenceDetailColumn.Width"]);
        Assert.Equal("1", setters["CemReferenceValuesPane.(Grid.Row)"]);
        Assert.Equal("2", setters["CemReferenceDetailPane.(Grid.Row)"]);
        Assert.Equal("1", setters["CemConfigCompareButton.(Grid.Row)"]);
        Assert.Equal("2", setters["CemConfigPrepareButton.(Grid.Row)"]);
        foreach (var name in new[] { "CemConfigBackupButton", "CemConfigCompareButton",
            "CemConfigPrepareButton" })
        {
            var button = Named(doc, name);
            Assert.Equal("False", (string?)button.Attribute("IsEnabled"));
            Assert.Null(button.Attribute("Click"));
        }
    }

    [Fact]
    public void TwoDistinctSideProfileIllustrationsHaveHeadlightsAndWindowDetails()
    {
        var doc = Layout();
        var stage = Named(doc, "EcuVehicleArtboard");
        Assert.Contains(stage, Named(doc, "EcuP2SideProfile").Ancestors());
        Assert.Contains(stage, Named(doc, "EcuP3SideProfile").Ancestors());
        var p2 = Named(doc, "EcuP2SideProfile");
        var p3 = Named(doc, "EcuP3SideProfile");
        Assert.Equal("Collapsed", (string?)p3.Attribute("Visibility"));
        Assert.True(p2.Elements().Count(e => e.Name.LocalName == "Path") >= 6);
        Assert.True(p3.Elements().Count(e => e.Name.LocalName == "Path") >= 5);
        Assert.True(p2.Elements().Count(e => e.Name.LocalName == "Ellipse") >= 4);
        Assert.True(p3.Elements().Count(e => e.Name.LocalName == "Ellipse") >= 4);
        Assert.NotEqual(
            (string?)p2.Elements().First(e => e.Name.LocalName == "Path").Attribute("Data"),
            (string?)p3.Elements().First(e => e.Name.LocalName == "Path").Attribute("Data"));
        Assert.DoesNotContain("ECM", p2.ToString());
        Assert.DoesNotContain("CEM", p3.ToString());
    }

    [Fact]
    public void LocaleSwitchRefreshesAllPrimaryPageCaptionsAndKeepsSelectedCanContext()
    {
        var source = Source("src/Dvinholm.App/MainWindow.AuroraScreenLocalization.cs");
        var switcher = Source("src/Dvinholm.App/MainWindow.UnifiedUpdates.cs");
        Assert.Contains("ApplyAuroraPrimaryScreenLocalization();", switcher);
        foreach (var control in new[] { "WorkspaceScopeLabel", "SidebarFooterLocale",
            "CemPinResearchTitle", "EcuDiscoveryTitle", "EcuVehicleHeading",
            "EcuObservationHeading", "DtcPageHeading", "DtcEvidenceDisclaimer",
            "DiagnosticsAddressHeading", "ConnectionPageHeading",
            "DiagnosticResultHeading", "CanMonitorHeading" })
        {
            Assert.NotNull(Named(Layout(), control));
            Assert.Contains(control + ".Text = L(", source);
        }
        Assert.DoesNotContain("Connect_Click(", source);
        Assert.DoesNotContain("ReadDtc_Click(", source);
        Assert.DoesNotContain("ReadCemPin_Click(", source);
        Assert.DoesNotContain("_session.Bus", source);
    }

    [Fact]
    public void SelectedCemEvidenceTierRefreshesSelectionBoxWithoutChangingCanonicalTier()
    {
        var source = Source("src/Dvinholm.App/MainWindow.AuroraReference.cs");
        var start = source.IndexOf("private void ApplyAuroraCemConfigLocalization()", StringComparison.Ordinal);
        var end = source.IndexOf("private void RefreshAuroraConfigReference()", start, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start);
        var projection = source[start..end];

        Assert.Contains("selectedEvidenceTier = CemConfigEvidenceTier.SelectedItem", projection);
        Assert.Contains("var evidenceReady = _cemConfigReferenceReady;", projection);
        Assert.Contains("_cemConfigReferenceReady = false;", projection);
        Assert.Contains("CemConfigEvidenceTier.SelectedItem = null;", projection);
        Assert.Contains("CemConfigEvidenceTier.SelectedItem = selectedEvidenceTier;", projection);
        Assert.Contains("_cemConfigReferenceReady = evidenceReady;", projection);
        Assert.Contains("L(\"Все источники\", \"All sources\")", projection);
        Assert.True(projection.IndexOf("CemConfigEvidenceTier.SelectedItem = null;", StringComparison.Ordinal) <
            projection.IndexOf("CemConfigEvidenceTier.SelectedItem = selectedEvidenceTier;", StringComparison.Ordinal));
    }

    [Fact]
    public void SelectedDtcStatusMaskRefreshesSelectionBoxWithoutResettingReadContext()
    {
        var source = Source("src/Dvinholm.App/MainWindow.AuroraScreenLocalization.cs");
        var start = source.IndexOf("DtcStatusMaskSelector.Header = L(", StringComparison.Ordinal);
        var end = source.IndexOf("ReadDtcButton.Content = L(", start, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start);
        var projection = source[start..end];

        Assert.Contains("selectedStatusMask = DtcStatusMaskSelector.SelectedItem", projection);
        Assert.Contains("SelectionChanged -= DtcStatusMask_SelectionChanged;", projection);
        Assert.Contains("DtcStatusMaskSelector.SelectedItem = null;", projection);
        Assert.Contains("DtcStatusMaskSelector.SelectedItem = selectedStatusMask;", projection);
        Assert.Contains("SelectionChanged += DtcStatusMask_SelectionChanged;", projection);
        Assert.Contains("L(\"Все статусы\", \"All status bits\")", projection);
        Assert.DoesNotContain("ClearDiagnosticReadResult(", projection);
        Assert.DoesNotContain("ReadDtc_Click(", projection);
        Assert.True(projection.IndexOf("SelectionChanged -= DtcStatusMask_SelectionChanged;", StringComparison.Ordinal) <
            projection.IndexOf("DtcStatusMaskSelector.SelectedItem = null;", StringComparison.Ordinal));
    }

    [Fact]
    public void DtcEnglishProjectionKeepsRawCodesAndNeverInventsFaultDescriptions()
    {
        var read = new UdsDtcReadResult(0x08, new[]
        {
            new UdsDtcRecord(0x012345, 0x09),
            new UdsDtcRecord(0x000001, 0x00)
        });
        var en = AuroraDtcTableProjection.Project(read, english: true);
        var ru = AuroraDtcTableProjection.Project(read);
        Assert.Equal(en.Select(r => r.Code), ru.Select(r => r.Code));
        Assert.Equal("0x012345", en[0].Code);
        Assert.Equal(AuroraDtcTableProjection.MissingDescriptionEnglish,
            en[0].Description);
        Assert.Equal(AuroraDtcTableProjection.MissingDescription, ru[0].Description);
        Assert.Contains("confirmed DTC", en[0].Status);
        Assert.Contains("unsupported bits 0x01", en[0].Status);
        Assert.DoesNotContain("test failed", en[0].Status);
        Assert.Contains("Status not verified", en[1].Status);
        Assert.Contains("No verified Volvo fault cause", en[0].Details);
        Assert.Empty(AuroraDtcTableProjection.Project(
            new UdsDtcReadResult(0x00, Array.Empty<UdsDtcRecord>()), true));
    }

    [Fact]
    public void SwitchingLanguageDoesNotRepeatDtcRequestsOrEnableErase()
    {
        var presenter = Source("src/Dvinholm.App/MainWindow.AuroraDtc.cs");
        Assert.Contains("_lastAuroraDtcRead = null;", presenter);
        Assert.Contains("Project(result, _unifiedEnglish)", presenter);
        Assert.Contains("selectedCode", presenter);
        Assert.Contains("RefreshAuroraDtcTableLocalization();", presenter);
        Assert.DoesNotContain("PassThru", presenter);
        Assert.DoesNotContain("ReadDtcResultByStatusMask", presenter);
        var clear = Named(Layout(), "DtcClearButton");
        Assert.Equal("False", (string?)clear.Attribute("IsEnabled"));
        Assert.Null(clear.Attribute("Click"));
        var primary = Source("src/Dvinholm.App/MainWindow.AuroraScreenLocalization.cs");
        Assert.Contains("DtcStatusMaskSelector.Items.OfType<ComboBoxItem>()", primary);
        Assert.Contains("RefreshAuroraDtcTableLocalization();", primary);
    }

    [Fact]
    public void EcuObservationAndSelectedVehicleStayReadOnlyAcrossLocales()
    {
        var source = Source("src/Dvinholm.App/MainWindow.AuroraEcuObservations.cs");
        var primary = Source("src/Dvinholm.App/MainWindow.AuroraScreenLocalization.cs");
        var reference = Source("src/Dvinholm.App/MainWindow.AuroraReference.cs");
        Assert.Contains("RefreshAuroraEcuObservationLocalization()", primary);
        Assert.Contains("ShowAuroraEcuObservationRail(", source);
        Assert.Contains("HighlightAuroraEcuObservation(index);", source);
        Assert.Contains("Index only; ECU identity", source);
        Assert.Contains("schematic side profile, manually selected", reference);
        Assert.DoesNotContain("SendAsync(", primary);
        Assert.DoesNotContain("PassThruWriteMsgs", primary);
        Assert.DoesNotContain("ClearDiagnosticInformation", primary);
    }
    [Fact]
    public void DeveloperFixtureControlsAreDevelopmentOnly()
    {
        var source = Source("src/Dvinholm.App/MainWindow.xaml.cs");
        var start = source.IndexOf("#if DVINHOLM_DEVELOPMENT_FEATURES\n    private void InitializeDevelopmentControls()", StringComparison.Ordinal);
        var end = source.IndexOf("private void AddTestProfiles_Click", start, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start);
        Assert.Contains("RefreshDevelopmentToolsLanguage();", source[start..end]);
    }

    [Fact]
    public void LanguageSwitchRefreshesDevelopmentPresentationOnlyWithinBuildFlag()
    {
        var source = Source("src/Dvinholm.App/MainWindow.UnifiedUpdates.cs");
        Assert.Contains("#if DVINHOLM_DEVELOPMENT_FEATURES\n        RefreshDevelopmentToolsLanguage();\n#endif", source);
    }

    [Theory]
    [InlineData("_developmentAddTestProfilesButton.Content", "Добавить тестовые профили", "Add test profiles")]
    [InlineData("_developmentPreviewUdsButton.Content", "Пример UDS (офлайн)", "Preview UDS example (offline)")]
    [InlineData("_developmentExportSyntheticButton.Content", "Экспорт синтетического UDS TXT", "Export synthetic UDS TXT")]
    public void DynamicDeveloperActionsHaveBilingualCaptions(string target, string russian, string english)
    {
        var source = Source("src/Dvinholm.App/MainWindow.xaml.cs");
        var method = DevelopmentProjection(source);
        Assert.Contains(target, method);
        Assert.Contains(russian, method);
        Assert.Contains(english, method);
    }

    [Fact]
    public void DemoButtonCaptionUsesActualTimerState()
    {
        var method = DevelopmentProjection(Source("src/Dvinholm.App/MainWindow.xaml.cs"));
        Assert.Contains("_demoMonitorButton.Content = _demoTimer is not null", method);
        Assert.Contains("Остановить демо", method);
        Assert.Contains("Запустить демо", method);
        Assert.Contains("Stop demo", method);
        Assert.Contains("Start demo", method);
    }

    [Fact]
    public void DemoEvidenceIsVisiblySyntheticInBothLanguages()
    {
        var method = DevelopmentProjection(Source("src/Dvinholm.App/MainWindow.xaml.cs"));
        Assert.Contains("ДЕМО АКТИВНО", method);
        Assert.Contains("DEMO ACTIVE", method);
        Assert.Contains("без адаптера или автомобиля", method);
        Assert.Contains("no adapter or vehicle", method);
        Assert.Contains("_demoDataPresent", method);
        Assert.Contains("синтетические кадры сохранены", method);
    }

    [Fact]
    public void LocaleProjectionNeverStartsStopsOrTransmitsFrames()
    {
        var method = DevelopmentProjection(Source("src/Dvinholm.App/MainWindow.xaml.cs"));
        foreach (var forbidden in new[] { "Start(", "Stop(", "EmitDemoFrames(",
            "SetStatus(", "PassThru", "SendAsync(", "Connect_Click(", "DemoMonitor_Click(" })
            Assert.DoesNotContain(forbidden, method);
    }

    [Fact]
    public void DemoLifecycleReusesBilingualProjectionForStartAndStop()
    {
        var source = Source("src/Dvinholm.App/MainWindow.xaml.cs");
        var start = source.IndexOf("private void DemoMonitor_Click(", StringComparison.Ordinal);
        var end = source.IndexOf("#endif", start, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start);
        var lifecycle = source[start..end];
        Assert.Contains("_demoTimer = timer;\n        RefreshDevelopmentToolsLanguage();", lifecycle);
        Assert.Contains("_demoTimer = null;", lifecycle);
        Assert.Contains("timer.Stop();", lifecycle);
        Assert.True(lifecycle.Split("RefreshDevelopmentToolsLanguage();").Length >= 3);
    }

    [Fact]
    public void DeveloperToolCallbacksAndVisibilityGuardsStayUnchanged()
    {
        var source = Source("src/Dvinholm.App/MainWindow.xaml.cs");
        Assert.Contains("_developmentAddTestProfilesButton.Click += AddTestProfiles_Click;", source);
        Assert.Contains("_developmentPreviewUdsButton.Click += PreviewUdsSample_Click;", source);
        Assert.Contains("_developmentExportSyntheticButton.Click += ExportOfflineUdsTxt_Click;", source);
        Assert.Contains("_demoMonitorButton.Click += DemoMonitor_Click;", source);
        Assert.Contains("DevelopmentProfileTools.Visibility = Visibility.Visible;", source);
        Assert.Contains("DevelopmentDiagnosticTools.Visibility = Visibility.Visible;", source);
        Assert.Contains("DeveloperMonitorHost.Visibility = Visibility.Visible;", source);
    }

    private static string DevelopmentProjection(string source)
    {
        var start = source.IndexOf("private void RefreshDevelopmentToolsLanguage()", StringComparison.Ordinal);
        var end = source.IndexOf("private void AddTestProfiles_Click", start, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start);
        return source[start..end];
    }

    [Fact]
    public void J2534ArchitectureAuditAccessibilityNameFollowsVisibleLocale()
    {
        var doc = Layout();
        var expander = Named(doc, "J2534ArchitectureAuditExpander");
        var caption = Named(doc, "J2534ArchitectureAuditHeaderText");
        Assert.Equal("Expander", expander.Name.LocalName);
        Assert.Contains(caption, expander.Descendants());

        var presenter = Source("src/Dvinholm.App/MainWindow.xaml.cs");
        var from = presenter.IndexOf("private void RefreshJ2534ArchitectureAuditPresentation()", StringComparison.Ordinal);
        var to = presenter.IndexOf("private void RefreshJ2534Devices(", from, StringComparison.Ordinal);
        Assert.True(from >= 0 && to > from);
        var projection = presenter[from..to];
        Assert.Contains("J2534ArchitectureAuditHeaderText.Text = L(", projection);
        Assert.Contains("J2534ArchitectureAuditExpander, J2534ArchitectureAuditHeaderText.Text);", projection);
        Assert.DoesNotContain("PassThru", projection);
        Assert.DoesNotContain("SendAsync(", projection);
        Assert.DoesNotContain("Discover_Click(", projection);
    }

    [Fact]
    public void CancelButtonTracksChosenRuEnLanguageWithoutChangingUdsSession()
    {
        var source = Source("src/Dvinholm.App/MainWindow.UnifiedUpdates.cs");
        Assert.Contains("RefreshCancellationButtonLanguage();", source);
        var start = source.IndexOf("private void RefreshCancellationButtonLanguage()", StringComparison.Ordinal);
        var end = source.IndexOf("private void SetPrimaryNavigationLabel(", start, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start);
        var method = source[start..end];
        Assert.Contains("_udsCts is not null", method);
        Assert.Contains("Отмена CEM", method);
        Assert.Contains("Cancel CEM", method);
        Assert.Contains("Отменить операцию", method);
        Assert.Contains("Cancel operation", method);
        Assert.DoesNotContain("_udsCts =", method);
        Assert.DoesNotContain("Cancel(", method);
        Assert.DoesNotContain("SendAsync(", method);
        Assert.DoesNotContain("PassThru", method);
    }

    [Fact]
    public void CemCancellationLabelRestoresCurrentLocaleAfterOperation()
    {
        var source = Source("src/Dvinholm.App/MainWindow.xaml.cs");
        var start = source.IndexOf("private async void ReadCemPartNumber_Click(", StringComparison.Ordinal);
        var end = source.IndexOf("private async void ReadDid_Click(", start, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start);
        var body = source[start..end];
        Assert.Contains("CancelUdsButton.Content = L(\"Отмена CEM\", \"Cancel CEM\");", body);
        Assert.Contains("CancelUdsButton.Content = cancelLabel;", body);
        Assert.Contains("RefreshCancellationButtonLanguage();", body);
    }

    [Theory]
    [InlineData("UdsRequestId", "Введите проверенный TX ID", "Enter source-verified TX ID")]
    [InlineData("UdsResponseId", "Введите проверенный RX ID", "Enter independently verified RX ID")]
    [InlineData("UdsDid", "Например F190", "e.g. F190")]
    public void DiagnosticInputPlaceholdersFollowRuEnWithoutChangingEnteredValues(
        string name, string ru, string en)
    {
        var doc = Layout();
        var input = Named(doc, name);
        Assert.Equal("TextBox", input.Name.LocalName);

        var primary = Source("src/Dvinholm.App/MainWindow.AuroraScreenLocalization.cs");
        var start = primary.IndexOf("UdsRequestId.Header = L(", StringComparison.Ordinal);
        var end = primary.IndexOf("CanIdFormat.Header = L(", start, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start);
        var projection = primary[start..end];

        Assert.Contains(name + ".PlaceholderText = L(", projection);
        Assert.Contains(ru, projection);
        Assert.Contains(en, projection);
        Assert.DoesNotContain("UdsRequestId.Text =", projection);
        Assert.DoesNotContain("UdsResponseId.Text =", projection);
        Assert.DoesNotContain("UdsDid.Text =", projection);
        Assert.DoesNotContain("PassThru", projection);
        Assert.DoesNotContain("SendAsync(", projection);
        Assert.DoesNotContain("ReadDid_Click(", projection);
    }

    [Fact]
    public void CemDialogsUseSelectedApplicationLanguageNotWindowsCulture()
    {
        var source = Source("src/Dvinholm.App/MainWindow.xaml.cs");
        Assert.Contains("private string CemText(string russian, string english) => L(russian, english);", source);
        Assert.DoesNotContain("CurrentUICulture.TwoLetterISOLanguageName", source);
        Assert.DoesNotContain("private static string CemText(", source);

        var pin = Source("src/Dvinholm.App/MainWindow.CemPin.cs");
        var saved = Source("src/Dvinholm.App/MainWindow.CemPinSaved.cs");
        Assert.Contains("Title = CemText(", pin);
        Assert.Contains("Title = CemText(", saved);
        Assert.Contains("CemPinResultText.Text = CemText(", pin);
        Assert.Contains("CemPinResultText.Text = CemText(", saved);
    }

    [Fact]
    public void CemPinSearchCancelRestoresLatestRuEnLanguageAfterCompletion()
    {
        var source = Source("src/Dvinholm.App/MainWindow.CemPin.cs");
        var begin = source.IndexOf("private async void ReadCemPin_Click(", StringComparison.Ordinal);
        Assert.True(begin >= 0);
        var body = source[begin..];
        Assert.Contains("CancelUdsButton.Content = cancelLabel;", body);
        Assert.Contains("RefreshCancellationButtonLanguage();", body);
        Assert.True(body.IndexOf("CancelUdsButton.Content = cancelLabel;", StringComparison.Ordinal) <
            body.IndexOf("RefreshCancellationButtonLanguage();", StringComparison.Ordinal));

        var switcher = Source("src/Dvinholm.App/MainWindow.UnifiedUpdates.cs");
        Assert.Contains("RefreshCancellationButtonLanguage();", switcher);
        var from = switcher.IndexOf("private void RefreshCancellationButtonLanguage()", StringComparison.Ordinal);
        var to = switcher.IndexOf("private void SetPrimaryNavigationLabel(", from, StringComparison.Ordinal);
        Assert.True(from >= 0 && to > from);
        var localeOnly = switcher[from..to];
        Assert.DoesNotContain("cts.Cancel(", localeOnly);
        Assert.DoesNotContain("SendAsync(", localeOnly);
        Assert.DoesNotContain("PassThru", localeOnly);
    }

    [Fact]
    public void PhysicalCanChannelFormatCaptionsFollowSelectedLanguageWithoutReconnecting()
    {
        var doc = Layout();
        var combo = Named(doc, "CanConnectionMode");
        Assert.Equal("ComboBox", combo.Name.LocalName);
        Assert.Equal("0", (string?)combo.Attribute("SelectedIndex"));
        Assert.Equal(new[] { "11-bit", "29-bit" },
            combo.Elements().Select(x => (string?)x.Attribute("Content")));

        var source = Source("src/Dvinholm.App/MainWindow.AuroraScreenLocalization.cs");
        var begin = source.IndexOf("CanConnectionMode.Header = L(", StringComparison.Ordinal);
        var end = source.IndexOf("DllPath.Header = Environment.Is64BitProcess", begin,
            StringComparison.Ordinal);
        Assert.True(begin >= 0 && end > begin);
        var captions = source[begin..end];
        Assert.Contains("Формат CAN-канала", captions);
        Assert.Contains("CAN channel ID format", captions);
        Assert.Contains("ToolTipService.SetToolTip(CanConnectionMode, L(", captions);
        Assert.Contains("переподключение", captions);
        Assert.Contains("Reconnect to change the physical channel format.", captions);
        Assert.DoesNotContain("CanConnectionMode.SelectedIndex =", captions);
        Assert.DoesNotContain("CanConnectionMode.SelectedItem =", captions);
        Assert.DoesNotContain("Connect_Click(", captions);
        Assert.DoesNotContain("PassThru", captions);
        Assert.DoesNotContain("_session", captions);
    }
}
