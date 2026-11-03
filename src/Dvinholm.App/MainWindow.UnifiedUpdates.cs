using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Automation;
using System.Net.Http;
using System.Text.Json;

namespace Dvinholm.App;

public sealed partial class MainWindow
{
    private UpdateSourcePreferenceStore? _unifiedChannelPreferences;
    private bool _unifiedChannelReady;
    private bool _unifiedLocaleReady;
    private bool _unifiedEnglish;
    private bool _unifiedUpdatesActive;
    private CancellationTokenSource? _unifiedCheckCts;
    private string _unifiedStatusRu = "Проверка x64-релизов ещё не выполнена. Установка отключена.";
    private string _unifiedStatusEn = "No x64 releases checked yet. Installation is disabled.";
    private string _unifiedChannelNoteRu = "Выбор сохраняется для этого пользователя.";
    private string _unifiedChannelNoteEn = "Selection is saved for this Windows user.";

    private void InitializeUnifiedUpdates()
    {
        UnifiedCurrentVersion.Text = LatestBuildUpdater.CurrentDisplayVersion;
        StatusFooterVersion.Text = "Dvinholm " + LatestBuildUpdater.CurrentDisplayVersion;
        _unifiedChannelPreferences = new UpdateSourcePreferenceStore(
            Path.Combine(DvinholmUserDataDirectory.Current,
                UpdateSourcePreferenceStore.FileName));
        var saved = UpdateSourceChannel.Development;
        try
        {
            saved = _unifiedChannelPreferences.Load();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or
                                 System.Security.SecurityException)
        {
            _unifiedChannelNoteRu = "Не удалось прочитать сохранённый канал. Используется Development.";
            _unifiedChannelNoteEn = "Could not load saved channel. Development selected.";
        }

        // Set selection before subscribing to persistence changes. An offline
        // acceptance launch must not overwrite an installed user's settings.
        UnifiedUpdateChannel.SelectedIndex = saved == UpdateSourceChannel.Stable ? 0 : 1;
        _unifiedChannelReady = true;
        _unifiedLocaleReady = true;
        ApplyUnifiedLocalization();
        RefreshUnifiedDownloadButtons();
    }

    private void UnifiedLanguageSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_unifiedLocaleReady) return;
        _unifiedEnglish = UnifiedLanguageSelector.SelectedIndex == 1;
        ApplyUnifiedLocalization();
    }

    private void ApplyUnifiedLocalization()
    {
        // Only the six approved navigation labels and integrated update panel
        // are translated here. Older diagnostic detail panes are a separate
        // localization task and retain their verified diagnostic semantics.
        SetPrimaryNavigationLabel("J2534Connection", ShellNavConnectionLabel,
            "Подключение", "Connection");
        SetPrimaryNavigationLabel("CemPinReadiness", ShellNavCemPinLabel,
            "CEM PIN", "CEM PIN");
        SetPrimaryNavigationLabel("CemConfigurationCatalog", ShellNavConfigLabel,
            "Конфигурация CEM", "CEM configuration");
        SetPrimaryNavigationLabel("DtcFaults", ShellNavDtcLabel,
            "Ошибки (DTC)", "Faults (DTC)");
        SetPrimaryNavigationLabel("EcuWorkspace", ShellNavEcuLabel,
            "Блоки (ECU)", "Modules (ECU)");
        ShellNavUpdatesLabel.Text = _unifiedEnglish ? "Updates" : "Обновление";
        var updatesLabel = ShellNavUpdatesLabel.Text;
        AutomationProperties.SetName(SidebarUpdatesButton, updatesLabel);
        ToolTipService.SetToolTip(SidebarUpdatesButton, updatesLabel);

        UnifiedCurrentVersionTitle.Text = L("Установленная версия", "Installed version");
        UnifiedVersionNote.Text = L("Сведения о последнем релизе появятся после проверки.",
            "Latest release information appears after checking.");
        UnifiedChannelTitle.Text = L("Канал обновлений", "Update channel");
        UnifiedChannelNote.Text = L(_unifiedChannelNoteRu, _unifiedChannelNoteEn);
        UnifiedUpdateSafetyTitle.Text = L("Проверка и установка", "Check and install");
        UnifiedUpdateStatus.Text = L(_unifiedStatusRu, _unifiedStatusEn);
        UnifiedUpdateSafety.Text = L(
            "Сведения о релизе не подтверждают целостность ZIP. Установка отключена до проверки SHA-256, разрядности, резервной копии и восстановления.",
            "Release metadata does not verify ZIP integrity. Installation stays disabled until SHA-256, architecture, backup and recovery are validated.");
        UnifiedCheckUpdateButton.Content = L("Проверить x64-релизы", "Check x64 releases");
        UnifiedStageUpdateButton.Content = L("Скачать и проверить ZIP", "Download and verify ZIP");
        UnifiedCancelStageButton.Content = L("Отменить загрузку", "Cancel download");
        UnifiedPreflightButton.Content = L("Проверить готовность", "Run backup preflight");
        ToolTipService.SetToolTip(UnifiedPreflightButton, L(
            "Офлайн-аудит x64 и будущей резервной копии. Без записи или установки.",
            "Read-only x64 and backup-path audit. No install or file changes."));
        ToolTipService.SetToolTip(UnifiedStageUpdateButton, L(
            "Скачать x64 ZIP в отдельный временный каталог, проверить SHA-256 и структуру. Без установки.",
            "Stage x64 ZIP privately; verify SHA-256 and archive structure. No installation."));
        ToolTipService.SetToolTip(UnifiedCancelStageButton, L(
            "Отменить загрузку и удалить частичный ZIP.",
            "Cancel the download and discard partial ZIP."));
        ToolTipService.SetToolTip(UnifiedCheckUpdateButton, L(
            "Только чтение метаданных официальных релизов, без загрузки ZIP.",
            "Read official release metadata only. No ZIP download."));

        // Keep the offline CEM evidence browser consistent with the selected
        // shell language. This does not localize all legacy diagnostics.
        ApplyAuroraCemConfigLocalization();
        ApplyAuroraPrimaryScreenLocalization();
        // Relabel already observed passive RX items without clearing their
        // selection, rescanning the bus, or modifying the underlying evidence.
        foreach (var row in _ecus)
            row.SetPresentationLanguage(_unifiedEnglish);
        // Re-render the existing passive scan's status, picker captions and
        // selected read-only evidence without changing any selected item.
        RefreshPassiveDiscoveryLanguage();
        RefreshCemPrerequisitePresentation();
        RefreshCemIdentityEvidencePresentation();
        RefreshJ2534ArchitectureAuditPresentation();
        if (_unifiedUpdatesActive)
            UpdateUnifiedWorkspaceHeader();
        else if (_workspaceDestination is AuroraDestination current)
            ApplyUnifiedWorkspaceSection(current);

        if (ShellConnectionText is not null)
            ShellConnectionText.Text = _connected && _session.IsConnected
                ? L("J2534 подключён", "J2534 connected")
                : L("Не подключено", "Disconnected");
        // Accessible header status is a navigation shortcut, never Connect.
        var statusNavigationHint = L(
            "Открыть параметры J2534. Это не подключает автомобиль.",
            "Open J2534 connection settings. This does not connect to the vehicle.");
        ToolTipService.SetToolTip(HeaderConnectionStatusButton, statusNavigationHint);
        AutomationProperties.SetName(HeaderConnectionStatusButton, statusNavigationHint);
        AutomationProperties.SetName(ShellConnectionDot,
            L("Состояние соединения J2534", "J2534 connection indicator"));
    }

    private void SetPrimaryNavigationLabel(string tag, TextBlock block, string ru, string en)
    {
        block.Text = L(ru, en);
        var button = SidebarNavPanel.Children.OfType<Button>().FirstOrDefault(
            b => string.Equals(b.Tag as string, tag, StringComparison.Ordinal));
        if (button is null) return;
        AutomationProperties.SetName(button, block.Text);
        ToolTipService.SetToolTip(button, block.Text);
    }

    private string L(string ru, string en) => _unifiedEnglish ? en : ru;

    private void ApplyUnifiedWorkspaceSection(AuroraDestination destination)
    {
        var section = AuroraWorkspaceNavigation.Describe(destination);
        WorkspaceTitle.Text = _unifiedEnglish ? destination switch
        {
            AuroraDestination.J2534Connection => "Connection",
            AuroraDestination.CemPinReadiness => "CEM PIN",
            AuroraDestination.CemConfigurationCatalog => "CEM configuration",
            AuroraDestination.DtcFaults => "Faults (DTC)",
            AuroraDestination.EcuWorkspace => "Modules (ECU)",
            AuroraDestination.DiagnosticsWorkspace => "Diagnostics",
            AuroraDestination.CanMonitor => "CAN monitor",
            AuroraDestination.EcuProfiles => "ECU profiles",
            _ => section.Title
        } : section.Title;
        WorkspaceDescription.Text = _unifiedEnglish ? destination switch
        {
            AuroraDestination.J2534Connection => "J2534 adapter and CAN connection",
            AuroraDestination.CemPinReadiness => "CEM identity and restricted PIN research",
            AuroraDestination.CemConfigurationCatalog => "CEM configuration evidence and compatibility",
            AuroraDestination.DtcFaults => "Read DTC codes and status, without unverified erase operations",
            AuroraDestination.EcuWorkspace => "Observed ECU responses and diagnostic tools",
            AuroraDestination.DiagnosticsWorkspace => "Verified ECU data reads and addressing",
            AuroraDestination.CanMonitor => "CAN traffic and offline analysis",
            AuroraDestination.EcuProfiles => "ECU address and read-only snapshot catalog",
            _ => section.Description
        } : section.Description;
    }

    private void UpdateUnifiedWorkspaceHeader()
    {
        WorkspaceTitle.Text = L("Обновление", "Updates");
        WorkspaceDescription.Text = L(
            "Каналы Stable / Development и проверка x64-релизов",
            "Stable / Development channels and x64 release check");
    }

    private void SetUnifiedStatus(string ru, string en)
    {
        _unifiedStatusRu = ru;
        _unifiedStatusEn = en;
        UnifiedUpdateStatus.Text = L(ru, en);
    }

    private void UnifiedUpdateChannel_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_unifiedChannelReady || _unifiedChannelPreferences is null)
            return;
        _unifiedCheckCts?.Cancel();
        InvalidateUnifiedCandidate();
        if (UnifiedUpdateChannel.SelectedItem is not ComboBoxItem item)
            return;
        var choice = (item.Tag as string) switch
        {
            "stable" => UpdateSourceChannel.Stable,
            "development" => UpdateSourceChannel.Development,
            _ => (UpdateSourceChannel?)null
        };
        if (choice is null) return;
        try
        {
            _unifiedChannelPreferences.Save(choice.Value);
            _unifiedChannelNoteRu = "Канал сохранён. Установка недоступна.";
            _unifiedChannelNoteEn = "Channel saved. Installation unavailable.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
                                   System.Security.SecurityException)
        {
            _unifiedChannelNoteRu = "Не удалось сохранить канал.";
            _unifiedChannelNoteEn = "Could not save update channel.";
        }
        UnifiedChannelNote.Text = L(_unifiedChannelNoteRu, _unifiedChannelNoteEn);
        SetUnifiedStatus("Канал изменён. Выполните проверку x64-релизов.",
            "Channel changed. Check x64 releases again.");
    }

    private async void UnifiedCheckUpdate_Click(object sender, RoutedEventArgs e)
    {
        if (!UnifiedNetworkAllowed || _unifiedCheckCts is not null ||
            _unifiedStageCts is not null)
            return;
        InvalidateUnifiedCandidate();
        var channel = UnifiedUpdateChannel.SelectedItem is ComboBoxItem { Tag: "stable" }
            ? UpdateSourceChannel.Stable : UpdateSourceChannel.Development;
        using var cts = new CancellationTokenSource();
        _unifiedCheckCts = cts;
        RefreshUnifiedDownloadButtons();
        SetUnifiedStatus("Чтение опубликованных x64-релизов GitHub…",
            "Reading published x64 releases from GitHub…");
        try
        {
            var candidate = await UnifiedX64ReleaseFeed.CheckAsync(channel, cts.Token);
            if (cts.IsCancellationRequested || _closing) return;
            if (candidate is null)
            {
                SetUnifiedStatus(
                    "В выбранном канале нет опубликованного пакета Dvinholm-Windows-x64.zip с файлом контрольных сумм. Установка недоступна.",
                    "No published Dvinholm-Windows-x64.zip with checksums in this channel. Installation unavailable.");
            }
            else
            {
                // Metadata only: user must separately opt in to download and
                // SHA-256/AMD64 inspection. This is never installation approval.
                _unifiedCandidate = candidate;
                SetUnifiedStatus(
                    $"Найдены метаданные x64-кандидата {candidate.Tag}. ZIP ещё не скачан и не проверен, установка отключена.",
                    $"x64 candidate metadata found: {candidate.Tag}. ZIP not downloaded or verified; installation disabled.");
            }
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested) { }
        catch (Exception ex) when (ex is HttpRequestException or IOException or
                                   InvalidDataException or JsonException or
                                   OperationCanceledException)
        {
            if (!_closing)
                SetUnifiedStatus(
                    "Не удалось проверить x64-релизы. Проверьте соединение с GitHub. Установка отключена.",
                    "Could not check x64 releases. Verify GitHub connectivity. Installation disabled.");
        }
        finally
        {
            if (ReferenceEquals(_unifiedCheckCts, cts))
                _unifiedCheckCts = null;
            RefreshUnifiedDownloadButtons();
        }
    }

    private void UnifiedUpdatesNavigate_Click(object sender, RoutedEventArgs e)
    {
        if (_closing) return;
        _udsCts?.Cancel();
        ClearDiagnosticReadResult();

        _workspaceDestination = null;
        _unifiedUpdatesActive = true;
        ShowDashboardWorkspace();
        foreach (var page in new FrameworkElement[]
        {
            UnifiedUpdatesPage, CemPinPage, CemConfigPage, DtcPage, EcuPage,
            DiagnosticsPage, CanMonitorPage, ProfilesPage, ConnectionPage
        })
            page.Visibility = ReferenceEquals(page, UnifiedUpdatesPage)
                ? Visibility.Visible : Visibility.Collapsed;
        UpdateUnifiedWorkspaceHeader();
        DiagnosticResultsCard.Visibility = Visibility.Collapsed;
        SelectWorkspaceNavigation(null);
        // A ComboBox focus below the first card can make WinUI scroll the
        // vertically-stacked Updates workspace past the installed-version
        // overview on 640/800/980 DIP windows. Keep keyboard focus on the
        // selected sidebar route in that layout; the channel is still
        // reachable with Tab. On >=1280 DIP the three cards share a row.
        if (RootLayout.ActualWidth >= 1280)
            UnifiedUpdateChannel.Focus(FocusState.Programmatic);
        else
            SidebarUpdatesButton.Focus(FocusState.Programmatic);
        WorkspaceScroll.ChangeView(null, 0, null);
    }
}
