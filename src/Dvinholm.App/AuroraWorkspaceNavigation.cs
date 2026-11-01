namespace Dvinholm.App;

internal sealed record AuroraSection(string Panel, string Title, string Description);

/// <summary>Presentation destinations only. No connection or ECU operation is implied.</summary>
internal static class AuroraWorkspaceNavigation
{
    internal static AuroraSection Describe(AuroraDestination destination) => destination switch
    {
        AuroraDestination.CemPinReadiness => new("CemPinPage", "CEM PIN", "Идентификация блока, ограниченный поиск и защищённое хранилище PIN."),
        AuroraDestination.CemConfigurationCatalog => new("CemConfigPage", "Конфигурация CEM", "Варианты оборудования и сведения о совместимости Volvo P2."),
        AuroraDestination.DtcFaults => new("DtcPage", "Ошибки / DTC", "Чтение кодов и статусов одного выбранного ECU. Стирание недоступно до проверки протокола."),
        AuroraDestination.EcuWorkspace => new("EcuPage", "Блоки ECU", "Наблюдение ответов и выбор блока для дальнейшей диагностики."),
        AuroraDestination.DiagnosticsWorkspace => new("DiagnosticsPage", "Диагностика", "Чтение VIN и параметров выбранного ECU. Адреса требуют проверки."),
        AuroraDestination.CanMonitor => new("CanMonitorPage", "CAN монитор", "Поток сообщений, фильтры и инструменты анализа шины."),
        AuroraDestination.EcuProfiles => new("ProfilesPage", "Профили ECU", "Библиотека адресов, диагностических параметров и локальных снимков."),
        AuroraDestination.J2534Connection => new("ConnectionPage", "Подключение", "Настройка J2534-адаптера и физического CAN-канала."),
        _ => throw new ArgumentOutOfRangeException(nameof(destination))
    };

    internal static bool TryParse(string? tag, out AuroraDestination destination) =>
        Enum.TryParse(tag, out destination) && Enum.IsDefined(destination);
}
