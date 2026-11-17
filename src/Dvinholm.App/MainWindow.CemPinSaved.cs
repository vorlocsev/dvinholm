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
            var unavailableIndex = 0;
            foreach (var recordId in inventory.UnavailableRecordIds)
            {
                unavailableIndex++;
                selection.Items.Add(new ComboBoxItem {
                    Tag = recordId,
                    Content = CemText($"Недоступная запись {unavailableIndex}",
                        $"Unavailable record {unavailableIndex}")
                });
            }
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
                IsPrimaryButtonEnabled = (selection.SelectedItem as ComboBoxItem)?.Tag is CemPinSavedEntry,
                IsSecondaryButtonEnabled = selection.SelectedIndex >= 0 };
            selection.SelectionChanged += (_, _) =>
            {
                // Failed-decryption records have no secret to load. Their
                // GUID is used only for explicit, separately confirmed deletion.
                var tag = (selection.SelectedItem as ComboBoxItem)?.Tag;
                dialog.IsPrimaryButtonEnabled = tag is CemPinSavedEntry;
                dialog.IsSecondaryButtonEnabled = tag is CemPinSavedEntry or Guid;
            };
            var action = await dialog.ShowAsync();
            if (_closing || !_udsResultLifetime.CanDisplay(generation) ||
                (selection.SelectedItem as ComboBoxItem)?.Tag is not { } selectedTag) return;
            var selectedId = selectedTag switch {
                CemPinSavedEntry entry => entry.Id,
                Guid unavailableId => unavailableId,
                _ => Guid.Empty
            };
            if (selectedId == Guid.Empty) return;
            if (action == ContentDialogResult.Secondary)
            {
                // Deletion is irreversible. Never treat the main dialog's
                // secondary action as consent to destroy an encrypted record.
                // Do not include the secret or any vehicle identity in the
                // confirmation; the selected entry is fixed before awaiting.
                var confirm = new ContentDialog {
                    XamlRoot = RootLayout.XamlRoot, RequestedTheme = ElementTheme.Dark,
                    Title = selectedTag is Guid
                        ? CemText("Удалить недоступную запись?", "Delete unavailable record?")
                        : CemText("Удалить сохранённый PIN?", "Delete saved PIN record?"),
                    Content = new TextBlock {
                        TextWrapping = TextWrapping.Wrap,
                        Text = selectedTag is Guid
                            ? CemText(
                                "Запись не удалось расшифровать. Это не доказывает её повреждение: " +
                                "она может принадлежать другому пользователю Windows. " +
                                "Удаление необратимо. Продолжить?",
                                "The record could not be decrypted. This does not prove corruption: " +
                                "it may belong to another Windows user. " +
                                "Deletion is irreversible. Continue?")
                            : CemText(
                                "Зашифрованная запись будет удалена без возможности восстановления из Dvinholm. " +
                                "Текущий автомобиль не проверяется. Продолжить?",
                                "The encrypted record will be permanently removed from Dvinholm. " +
                                "The current vehicle is not checked. Continue?")
                    },
                    PrimaryButtonText = CemText("Удалить окончательно", "Delete permanently"),
                    CloseButtonText = CemText("Отмена", "Cancel"),
                    DefaultButton = ContentDialogButton.Close
                };
                if (await confirm.ShowAsync() != ContentDialogResult.Primary ||
                    _closing || !_udsResultLifetime.CanDisplay(generation))
                    return;
                // Delete waits for the cross-process vault writer gate.
                // Keep WinUI responsive during its bounded wait and I/O.
                if (selectedTag is Guid)
                {
                    // Bind deletion to exactly the unreadable ciphertext
                    // selected before the confirmation dialog was opened.
                    if (!inventory.UnavailableSnapshots.TryGetValue(selectedId, out var fingerprint))
                        return;
                    await Task.Run(() => store.DeleteUnavailable(selectedId, fingerprint));
                }
                else
                    await Task.Run(() => store.Delete(selectedId));
                if (_closing || !_udsResultLifetime.CanDisplay(generation)) return;
                SetStatus(CemText("Сохранённая запись PIN удалена.", "Saved PIN record deleted."));
            }
            else if (action == ContentDialogResult.Primary && selectedTag is CemPinSavedEntry selected)
            {
                loaded = await Task.Run(() => store.Load(selected.Id));
                // Loading DPAPI material can also outlive a context edit.
                // Dispose the newly loaded secret rather than show stale PIN.
                if (_closing || !_udsResultLifetime.CanDisplay(generation)) return;
                // A previous PIN may still be backed by a live expiry timer.
                // Dispose and zero it before replacing the confidential view.
                ClearCemPinMaterial();
                _cemPinResult = CemPinAttemptResult.Restored(loaded);
                loaded = null;
                _cemPinLoadedFromStore = true;
                CemPinResultText.Text = CemText("Сохранённый PIN скрыт. Текущий автомобиль не проверен.",
                    "Saved PIN hidden. The current vehicle has not been checked.");
                CemPinRevealButton.IsEnabled = true;
                StartCemPinExpiry();
                RefreshCemPinProgress();
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
