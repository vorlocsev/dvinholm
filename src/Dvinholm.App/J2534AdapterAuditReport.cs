using Microsoft.Win32;
using System.Globalization;
using System.Text;

namespace Dvinholm.App;

/// <summary>
/// Read-only local report of registered J2534 native DLL architecture.
/// Does not execute a driver or disclose registry keys, DLL paths or
/// environment variables. A PE match alone does not prove a working adapter.
/// </summary>
internal enum J2534AdapterAuditOutcome
{
    CompatibleArchitecture,
    WrongBitness,
    WrongRegistryView,
    Arm64Unsupported,
    UnreadableOrUnknown
}

internal sealed record J2534AdapterAuditItem(
    string DisplayName, J2534AdapterAuditOutcome Outcome);

internal sealed record J2534AdapterAuditSummary(
    int Inspected,
    int Compatible,
    int Rejected,
    bool Truncated,
    IReadOnlyList<J2534AdapterAuditItem> Items)
{
    internal bool ConfirmsPhysicalAdapter => false;
    internal bool HasInstalledVerifiedVendorDriver => false;
    internal bool MayOpenVehicleConnection => false;
}

internal static class J2534AdapterAuditReport
{
    internal const int MaximumRows = 16;
    internal const int MaximumAuditEntries = 256;

    internal static J2534AdapterAuditSummary Assess(
        IEnumerable<J2534DeviceDiscovery.RegisteredAudit> audit,
        bool is64BitProcess)
    {
        ArgumentNullException.ThrowIfNull(audit);
        // We process at most 257 records to avoid unbounded UI work when
        // encountering anomalous registry discovery results.
        var entries = audit.Take(MaximumAuditEntries + 1).ToArray();
        var truncated = entries.Length > MaximumAuditEntries;
        var inspected = Math.Min(entries.Length, MaximumAuditEntries);
        var items = entries.Take(inspected)
            .Select(entry =>
            {
                if (entry is null)
                    throw new InvalidDataException("Null J2534 registration audit.");
                return new J2534AdapterAuditItem(
                    RedactName(entry.Device?.Name),
                    Classify(entry.View, entry.Architecture, is64BitProcess));
            })
            .OrderBy(item => item.Outcome)
            .ThenBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var shown = items.Take(MaximumRows).ToArray();
        return new(inspected,
            items.Count(item => item.Outcome == J2534AdapterAuditOutcome.CompatibleArchitecture),
            items.Count(item => item.Outcome != J2534AdapterAuditOutcome.CompatibleArchitecture),
            truncated || items.Length > shown.Length,
            Array.AsReadOnly(shown));
    }

    internal static J2534AdapterAuditOutcome Classify(
        RegistryView view, J2534PeArchitecture architecture, bool is64BitProcess)
    {
        if (architecture == J2534PeArchitecture.Unknown)
            return J2534AdapterAuditOutcome.UnreadableOrUnknown;
        if (architecture == J2534PeArchitecture.Arm64)
            return J2534AdapterAuditOutcome.Arm64Unsupported;
        if (!J2534PeInspector.CanLoad(architecture, is64BitProcess))
            return J2534AdapterAuditOutcome.WrongBitness;
        return view == J2534DeviceDiscovery.RegistryViewForProcess(is64BitProcess)
            ? J2534AdapterAuditOutcome.CompatibleArchitecture
            : J2534AdapterAuditOutcome.WrongRegistryView;
    }

    internal static string Format(J2534AdapterAuditSummary summary, bool english,
        bool is64BitProcess)
    {
        ArgumentNullException.ThrowIfNull(summary);
        if (summary.Inspected < 0 || summary.Compatible < 0 || summary.Rejected < 0 ||
            summary.Compatible + summary.Rejected != summary.Inspected ||
            summary.Items is null || summary.Items.Count > MaximumRows ||
            summary.Items.Any(item => item is null ||
                !Enum.IsDefined(item.Outcome) || item.DisplayName.Length > 56))
            throw new InvalidDataException("Malformed J2534 audit summary.");

        var text = new StringBuilder();
        text.AppendLine(english ? "Registered adapter DLL audit (read-only)" :
            "Аудит зарегистрированных J2534 DLL (только чтение)");
        text.Append(english ? "App architecture: " : "Разрядность приложения: ")
            .AppendLine(is64BitProcess ? "x64" : "x86");
        text.Append(english ? "Inspected: " : "Проверено: ")
            .Append(summary.Inspected.ToString(CultureInfo.InvariantCulture))
            .Append(english ? " · matching DLLs: " : " · совпадений: ")
            .Append(summary.Compatible.ToString(CultureInfo.InvariantCulture))
            .Append(english ? " · rejected: " : " · несовместимых: ")
            .AppendLine(summary.Rejected.ToString(CultureInfo.InvariantCulture));
        if (summary.Inspected == 0)
            text.AppendLine(english
                ? "No existing registered DLL files were located in the scanned views."
                : "В проверенных разделах реестра не найдены существующие зарегистрированные DLL.");
        foreach (var item in summary.Items)
            text.Append("• ").Append(item.DisplayName).Append(": ")
                .AppendLine(Status(item.Outcome, english));

        if (summary.Truncated)
            text.AppendLine(english
                ? "Result list truncated; additional entries are not displayed."
                : "Список ограничен; дополнительные записи не показаны.");

        text.AppendLine(english
            ? "PE architecture and registry view only. Does not verify a driver, cable, DiCE/Mongoose, Windows installation or physical vehicle connection."
            : "Проверены только PE-разрядность и реестр. Не подтверждены работоспособность драйвера, кабеля, DiCE/Mongoose, установка Windows или связь с автомобилем.");
        text.AppendLine(english
            ? "No vendor DLL was loaded and no vehicle request was sent."
            : "DLL производителя не загружались, запросов к автомобилю не было.");
        return text.ToString();
    }

    private static string RedactName(string? name)
    {
        var value = name?.Trim();
        if (string.IsNullOrWhiteSpace(value) || value.Length > 56 ||
            value.Any(char.IsControl) || value.Contains(':') ||
            value.Contains('/') || value.Contains('\\') ||
            value.Contains('@'))
            return "J2534 adapter";
        return value;
    }

    private static string Status(J2534AdapterAuditOutcome outcome, bool english) =>
        outcome switch
        {
            J2534AdapterAuditOutcome.CompatibleArchitecture => english
                ? "matching architecture (not hardware-tested)" :
                  "разрядность подходит (оборудование не проверено)",
            J2534AdapterAuditOutcome.WrongBitness => english
                ? "wrong DLL bitness; matching driver required" :
                  "неподходящая разрядность DLL; нужен совместимый драйвер",
            J2534AdapterAuditOutcome.WrongRegistryView => english
                ? "registered in the other Windows registry view" :
                  "регистрация в другом разделе реестра Windows",
            J2534AdapterAuditOutcome.Arm64Unsupported => english
                ? "ARM64 DLL not supported for this process" :
                  "DLL ARM64 не поддерживается этим процессом",
            J2534AdapterAuditOutcome.UnreadableOrUnknown => english
                ? "unreadable or unknown PE header" :
                  "недоступный или неизвестный заголовок PE",
            _ => throw new InvalidDataException("Unknown J2534 audit classification.")
        };
}
