namespace Dvinholm.App;

/// <summary>
/// Pure offline consolidation of existing CEM/J2534 conditions. This is UI
/// feedback only: it cannot confer physical compatibility, PIN entitlement
/// or permission to send a command to a vehicle.
/// </summary>
internal enum CemPinExperimentBlocker
{
    Disconnected,
    WrongSpeed,
    WrongCanIdFormat,
    MissingHardwareFilterSupport,
    UserFilterActive,
    MonitorRunning,
    DiagnosticBusy,
    ResearchModeUnavailable,
    NoCurrentPartNumber,
    NoSourceProfile,
    InvalidAttemptRange,
    AttemptAlreadyRunning
}

internal sealed record CemPinExperimentAssessment(
    IReadOnlyList<CemPinExperimentBlocker> Blockers)
{
    internal bool AllLocalConditionsMet => Blockers.Count == 0;
    internal bool HasVerifiedPhysicalCem => false;
    internal bool HasVerifiedPin => false;
    internal bool MayWriteConfiguration => false;
}

internal static class CemPinExperimentReadiness
{
    internal static CemPinExperimentAssessment Assess(
        CemReadOnlyPrerequisiteAssessment prerequisites,
        bool currentPartNumberObserved, bool sourceProfileKnown,
        bool attemptRangeValid, bool attemptAlreadyRunning)
    {
        ArgumentNullException.ThrowIfNull(prerequisites);
        var values = prereqs(prerequisites);
        var blockers = new List<CemPinExperimentBlocker>(12);
        for (var i = 0; i < values.Length; i++)
        {
            if (!values[i])
                blockers.Add((CemPinExperimentBlocker)i);
        }
        if (!currentPartNumberObserved)
            blockers.Add(CemPinExperimentBlocker.NoCurrentPartNumber);
        if (!sourceProfileKnown)
            blockers.Add(CemPinExperimentBlocker.NoSourceProfile);
        if (!attemptRangeValid)
            blockers.Add(CemPinExperimentBlocker.InvalidAttemptRange);
        if (attemptAlreadyRunning)
            blockers.Add(CemPinExperimentBlocker.AttemptAlreadyRunning);
        return new(Array.AsReadOnly(blockers.ToArray()));
    }

    private static bool[] prereqs(CemReadOnlyPrerequisiteAssessment value)
    {
        var expected = Enum.GetValues<CemReadOnlyPrerequisiteKind>();
        if (value.Checks.Count != expected.Length ||
            expected.Length != 8 ||
            value.Checks.Where((item, index) =>
                item.Kind != expected[index]).Any())
            throw new InvalidDataException("Invalid CEM prerequisite assessment.");
        return value.Checks.Select(item => item.Satisfied).ToArray();
    }

    internal static string Format(CemPinExperimentAssessment assessment, bool english)
    {
        ArgumentNullException.ThrowIfNull(assessment);
        var blockers = assessment.Blockers;
        if (blockers.Count != blockers.Distinct().Count() ||
            blockers.Any(x => !Enum.IsDefined(x)) ||
            blockers.Count > 12)
            throw new InvalidDataException("Invalid CEM PIN preflight blockers.");

        var state = assessment.AllLocalConditionsMet
            ? english
                ? "Local prerequisites appear satisfied. Explicit authorization and a fresh live check remain necessary."
                : "Локальные условия выполнены. Требуются явное согласие и повторная проверка перед процедурой."
            : english
                ? "Not ready: " + string.Join("; ", blockers.Select(x => Label(x, true)))
                : "Не готово: " + string.Join("; ", blockers.Select(x => Label(x, false)));
        var limit = english
            ? "Offline inspection only. No CAN request sent. Physical CEM identity, firmware and PIN compatibility are unverified."
            : "Только локальная проверка, без CAN-запроса. Физический CEM, прошивка и совместимость с PIN не подтверждены.";
        return state + Environment.NewLine + limit;
    }

    private static string Label(CemPinExperimentBlocker blocker, bool english) =>
        blocker switch
        {
            CemPinExperimentBlocker.Disconnected =>
                english ? "J2534 disconnected" : "J2534 не подключён",
            CemPinExperimentBlocker.WrongSpeed =>
                english ? "CAN is not 500 kbit/s" : "CAN не 500 кбит/с",
            CemPinExperimentBlocker.WrongCanIdFormat =>
                english ? "29-bit CAN required" : "нужен 29-битный CAN",
            CemPinExperimentBlocker.MissingHardwareFilterSupport =>
                english ? "hardware RX filter unavailable" : "нет аппаратного RX-фильтра",
            CemPinExperimentBlocker.UserFilterActive =>
                english ? "custom RX filter active" : "включён пользовательский RX-фильтр",
            CemPinExperimentBlocker.MonitorRunning =>
                english ? "CAN monitor running" : "CAN-монитор запущен",
            CemPinExperimentBlocker.DiagnosticBusy =>
                english ? "diagnostic operation active" : "диагностика занята",
            CemPinExperimentBlocker.ResearchModeUnavailable =>
                english ? "Development research context unavailable" : "исследовательский режим недоступен",
            CemPinExperimentBlocker.NoCurrentPartNumber =>
                english ? "no current CEM P/N observation" : "нет актуального CEM P/N",
            CemPinExperimentBlocker.NoSourceProfile =>
                english ? "source P/N route unavailable" : "нет исследовательского маршрута P/N",
            CemPinExperimentBlocker.InvalidAttemptRange =>
                english ? "invalid attempt range" : "неверный диапазон",
            CemPinExperimentBlocker.AttemptAlreadyRunning =>
                english ? "experiment already running" : "эксперимент выполняется",
            _ => throw new InvalidDataException("Unknown CEM PIN blocker.")
        };
}
