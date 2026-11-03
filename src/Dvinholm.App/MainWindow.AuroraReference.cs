using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Dvinholm.App;

/// <summary>
/// Display projection of source-attributed P2 configuration research. Unknown
/// hardware mapping and unread vehicle values never become writable defaults.
/// </summary>
internal sealed record AuroraCemConfigRow(VolvoP2CemConfigOptionDefinition Option, bool English)
{
    public string Code => Option.CanonicalId;
    public string Name => AuroraCemCatalogLocalization.OptionLabel(Option, English);
    public string Current => English ? "Not read" : "Не прочитано";
    public string Proposed => "—";
    public string Source => Option.Provenance switch
    {
        VolvoP2CemOptionProvenance.VolvoPublishedCemServiceMenu =>
            English ? "Volvo service menu" : "Volvo · меню",
        _ => English ? "Third-party advertisement" : "Сторонний источник"
    };
}

public sealed partial class MainWindow
{
    private readonly ObservableCollection<AuroraCemConfigRow> _cemConfigRows = new();
    private bool _cemConfigReferenceReady;

    /// <summary>
    /// Loads local immutable evidence only; no J2534 connection, CAN request,
    /// VIN read, PIN operation or configuration write.
    /// </summary>
    private void InitializeAuroraConfigReference()
    {
        if (CemConfigCategory is null || CemConfigResults is null)
            return;

        _cemConfigReferenceReady = false;
        CemConfigResults.ItemsSource = _cemConfigRows;
        CemConfigCategory.ItemsSource = AuroraCemCatalogLocalization.Categories(_unifiedEnglish);
        CemConfigCategory.SelectedIndex = 0;
        _cemConfigReferenceReady = true;
        RefreshAuroraConfigReference();
    }

    private void CemConfigCategory_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_cemConfigReferenceReady) RefreshAuroraConfigReference();
    }

    private void CemConfigEvidenceTier_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_cemConfigReferenceReady) RefreshAuroraConfigReference();
    }

    private void CemConfigSearch_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_cemConfigReferenceReady) RefreshAuroraConfigReference();
    }

    private void ApplyAuroraCemConfigLocalization()
    {
        CemConfigCategory.Header = L("Группа", "Category");
        CemConfigEvidenceTier.Header = L("Источник", "Evidence source");
        CemConfigSearch.Header = L("Найти параметр", "Search research labels");
        CemConfigSearch.PlaceholderText = L("Код или название", "Code or name");

        // Category keys remain immutable regardless of displayed language.
        // Suppress SelectionChanged while repopulating, then restore the
        // canonical selection and previous research-row ID.
        var selectedCategory = (CemConfigCategory.SelectedItem as
            AuroraCemCategoryChoice)?.CanonicalCategory;
        var wasReady = _cemConfigReferenceReady;
        _cemConfigReferenceReady = false;
        try
        {
            var categories = AuroraCemCatalogLocalization.Categories(_unifiedEnglish);
            CemConfigCategory.ItemsSource = categories;
            var index = categories.ToList().FindIndex(choice =>
                string.Equals(choice.CanonicalCategory, selectedCategory,
                    StringComparison.Ordinal));
            CemConfigCategory.SelectedIndex = index < 0 ? 0 : index;
        }
        finally
        {
            _cemConfigReferenceReady = wasReady;
        }

        CemConfigPageTitle.Text = L("Конфигурация CEM", "CEM configuration");
        CemConfigPageIntro.Text = L(
            "Каталог Volvo P2 · офлайн. Выбор пункта показывает источник, но не читает или меняет автомобиль.",
            "Offline Volvo P2 catalog. Selecting a label shows its source, not a vehicle read or configuration edit.");
        CemConfigCategoriesTitle.Text = L("Категории", "Categories");
        CemConfigFilterSafetyNote.Text = L(
            "Данные получены из локального каталога источников. Совместимость не подтверждена.",
            "Only local research source labels are shown. Vehicle compatibility is unverified.");
        CemConfigResultsTitle.Text = L("Параметры и значения", "Research labels and values");
        CemConfigListParameterHeader.Text = L("Параметр (ID / название)",
            "Research label (ID / name)");
        CemConfigListCurrentHeader.Text = L("Текущее", "Current");
        CemConfigDetailHeading.Text = L("Описание", "Details");
        CemConfigReadOnlyNotice.Text = L(
            "Чтение, изменение и запись CEM пока заблокированы. Никаких реальных значений, адресов или прав на запись этот каталог не подтверждает.",
            "CEM reading, editing and writing remain blocked. No real values, addresses or write permissions are verified.");
        P2ConfigCatalogButton.Content = L("Источники (offline)", "Sources (offline)");
        CemConfigBackupButton.Content = L("Резервная копия", "Backup");
        CemConfigCompareButton.Content = L("Сравнить", "Compare");
        CemConfigPrepareButton.Content = L("Подготовить изменения", "Prepare changes");
        ToolTipService.SetToolTip(P2ConfigCatalogButton, L(
            "Только локальные источники. Без CAN, PIN и записи CEM.",
            "Offline sources only. No CAN, PIN or CEM write."));
        ToolTipService.SetToolTip(CemConfigBackupButton, L(
            "Чтение и резервирование реальной конфигурации CEM не подтверждены.",
            "Actual CEM configuration reading and backup are unverified."));
        ToolTipService.SetToolTip(CemConfigCompareButton, L(
            "Нет проверенного снимка для сравнения.",
            "No verified configuration snapshot available for comparison."));
        ToolTipService.SetToolTip(CemConfigPrepareButton, L(
            "Непроверенные параметры нельзя записывать в блок.",
            "Unverified parameters cannot be written to the ECU."));

        foreach (var item in CemConfigEvidenceTier.Items.OfType<ComboBoxItem>())
            item.Content = (item.Tag as string) switch
            {
                "Vendor" => L("Сторонние публикации", "Third-party publications"),
                "Volvo" => L("Меню Volvo", "Volvo service menus"),
                _ => L("Все источники", "All sources")
            };

        if (_cemConfigReferenceReady)
            RefreshAuroraConfigReference();
        else
            ClearAuroraConfigDetail();
    }

    private void RefreshAuroraConfigReference()
    {
        if (!_cemConfigReferenceReady) return;

        var category = (CemConfigCategory.SelectedItem as
            AuroraCemCategoryChoice)?.CanonicalCategory;
        var query = CemConfigSearch.Text;
        var tier = ((CemConfigEvidenceTier.SelectedItem as ComboBoxItem)?.Tag as string) switch
        {
            "Vendor" => VolvoP2CemCatalogEvidenceTier.VendorAdvertisement,
            "Volvo" => VolvoP2CemCatalogEvidenceTier.VolvoPublishedServiceMenu,
            _ => VolvoP2CemCatalogEvidenceTier.All
        };
        var previousSelection = (CemConfigResults.SelectedItem as AuroraCemConfigRow)?.Code;
        IReadOnlyList<VolvoP2CemConfigOptionDefinition> matches;
        try
        {
            matches = _unifiedEnglish
                ? AuroraCemConfigReferenceFilter.Find(query, category, tier)
                : AuroraCemConfigReferenceFilter.FindLocalized(query, category, tier);
        }
        catch (InvalidDataException)
        {
            // Reject invalid local search without leaking the input or using
            // stale results from a previously valid filter.
            matches = Array.Empty<VolvoP2CemConfigOptionDefinition>();
        }

        _cemConfigRows.Clear();
        foreach (var row in matches)
            _cemConfigRows.Add(new AuroraCemConfigRow(row, _unifiedEnglish));
        CemConfigCount.Text = L(
            $"{matches.Count} справочных пунктов · значения автомобиля не прочитаны",
            $"{matches.Count} research labels · no vehicle values read");

        var index = previousSelection is null ? -1 :
            _cemConfigRows.ToList().FindIndex(row => string.Equals(
                row.Code, previousSelection, StringComparison.Ordinal));
        CemConfigResults.SelectedIndex = index;
        if (index < 0)
            ClearAuroraConfigDetail();
        else
            PresentAuroraConfigDetail(_cemConfigRows[index]);
    }

    private void CemConfigResults_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_cemConfigReferenceReady) return;
        if (CemConfigResults.SelectedItem is not AuroraCemConfigRow selected)
        {
            ClearAuroraConfigDetail();
            return;
        }
        PresentAuroraConfigDetail(selected);
    }

    private void PresentAuroraConfigDetail(AuroraCemConfigRow selected)
    {
        var row = selected.Option;
        CemConfigDetailTitle.Text = AuroraCemCatalogLocalization.OptionLabel(
            row, _unifiedEnglish);
        CemConfigDetailCode.Text = L("Идентификатор исследования: ", "Research ID: ") + row.CanonicalId;
        CemConfigDetailSource.Text = L("Источник: ", "Source: ") + selected.Source;
        CemConfigDetailEvidence.Text = L("Ссылка на свидетельство: ", "Evidence reference: ") +
            row.EvidenceReference;
        CemConfigDetailValues.Text = L(
            "Текущее значение: неизвестно · допустимые значения: не подтверждены.",
            "Current value: unknown · permitted values: unverified.");
        CemConfigDetailSafety.Text = row.AdditionalSafetyOrEquipmentReviewRequired
            ? L("Нужно отдельно проверить оборудование, безопасность и совместимость.",
                "Hardware, safety and compatibility require independent review.")
            : L("Физическое соответствие CEM и карта значений пока не подтверждены.",
                "Physical CEM identity and value mapping remain unverified.");
    }

    private void ClearAuroraConfigDetail()
    {
        if (CemConfigDetailTitle is null) return;
        CemConfigDetailTitle.Text = L("Выберите параметр", "Select a research label");
        CemConfigDetailCode.Text = L("Код и источник появятся после выбора.",
            "Research code and source appear after selection.");
        CemConfigDetailSource.Text = string.Empty;
        CemConfigDetailEvidence.Text = string.Empty;
        CemConfigDetailValues.Text = L("Значения автомобиля не считывались.",
            "No vehicle values have been read.");
        CemConfigDetailSafety.Text = L(
            "Изменение и запись недоступны до проверки протокола и резервного восстановления.",
            "Editing and writing are disabled pending protocol and recovery validation.");
    }

    /// <summary>
    /// Model artwork selector is display-only. Distinct side profiles reflect
    /// a user's manual choice, not decoded vehicle identity or module position.
    /// </summary>
    private void EcuVehicleModel_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (EcuP2SideProfile is null || EcuP3SideProfile is null || EcuDiagramCaption is null)
            return;
        var p3 = EcuVehicleModel.SelectedIndex == 1;
        EcuP2SideProfile.Visibility = p3 ? Visibility.Collapsed : Visibility.Visible;
        EcuP3SideProfile.Visibility = p3 ? Visibility.Visible : Visibility.Collapsed;
        EcuDiagramCaption.Text = p3
            ? "XC70 P3 · условный боковой силуэт, выбран вручную"
            : "XC90 P2 · условный боковой силуэт, выбран вручную";
        // The selection cannot relabel passive RX IDs as specific ECUs.
    }
}
