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
    internal const int MaxVisibleRows = 1024;

    internal static IReadOnlyList<AuroraDtcRow> Project(UdsDtcReadResult read)
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
                ? "Статус не подтверждён"
                : UdsDtcStatusFormatter.Format(supported);
            var extra = unsupported == 0 ? string.Empty
                : $" · неподдерживаемые биты 0x{unsupported:X2}";
            rows.Add(new AuroraDtcRow(
                $"0x{record.Code:X6}",
                MissingDescription,
                $"0x{record.Status:X2}: {status}{extra}",
                $"Код: 0x{record.Code:X6}. Raw status: 0x{record.Status:X2}. " +
                $"ECU-supported mask: 0x{read.StatusAvailabilityMask:X2}. " +
                $"Supported status: {status}.{extra} " +
                "Причина, описание Volvo и способ устранения не подтверждены."));
        }

        return rows;
    }
}

public sealed partial class MainWindow
{
    private readonly ObservableCollection<AuroraDtcRow> _auroraDtcRows = new();

    private void InitializeAuroraDtcTable()
    {
        if (DtcRecordsView is null) return;
        DtcRecordsView.ItemsSource = _auroraDtcRows;
        ClearAuroraDtcTable();
    }

    private void ClearAuroraDtcTable()
    {
        _auroraDtcRows.Clear();
        if (DtcTableSummary is not null)
            DtcTableSummary.Text = "Коды не считывались в текущем диагностическом контексте.";
        if (DtcSelectedRecordDetails is not null)
            DtcSelectedRecordDetails.Text = "Выберите считанную ошибку, чтобы посмотреть сырые данные и маску статуса.";
    }

    private void PublishAuroraDtcTable(UdsDtcReadResult result)
    {
        var rows = AuroraDtcTableProjection.Project(result);
        _auroraDtcRows.Clear();
        foreach (var row in rows) _auroraDtcRows.Add(row);
        DtcTableSummary.Text = result.Records.Count == 0
            ? "ECU вернул 0 записей для выбранного фильтра. Это не доказывает отсутствие неисправностей."
            : $"Получено {result.Records.Count} записей одного ECU · показано {rows.Count}. " +
              $"Маска поддерживаемых битов: 0x{result.StatusAvailabilityMask:X2}.";
        DtcSelectedRecordDetails.Text = "Выберите строку для просмотра исходного кода и битов статуса.";
    }

    private void DtcRecordsView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DtcSelectedRecordDetails is null) return;
        DtcSelectedRecordDetails.Text = DtcRecordsView.SelectedItem is AuroraDtcRow row
            ? row.Details
            : "Выберите считанную ошибку, чтобы посмотреть сырые данные и маску статуса.";
    }
}
