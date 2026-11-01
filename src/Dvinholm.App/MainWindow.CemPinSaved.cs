using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Dvinholm.App;

public sealed partial class MainWindow
{
    private async void SavedCemPins_Click(object sender, RoutedEventArgs e)
    {
#if DVINHOLM_DEVELOPMENT_FEATURES
        if (_closing || !await _udsGate.WaitAsync(0)) return;
        var generation = ClearDiagnosticReadResult();
        CemPinSecret? loaded = null;
        CemPinStore? store = null;
        try
        {
            CemPinSavedButton.IsEnabled = false;
            RefreshCemPartReadAvailability();
            store = OpenCemPinStore();
            var inventory = await Task.Run(store.List);
            if (_closing || !_udsResultLifetime.CanDisplay(generation)) return;
            var selection = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch,
                Header = CemText("Сохранённая запись", "Saved record") };
            foreach (var entry in inventory.Entries)
                selection.Items.Add(new ComboBoxItem { Tag = entry,
                    Content = $"{entry.SavedAt.ToLocalTime():g} · CEM P/N {entry.PartNumber}" });
            if (selection.Items.Count != 0) selection.SelectedIndex = 0;
            var content = new StackPanel { Spacing = 10, MaxWidth = 500, RequestedTheme = ElementTheme.Dark };
            content.Children.Add(new TextBlock { TextWrapping = TextWrapping.Wrap,
                Text = CemText("Локальные зашифрованные записи доступны текущему пользователю Windows. " +
                    "Номер детали не идентифицирует автомобиль. Загрузка не проверяет PIN на текущем CEM. Показ — до 15 секунд.",
                    "Local encrypted records are available to the current Windows user. A part number does not identify a vehicle. " +
                    "Loading does not check this PIN against the current CEM. Reveal lasts up to 15 seconds.") });
            content.Children.Add(selection);
            if (inventory.Entries.Count == 0)
                content.Children.Add(new TextBlock { Text = CemText("Нет доступных сохранённых PIN.", "No saved PIN records available.") });
            if (inventory.UnavailableCount != 0)
                content.Children.Add(new TextBlock { TextWrapping = TextWrapping.Wrap,
                    Text = CemText("Некоторые записи повреждены или недоступны этому пользователю Windows.",
                        "Some records are damaged or unavailable to this Windows user.") });
            var dialog = new ContentDialog { XamlRoot = RootLayout.XamlRoot, RequestedTheme = ElementTheme.Dark,
                Title = CemText("Сохранённые PIN CEM", "Saved CEM PINs"), Content = content,
                PrimaryButtonText = CemText("Загрузить скрытым", "Load hidden"),
                SecondaryButtonText = CemText("Удалить запись", "Delete record"),
                CloseButtonText = CemText("Закрыть", "Close"), DefaultButton = ContentDialogButton.Close,
                IsPrimaryButtonEnabled = selection.SelectedIndex >= 0,
                IsSecondaryButtonEnabled = selection.SelectedIndex >= 0 };
            var action = await dialog.ShowAsync();
            if (_closing || !_udsResultLifetime.CanDisplay(generation) ||
                (selection.SelectedItem as ComboBoxItem)?.Tag is not CemPinSavedEntry selected) return;
            if (action == ContentDialogResult.Secondary)
            {
                store.Delete(selected.Id);
                SetStatus(CemText("Сохранённая запись PIN удалена.", "Saved PIN record deleted."));
            }
            else if (action == ContentDialogResult.Primary)
            {
                loaded = store.Load(selected.Id);
                _cemPinResult = CemPinAttemptResult.Restored(loaded);
                loaded = null;
                _cemPinLoadedFromStore = true;
                CemPinResultText.Text = CemText("Сохранённый PIN скрыт. Текущий автомобиль не проверен.",
                    "Saved PIN hidden. The current vehicle has not been checked.");
                CemPinRevealButton.IsEnabled = true;
                StartCemPinExpiry();
                SetStatus(CemText("Сохранённый PIN загружен скрытым; запросов к CEM не отправлено.",
                    "Saved PIN loaded hidden; no CEM requests sent."));
            }
        }
        catch (Exception)
        {
            if (!_closing && _udsResultLifetime.CanDisplay(generation))
                SetStatus(CemText("Операция с хранилищем PIN не выполнена. Частные данные скрыты.",
                    "PIN storage operation failed. Private details withheld."));
        }
        finally
        {
            loaded?.Dispose();
            CemPinSavedButton.IsEnabled = !_closing;
            _udsGate.Release();
            SetUdsEnabled(_connected && _session.IsConnected && !_closing);
        }
#else
        await Task.CompletedTask;
#endif
    }
}
