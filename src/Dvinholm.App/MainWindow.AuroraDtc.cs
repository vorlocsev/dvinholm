using System.Collections.ObjectModel;
using Microsoft.UI.Xaml.Controls;

namespace Dvinholm.App;

/// <summary>
/// Plain presentation row sourced from a completed, explicitly requested
/// single-ECU read. Neither raw 24-bit DTC codes nor status bytes imply
/// an OEM fault cause, severity, repair advice or physical ECU identity.
/// </summary>
internal sealed record AuroraDtcRow(string Code, string Description, string Status, string Details);

internal static class AuroraDtcTableProjection
{
    internal const string MissingDescription = "Нет проверенного описания";
    internal const string MissingDescriptionEnglish = "No verified description";
    internal const int MaxVisibleRows = 1024;

    internal static IReadOnlyList<AuroraDtcRow> Project(UdsDtcReadResult read, bool english = false)
    {
        ArgumentNullException.ThrowIfNull(read);
        ArgumentNullException.ThrowIfNull(read.Records);
        var rows = new List<AuroraDtcRow>(Math.Min(read.Records.Count, MaxVisibleRows));
        foreach (var record in read.Records.Take(MaxVisibleRows))
        {
            if (record.Code > 0xFFFFFF)
                throw new ArgumentOutOfRangeException(nameof(read), "DTC must fit in 24 bits.");

            var supported = (byte)(record.Status & read.StatusAvailabilityMask);
            var unsupported = (byte)(record.Status & ~read.StatusAvailabilityMask);
            var status = supported == 0
                ? (english ? "Status not verified" : "Статус не подтверждён")
                : UdsDtcStatusFormatter.Format(supported, english);
            var extra = unsupported == 0 ? string.Empty
                : english
                    ? $" · unsupported bits 0x{unsupported:X2}"
                    : $" · неподдерживаемые биты 0x{unsupported:X2}";
            rows.Add(new AuroraDtcRow(
                $"0x{record.Code:X6}",
                english ? MissingDescriptionEnglish : MissingDescription,
                $"0x{record.Status:X2}: {status}{extra}",
                english
                    ? $"Code: 0x{record.Code:X6}. Raw status: 0x{record.Status:X2}. " +
                      $"ECU-supported mask: 0x{read.StatusAvailabilityMask:X2}. " +
                      $"Supported status: {status}.{extra} " +
                      "No verified Volvo fault cause, description or repair."
                    : $"Код: 0x{record.Code:X6}. Исходный статус: 0x{record.Status:X2}. " +
                      $"Маска поддерживаемых ECU битов: 0x{read.StatusAvailabilityMask:X2}. " +
                      $"Поддерживаемые биты статуса: {status}.{extra} " +
                      "Причина, описание Volvo и способ устранения не подтверждены."));
        }

        return rows;
    }
}

public sealed partial class MainWindow
{
    private readonly ObservableCollection<AuroraDtcRow> _auroraDtcRows = new();
    // Retain only the latest session-scoped DTC read already displayed in
    // this workspace, so language switches can re-render it without CAN I/O.
    // ClearAuroraDtcTable releases it on any diagnostic-context invalidation.
    private UdsDtcReadResult? _lastAuroraDtcRead;

    private void InitializeAuroraDtcTable()
    {
        if (DtcRecordsView is null) return;
        DtcRecordsView.ItemsSource = _auroraDtcRows;
        ClearAuroraDtcTable();
    }

    private void ClearAuroraDtcTable()
    {
        _lastAuroraDtcRead = null;
        _auroraDtcRows.Clear();
        if (DtcTableSummary is not null)
            DtcTableSummary.Text = L(
                "Коды не считывались в текущем диагностическом контексте.",
                "No DTCs read in the current diagnostic context.");
        if (DtcSelectedRecordDetails is not null)
            DtcSelectedRecordDetails.Text = L(
                "Выберите считанную ошибку, чтобы посмотреть сырые данные и маску статуса.",
                "Select a DTC to inspect its raw code and status mask.");
    }

    private void PublishAuroraDtcTable(UdsDtcReadResult result)
    {
        _lastAuroraDtcRead = result;
        RefreshAuroraDtcTableLocalization();
    }

    private void RefreshAuroraDtcTableLocalization()
    {
        if (DtcTableSummary is null || DtcSelectedRecordDetails is null)
            return;
        if (_lastAuroraDtcRead is not { } result)
        {
            DtcTableSummary.Text = L(
                "Коды не считывались в текущем диагностическом контексте.",
                "No DTCs read in the current diagnostic context.");
            DtcSelectedRecordDetails.Text = L(
                "Выберите считанную ошибку, чтобы посмотреть сырые данные и маску статуса.",
                "Select a DTC to inspect its raw code and status mask.");
            return;
        }

        var selectedCode = (DtcRecordsView.SelectedItem as AuroraDtcRow)?.Code;
        var rows = AuroraDtcTableProjection.Project(result, _unifiedEnglish);
        _auroraDtcRows.Clear();
        foreach (var row in rows) _auroraDtcRows.Add(row);
        DtcTableSummary.Text = result.Records.Count == 0
            ? L(
                "ECU вернул 0 записей для выбранного фильтра. Это не доказывает отсутствие неисправностей.",
                "The selected ECU returned 0 DTCs for this filter. This does not prove the vehicle is fault-free.")
            : _unifiedEnglish
                ? $"Received {result.Records.Count} DTCs from one ECU · showing {rows.Count}. " +
                  $"ECU status availability mask: 0x{result.StatusAvailabilityMask:X2}."
                : $"Получено {result.Records.Count} записей одного ECU · показано {rows.Count}. " +
                  $"Маска поддерживаемых битов: 0x{result.StatusAvailabilityMask:X2}.";
        var selected = rows.FirstOrDefault(row =>
            string.Equals(row.Code, selectedCode, StringComparison.Ordinal));
        if (selected is not null)
            DtcRecordsView.SelectedItem = selected;
        DtcSelectedRecordDetails.Text = selected?.Details ?? L(
            "Выберите строку для просмотра исходного кода и битов статуса.",
            "Select a row to inspect the raw code and status bits.");
    }

    private void DtcRecordsView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DtcSelectedRecordDetails is null) return;
        DtcSelectedRecordDetails.Text = DtcRecordsView.SelectedItem is AuroraDtcRow row
            ? row.Details
            : L("Выберите считанную ошибку, чтобы посмотреть сырые данные и маску статуса.",
                "Select a DTC to inspect its raw code and status mask.");
    }
}
