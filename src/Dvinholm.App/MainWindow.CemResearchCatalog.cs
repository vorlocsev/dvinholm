using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Automation;
using WinRT.Interop;
using Windows.Storage.Pickers;

namespace Dvinholm.App;

/// <summary>
/// Independent local CEM index research viewer. No saved user data,
/// changes to ECU profile, CAN/UDS, live CEM read, PIN or write.
/// </summary>
public sealed partial class MainWindow
{
    private bool _cemResearchViewerBusy;

    private async void CemConfigResearchJson_Click(object sender, RoutedEventArgs e)
    {
        if (_closing || _cemResearchViewerBusy || _snapshotInspectionBusy)
            return;

        _cemResearchViewerBusy = true;
        CemConfigResearchJsonButton.IsEnabled = false;
        try
        {
            var picker = new FileOpenPicker();
            InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
            picker.FileTypeFilter.Add(".json");
            var selected = await picker.PickSingleFileAsync();
            if (selected is null || _closing)
                return;

            var research = await CemConfigurationResearchCatalogImport.InspectFileAsync(
                selected.Path);
            if (_closing)
                return;

            await ShowCemResearchCatalogViewerAsync(research);
            if (!_closing)
                SetStatus(L(
                    "Локальный каталог P2/P3 просмотрен офлайн. Соответствия и идентификация CEM не подтверждены.",
                    "Local P2/P3 catalog reviewed offline. No CEM mappings or identity verified."));
        }
        catch (InvalidDataException)
        {
            if (!_closing)
                SetStatus(L(
                    "Каталог P2/P3 отклонён: неверный JSON, формат, индекс или источник. Данные автомобиля не изменены.",
                    "P2/P3 catalog rejected: invalid JSON, format, index or evidence. No vehicle data changed."));
        }
        catch (IOException)
        {
            if (!_closing)
                SetStatus(L(
                    "Не удалось прочитать выбранный локальный каталог P2/P3. Ничего не импортировано.",
                    "Unable to read selected local P2/P3 catalog. Nothing imported."));
        }
        catch (UnauthorizedAccessException)
        {
            if (!_closing)
                SetStatus(L(
                    "Нет доступа к локальному файлу P2/P3. Автомобиль не затронут.",
                    "No permission to open local P2/P3 file. Vehicle untouched."));
        }
        finally
        {
            _cemResearchViewerBusy = false;
            if (!_closing)
                CemConfigResearchJsonButton.IsEnabled = true;
        }
    }

    private async Task ShowCemResearchCatalogViewerAsync(
        CemConfigurationResearchCatalogDocument catalog)
    {
        CemConfigurationIndexedSnapshot? offlineBody = null;
        CemConfigurationIndexedFormat? selectedFormat = null;
        string? bodyFeedback = null;

        // Choose the local body only after the modal dialog has closed.
        // A WinRT FileOpenPicker must never be launched under a live ContentDialog.
        while (!_closing)
        {
            var filter = new ComboBox
            {
                Header = L("Поколение данных", "Data generation"),
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            filter.Items.Add(new ComboBoxItem
            {
                Tag = "All", Content = L("Все P2 и P3", "All P2 and P3")
            });
            filter.Items.Add(new ComboBoxItem { Tag = "P2", Content = "P2 B9FC" });
            filter.Items.Add(new ComboBoxItem { Tag = "P3", Content = "P3 F106" });

            var summary = new TextBlock
            {
                TextWrapping = TextWrapping.Wrap,
                Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                    Windows.UI.Color.FromArgb(255, 181, 211, 228))
            };
            var warning = new TextBlock
            {
                Text = L(
                    "НЕПОДТВЕРЖДЁННЫЕ ДАННЫЕ: названия, номера и версии взяты из выбранного JSON. " +
                    "Бинарный блок читается только с диска. Нет идентификации автомобиля, чтения ECU или записи CEM.",
                    "UNVERIFIED RESEARCH: names, part numbers and revisions are supplied by local JSON. " +
                    "An optional binary body is read only from disk. No vehicle identity, ECU read or CEM write."),
                TextWrapping = TextWrapping.Wrap
            };
            var list = new ListView
            {
                Height = 176,
                SelectionMode = ListViewSelectionMode.Single,
                // A wrapping DataTemplate keeps 100-character source claims
                // readable at the approved 1280x800 DIP client viewport.
                ItemTemplate = (DataTemplate)RootLayout.Resources[
                    "CemResearchCatalogRowTemplate"],
                HorizontalContentAlignment = HorizontalAlignment.Stretch
            };
            var detail = new TextBlock
            {
                TextWrapping = TextWrapping.Wrap,
                IsTextSelectionEnabled = true,
                Text = L("Выберите исследовательскую запись.", "Select a research claim.")
            };
            // Stable accessibility anchors disclose no private user data.
            AutomationProperties.SetAutomationId(filter, "DvinholmCemResearchGenerationFilter");
            AutomationProperties.SetAutomationId(list, "DvinholmCemResearchClaimsList");
            AutomationProperties.SetName(list, L("Исследовательские записи CEM (не подтверждены)",
                "CEM research claims (unverified)"));
            AutomationProperties.SetAutomationId(warning, "DvinholmCemResearchSafetyWarning");
            AutomationProperties.SetAutomationId(detail, "DvinholmCemResearchClaimDetails");

            void Refresh()
            {
                var rows = CemConfigurationResearchCatalogProjection.Filter(
                    catalog, selectedFormat, offlineBody, _unifiedEnglish);
                list.ItemsSource = rows;
                summary.Text = L(
                    $"Записей: {rows.Count} из {catalog.Claims.Count}. " +
                    (offlineBody is null
                        ? "Бинарный блок не выбран."
                        : $"Локальный блок: {offlineBody.Format}. Только сырые байты.") +
                    (bodyFeedback is null ? "" : " " + bodyFeedback),
                    $"Showing {rows.Count} of {catalog.Claims.Count} claims. " +
                    (offlineBody is null
                        ? "No local binary body selected."
                        : $"Local body: {offlineBody.Format}. Raw bytes only.") +
                    (bodyFeedback is null ? "" : " " + bodyFeedback));
                detail.Text = L("Выберите исследовательскую запись.",
                    "Select a research claim.");
            }

            filter.SelectionChanged += (_, _) =>
            {
                selectedFormat = (filter.SelectedItem as ComboBoxItem)?.Tag as string switch
                {
                    "P2" => CemConfigurationIndexedFormat.P2B9Fc,
                    "P3" => CemConfigurationIndexedFormat.P3F106,
                    _ => null
                };
                Refresh();
            };
            list.SelectionChanged += (_, _) =>
            {
                if (list.SelectedItem is not CemConfigurationResearchViewRow row)
                    return;
                detail.Text =
                    L("Индекс (не адрес CEM): ", "Ordinal (not CEM address): ")
                    + row.Index1Based + Environment.NewLine
                    + L("Заявленный аппаратный номер: ", "Claimed hardware part: ")
                    + row.HardwarePartNumber + Environment.NewLine
                    + L("Заявленная версия ПО: ", "Claimed software revision: ")
                    + row.SoftwareRevision + Environment.NewLine
                    + L("Заявленное значение: ", "Claimed meaning: ")
                    + row.ClaimedMeaning + Environment.NewLine
                    + L("Источник (не проверен): ", "Evidence (not verified): ")
                    + row.EvidenceReference + Environment.NewLine
                    + (row.LocalRawHex is null
                        ? L("Совпадающий локальный P2/P3 блок не выбран.",
                            "No matching local P2/P3 body selected.")
                        : L("Сырой байт локального файла: 0x",
                            "Local file raw byte: 0x") + row.LocalRawHex)
                    + Environment.NewLine
                    + L("Подлинность, смысл параметра и права записи НЕ подтверждены.",
                        "Authenticity, meaning and write rights NOT verified.");
            };

            var content = new StackPanel { Spacing = 10 };
            content.Children.Add(warning);
            content.Children.Add(filter);
            content.Children.Add(summary);
            content.Children.Add(list);
            // Keep a selected claim visible within the fixed-height modal.
            // Longer references remain fully scrollable and selectable.
            content.Children.Add(new ScrollViewer
            {
                MaxHeight = 142,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Content = detail
            });
            filter.SelectedIndex = selectedFormat switch
            {
                CemConfigurationIndexedFormat.P2B9Fc => 1,
                CemConfigurationIndexedFormat.P3F106 => 2,
                _ => 0
            };
            Refresh();

            var dialog = new ContentDialog
            {
                XamlRoot = RootLayout.XamlRoot,
                Title = L("Локальный каталог CEM P2/P3 (офлайн)",
                    "Local CEM P2/P3 research (offline)"),
                Content = new ScrollViewer { MaxHeight = 430, Content = content },
                PrimaryButtonText = L("Сопоставить с .bin (офлайн)...",
                    "Compare with local .bin..."),
                CloseButtonText = L("Закрыть", "Close"),
                DefaultButton = ContentDialogButton.Close
            };
            AutomationProperties.SetAutomationId(dialog, "DvinholmCemResearchDialog");
            AutomationProperties.SetName(dialog, L(
                "Локальный каталог CEM P2/P3 (офлайн)",
                "Local CEM P2/P3 research (offline)"));
            var choice = await dialog.ShowAsync();
            if (_closing || choice != ContentDialogResult.Primary)
                return;

            // A separate, explicitly selected and bounded local raw body.
            var picker = new FileOpenPicker();
            InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
            picker.FileTypeFilter.Add(".bin");
            var file = await picker.PickSingleFileAsync();
            if (_closing)
                return;
            if (file is null)
                continue;
            try
            {
                offlineBody = await CemConfigurationOfflineBlockInspection.InspectFileAsync(
                    file.Path);
                bodyFeedback = null;
            }
            catch (InvalidDataException)
            {
                offlineBody = null;
                bodyFeedback = L(
                    "Локальный .bin отклонён: требуется ровно 142 или 254 байта с корректной суммой.",
                    "Local .bin rejected: requires exactly 142 or 254 bytes with valid checksum.");
            }
            catch (FormatException)
            {
                // The raw P2/P3 body decoder rejects invalid count/checksum.
                // Invalid user files are expected; they must not crash WinUI.
                offlineBody = null;
                bodyFeedback = L(
                    "Повреждённый .bin: неверное количество или контрольная сумма.",
                    "Corrupted .bin: invalid value count or checksum.");
            }
            catch (IOException)
            {
                offlineBody = null;
                bodyFeedback = L("Ошибка чтения локального .bin.",
                    "Unable to read local .bin.");
            }
            catch (UnauthorizedAccessException)
            {
                offlineBody = null;
                bodyFeedback = L("Нет доступа к локальному .bin.",
                    "Local .bin access denied.");
            }
        }
    }
}
