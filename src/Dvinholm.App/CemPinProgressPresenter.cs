namespace Dvinholm.App;

/// <summary>
/// Presentation-only CEM PIN stepper. The input is already-known UI/session
/// metadata; no PIN bytes, VIN, addresses, adapter paths or user identifiers.
/// A source profile or an adapter acceptance is NEVER authenticated ECU proof.
/// </summary>
internal sealed record CemPinProgressSnapshot(
    bool Development,
    bool SessionConnected,
    bool SessionReady,
    bool PartNumberObserved,
    bool SourceProfileKnown,
    bool RangeValid,
    bool AttemptRunning,
    bool CandidateAvailable,
    bool FromSaved,
    bool Revealed);

internal sealed record CemPinProgressCopy(string Identity, string Search, string Result)
{
    internal bool PhysicalPinVerified => false;
    internal bool MayWriteConfiguration => false;
}

internal static class CemPinProgressPresenter
{
    internal static CemPinProgressCopy Format(CemPinProgressSnapshot state, bool english)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (!state.Development)
            return new(
                english ? "Identification unverified" : "Идентификация не подтверждена",
                english ? "PIN unavailable (Stable)" : "PIN недоступен (Stable)",
                english ? "No result" : "Нет результата");

        var identity = state.PartNumberObserved
            ? english ? "P/N observed, not attested" : "P/N считан, без аттестации"
            : english ? "CEM P/N not read" : "Номер CEM не считан";

        var search = state.AttemptRunning
            ? english ? "Experiment running" : "Эксперимент выполняется"
            : !state.SessionConnected
                ? english ? "J2534 disconnected" : "J2534 не подключён"
            : !state.SessionReady
                ? english ? "Session prerequisites unmet" : "Условия сессии не выполнены"
            : !state.PartNumberObserved
                ? english ? "Read CEM P/N first" : "Сначала прочитайте P/N"
            : !state.SourceProfileKnown
                ? english ? "Source route unverified" : "Маршрут не подтверждён"
            : !state.RangeValid
                ? english ? "Check attempt range" : "Проверьте диапазон"
            : english ? "Experiment ready, not verified" : "Эксперимент доступен, не проверен";

        var result = !state.CandidateAvailable
            ? english ? "No result" : "Нет результата"
            : state.FromSaved
                ? state.Revealed
                    ? english ? "Saved candidate temporarily shown" : "Сохранённый кандидат временно показан"
                    : english ? "Saved candidate hidden" : "Сохранённый кандидат скрыт"
                : state.Revealed
                    ? english ? "Unverified candidate temporarily shown" : "Непроверенный кандидат временно показан"
                    : english ? "Unverified candidate hidden" : "Непроверенный кандидат скрыт";

        return new(identity, search, result);
    }
}
