namespace Dvinholm.App;

/// <summary>
/// Presentation-only inspection of the already-existing experimental,
/// source-defined CEM part-number observation prerequisites.
/// It does not open a session, query a vehicle or authorize PIN/configuration.
/// </summary>
internal sealed record CemReadOnlyPrerequisiteInput(
    bool Connected,
    bool Baud500K,
    bool Extended29Bit,
    bool HardwareCanFilters,
    bool NoUserFilter,
    bool MonitorStopped,
    bool DiagnosticGateIdle,
    bool SafeResearchContext);

internal enum CemReadOnlyPrerequisiteKind
{
    Connected,
    Baud500K,
    Extended29Bit,
    HardwareCanFilters,
    NoUserFilter,
    MonitorStopped,
    DiagnosticGateIdle,
    SafeResearchContext
}

internal sealed record CemReadOnlyPrerequisiteCheck(
    CemReadOnlyPrerequisiteKind Kind, bool Satisfied);

internal sealed record CemReadOnlyPrerequisiteAssessment(
    IReadOnlyList<CemReadOnlyPrerequisiteCheck> Checks)
{
    // This is only a snapshot of UI/session preconditions. Re-evaluate under
    // the live diagnostic gate before a separately authorized request.
    internal bool EligibleForSourceDefinedPartReadOnly => Checks.All(c => c.Satisfied);
    internal bool HasAuthenticatedPhysicalCem => false;
    internal bool MayAcquirePin => false;
    internal bool MayReadConfiguration => false;
    internal bool MayWriteConfiguration => false;
}

internal static class CemReadOnlyPrerequisiteReport
{
    internal static CemReadOnlyPrerequisiteAssessment Assess(
        CemReadOnlyPrerequisiteInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        return new(Array.AsReadOnly(new[]
        {
            new CemReadOnlyPrerequisiteCheck(CemReadOnlyPrerequisiteKind.Connected, input.Connected),
            new CemReadOnlyPrerequisiteCheck(CemReadOnlyPrerequisiteKind.Baud500K, input.Baud500K),
            new CemReadOnlyPrerequisiteCheck(CemReadOnlyPrerequisiteKind.Extended29Bit, input.Extended29Bit),
            new CemReadOnlyPrerequisiteCheck(CemReadOnlyPrerequisiteKind.HardwareCanFilters, input.HardwareCanFilters),
            new CemReadOnlyPrerequisiteCheck(CemReadOnlyPrerequisiteKind.NoUserFilter, input.NoUserFilter),
            new CemReadOnlyPrerequisiteCheck(CemReadOnlyPrerequisiteKind.MonitorStopped, input.MonitorStopped),
            new CemReadOnlyPrerequisiteCheck(CemReadOnlyPrerequisiteKind.DiagnosticGateIdle, input.DiagnosticGateIdle),
            new CemReadOnlyPrerequisiteCheck(CemReadOnlyPrerequisiteKind.SafeResearchContext, input.SafeResearchContext)
        }));
    }

    internal static string Format(CemReadOnlyPrerequisiteAssessment assessment, bool english)
    {
        ArgumentNullException.ThrowIfNull(assessment);
        if (assessment.Checks.Count != 8 ||
            assessment.Checks.Select(c => c.Kind).Distinct().Count() != 8 ||
            assessment.Checks.Any(c => !Enum.IsDefined(c.Kind)))
            throw new InvalidDataException("Invalid CEM prerequisite assessment.");

        var title = english
            ? "Experimental part-number read prerequisites (local inspection only)"
            : "Условия экспериментального чтения номера детали (только локальная проверка)";
        var state = assessment.EligibleForSourceDefinedPartReadOnly
            ? english ? "All session conditions appear satisfied; consent and a new live check are still required."
                : "Условия сессии выполнены; всё равно нужны согласие и повторная проверка перед запросом."
            : english ? "Source-defined part-number read is currently unavailable."
                : "Чтение номера детали по экспериментальному маршруту сейчас недоступно.";
        var rows = assessment.Checks.Select(item =>
            (item.Satisfied ? "[OK] " : "[--] ") + Label(item.Kind, english));
        var warning = english
            ? "No request was sent. This does not authenticate physical CEM identity, firmware, PIN support or configuration access."
            : "Запросов к автомобилю не было. Это не подтверждает физический CEM, прошивку, поддержку PIN или доступ к конфигурации.";
        return string.Join(Environment.NewLine, new[] { title, state }
            .Concat(rows).Append(warning));
    }

    private static string Label(CemReadOnlyPrerequisiteKind kind, bool english) =>
        kind switch
        {
            CemReadOnlyPrerequisiteKind.Connected =>
                english ? "J2534 session connected" : "Сессия J2534 подключена",
            CemReadOnlyPrerequisiteKind.Baud500K =>
                english ? "CAN set to 500 kbit/s" : "CAN настроен на 500 кбит/с",
            CemReadOnlyPrerequisiteKind.Extended29Bit =>
                english ? "29-bit CAN mode selected" : "Выбран 29-битный режим CAN",
            CemReadOnlyPrerequisiteKind.HardwareCanFilters =>
                english ? "Adapter supports hardware CAN filters" : "Адаптер поддерживает аппаратные CAN-фильтры",
            CemReadOnlyPrerequisiteKind.NoUserFilter =>
                english ? "No user hardware filter active" : "Пользовательский HW-фильтр выключен",
            CemReadOnlyPrerequisiteKind.MonitorStopped =>
                english ? "CAN monitor stopped" : "Монитор CAN остановлен",
            CemReadOnlyPrerequisiteKind.DiagnosticGateIdle =>
                english ? "No concurrent diagnostic operation" : "Нет параллельной диагностической операции",
            CemReadOnlyPrerequisiteKind.SafeResearchContext =>
                english ? "Development route enabled, not demo/offline/closing" :
                    "Исследовательский режим включён, без демо/офлайн/закрытия",
            _ => throw new InvalidDataException("Unknown CEM requirement.")
        };
}
