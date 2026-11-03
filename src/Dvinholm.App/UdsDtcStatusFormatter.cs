namespace Dvinholm.App;

/// <summary>
/// Formats the ISO 14229 DTC status byte. These flags describe ECU-reported
/// status, not the root cause or severity of a diagnostic trouble code.
/// </summary>
internal static class UdsDtcStatusFormatter
{
    private static readonly (byte Mask, string English, string Russian)[] Flags =
    [
        (0x01, "test failed", "тест не пройден"),
        (0x02, "failed this operation cycle", "тест не пройден в текущем цикле"),
        (0x04, "pending DTC", "ожидающий DTC"),
        (0x08, "confirmed DTC", "подтверждённый DTC"),
        (0x10, "test not completed since last clear", "тест не завершён с последней очистки"),
        (0x20, "failed since last clear", "тест не пройден с последней очистки"),
        (0x40, "test not completed this operation cycle", "тест не завершён в текущем цикле"),
        (0x80, "warning indicator requested", "запрошен индикатор неисправности")
    ];

    // Preserve the English default used by existing non-localized diagnostics.
    // The Aurora DTC table explicitly supplies the selected UI language.
    internal static string Format(byte status, bool english = true)
    {
        if (status == 0)
            return english ? "no status bits set" : "нет установленных битов статуса";

        var labels = Flags
            .Where(flag => (status & flag.Mask) != 0)
            .Select(flag => english ? flag.English : flag.Russian);

        return string.Join(", ", labels);
    }
}
