using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Dvinholm.App;

public sealed partial class MainWindow
{
    private UpdateSourcePreferenceStore? _unifiedChannelPreferences;
    private bool _unifiedChannelReady;
    private bool _unifiedUpdatesActive;

    private void InitializeUnifiedUpdates()
    {
        UnifiedCurrentVersion.Text = LatestBuildUpdater.CurrentDisplayVersion;

        // The old feed still contains win-x86 archives. Never show its result
        // in a win-x64 update page: selecting it could replace the wrong binary.
        UnifiedUpdateStatus.Text = Environment.Is64BitProcess
            ? "Проверенный канал win-x64 пока не подключён. Проверка, загрузка и установка отключены до переноса безопасного механизма обновления."
            : "Для этого выпуска используется прежний канал win-x86. Новая встроенная установка станет доступна после отдельной проверки совместимости.";

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
            UnifiedChannelNote.Text = "Не удалось прочитать сохранённый канал. Используется Development.";
        }

        // Set the selection before subscribing to changes: loading must never
        // overwrite the previous user's channel choice.
        UnifiedUpdateChannel.SelectedIndex = saved == UpdateSourceChannel.Stable ? 0 : 1;
        _unifiedChannelReady = true;
    }

    private void UnifiedUpdateChannel_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_unifiedChannelReady || _unifiedChannelPreferences is null)
            return;
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
            UnifiedChannelNote.Text = "Канал сохранён. Установка обновлений пока недоступна.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
                                   System.Security.SecurityException)
        {
            // A write failure must never be reported as successful.
            UnifiedChannelNote.Text = "Не удалось сохранить канал обновлений.";
        }
    }

    private void UnifiedUpdatesNavigate_Click(object sender, RoutedEventArgs e)
    {
        if (_closing) return;

        // Navigation must not let a stale ECU read populate another page.
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

        WorkspaceTitle.Text = "Обновление";
        WorkspaceDescription.Text = "Канал Stable / Development и проверка безопасного обновления";
        DiagnosticResultsCard.Visibility = Visibility.Collapsed;
        WorkspaceScroll.ChangeView(null, 0, null);
        SelectWorkspaceNavigation(null);
        UnifiedUpdateChannel.Focus(FocusState.Programmatic);
    }
}
