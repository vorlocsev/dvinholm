using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Dvinholm.App;

/// <summary>
/// Display projection of source-attributed P2 configuration research. Unknown
/// hardware mapping and unread vehicle values never become writable defaults.
/// </summary>
internal sealed record AuroraCemConfigRow(VolvoP2CemConfigOptionDefinition Option)
{
    public string Code => Option.CanonicalId;
    public string Name => Option.DisplayName;
    public string Current => "Не прочитано";
    public string Proposed => "—";
    public string Source => Option.Provenance switch
    {
        VolvoP2CemOptionProvenance.VolvoPublishedCemServiceMenu => "Volvo · меню",
        _ => "Сторонний источник"
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
        var categories = new[] { "Все категории" }
            .Concat(VolvoP2CemConfigOptionManifest.Entries
                .Select(row => row.Category)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(x => x, StringComparer.CurrentCulture))
            .ToArray();
        CemConfigCategory.ItemsSource = categories;
        CemConfigCategory.SelectedIndex = 0;
        _cemConfigReferenceReady = true;
        RefreshAuroraConfigReference();
    }

    private void CemConfigCategory_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_cemConfigReferenceReady) RefreshAuroraConfigReference();
    }

    private void CemConfigSearch_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_cemConfigReferenceReady) RefreshAuroraConfigReference();
    }

    private void RefreshAuroraConfigReference()
    {
        if (!_cemConfigReferenceReady) return;
        var category = CemConfigCategory.SelectedItem as string;
        var query = CemConfigSearch.Text?.Trim();
        // Search input is local, bounded, and never sent to a vehicle or log.
        if (query?.Length > 80) query = query[..80];
        var matches = VolvoP2CemConfigOptionManifest.Entries
            .Where(row => category is null || category == "Все категории" ||
                string.Equals(row.Category, category, StringComparison.Ordinal))
            .Where(row => string.IsNullOrWhiteSpace(query) ||
                row.CanonicalId.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                row.DisplayName.Contains(query, StringComparison.CurrentCultureIgnoreCase))
            .Take(128)
            .Select(row => new AuroraCemConfigRow(row))
            .ToArray();
        _cemConfigRows.Clear();
        foreach (var row in matches) _cemConfigRows.Add(row);
        CemConfigCount.Text = $"{matches.Length} справочных пунктов · значения автомобиля не прочитаны";
        CemConfigResults.SelectedIndex = -1;
        ClearAuroraConfigDetail();
    }

    private void CemConfigResults_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_cemConfigReferenceReady) return;
        if (CemConfigResults.SelectedItem is not AuroraCemConfigRow selected)
        {
            ClearAuroraConfigDetail();
            return;
        }
        var row = selected.Option;
        CemConfigDetailTitle.Text = row.DisplayName;
        CemConfigDetailCode.Text = "Идентификатор исследования: " + row.CanonicalId;
        CemConfigDetailSource.Text = "Источник: " + selected.Source;
        CemConfigDetailEvidence.Text = "Ссылка на свидетельство: " + row.EvidenceReference;
        CemConfigDetailValues.Text = "Текущее значение: неизвестно · допустимые значения: не подтверждены.";
        CemConfigDetailSafety.Text = row.AdditionalSafetyOrEquipmentReviewRequired
            ? "Нужно отдельно проверить оборудование, безопасность и совместимость."
            : "Физическое соответствие CEM и карта значений пока не подтверждены.";
    }

    private void ClearAuroraConfigDetail()
    {
        if (CemConfigDetailTitle is null) return;
        CemConfigDetailTitle.Text = "Выберите параметр";
        CemConfigDetailCode.Text = "Код и источник появятся после выбора.";
        CemConfigDetailSource.Text = string.Empty;
        CemConfigDetailEvidence.Text = string.Empty;
        CemConfigDetailValues.Text = "Значения автомобиля не считывались.";
        CemConfigDetailSafety.Text = "Изменение и запись недоступны до проверки протокола и резервного восстановления.";
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
