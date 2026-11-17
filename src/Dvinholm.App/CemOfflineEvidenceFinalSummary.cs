namespace Dvinholm.App;

/// <summary>
/// A combined REDACTED presentation of the same three user-selected,
/// locally validated inputs. Only ReviewFinalSummaryAsync guarantees
/// a single coherent file-inspection pass. All claims stay unverified.
/// </summary>
internal sealed class CemOfflineEvidenceFinalSummary
{
    internal CemConfigurationResearchChangeCoverage Research { get; }
    internal CemOfflineEvidenceFreshnessAssessment Freshness { get; }
    internal int ChangedRawIndices => Research.ChangedOrdinalCount;
    internal int UnverifiedChangedExactClaims =>
        Research.ChangedExactUnverifiedClaimCount;
    internal int ChangedWithoutExactClaim =>
        Research.ChangedWithoutExactClaimCount;
    internal int LocalSourcesNeedingRecheck =>
        Freshness.NeedsNewLocalReviewCount;
    internal int IndependentPhysicalGatesOpen =>
        Freshness.Ledger.OpenPhysicalEvidenceGates.Count;

    internal bool HasVerifiedSamePhysicalCem => false;
    internal bool HasVerifiedSourceProvenance => false;
    internal bool HasVerifiedOptionMeaning => false;
    internal bool MayReadLiveConfiguration => false;
    internal bool MayAcquirePin => false;
    internal bool MayWriteConfiguration => false;
    internal bool IsRestorableBackup => false;

    internal CemOfflineEvidenceFinalSummary(
        CemConfigurationResearchChangeCoverage research,
        CemOfflineEvidenceFreshnessAssessment freshness,
        CemOfflineProvenanceAssessment provenance)
    {
        ArgumentNullException.ThrowIfNull(research);
        ArgumentNullException.ThrowIfNull(freshness);
        ArgumentNullException.ThrowIfNull(provenance);
        if (research.ChangedOrdinalCount != provenance.ChangedRawIndices ||
            research.TotalRawOrdinals is not (140 or 252) ||
            freshness.Sources.Count != 3 ||
            freshness.Ledger.OpenPhysicalEvidenceGates.Count != 7 ||
            provenance.OpenPhysicalEvidenceGates.Count != 7 ||
            freshness.Ledger.InitiallyMatchingLocalDigests !=
                provenance.LocallyMatchingFingerprintCount)
            throw new InvalidDataException(
                "Inconsistent offline CEM evidence sections.");

        Research = research;
        Freshness = freshness;
    }

    internal string CreateRedactedReport(bool english)
    {
        var title = english
            ? "DVINHOLM CEM OFFLINE RESEARCH — CONSOLIDATED REDACTED REPORT"
            : "DVINHOLM CEM: ИТОГОВЫЙ ОБЕЗЛИЧЕННЫЙ ОФЛАЙН-ОТЧЁТ";
        var warning = english
            ? "USER-ASSERTED RESEARCH ONLY. Structural checks and SHA-256 DO NOT authenticate an ECU."
            : "ТОЛЬКО ЗАЯВЛЕННЫЕ ИССЛЕДОВАТЕЛЬСКИЕ ДАННЫЕ. Структура и SHA-256 НЕ подтверждают подлинность ЭБУ.";
        var separator = Environment.NewLine + Environment.NewLine + "---" +
            Environment.NewLine + Environment.NewLine;
        return title + Environment.NewLine + warning + separator +
            Research.CreateRedactedReport(english) + separator +
            Freshness.CreateRedactedReport(english) + separator +
            (english
                ? "FINAL GATE: OPEN 7/7. No vehicle reads, PIN, configuration writes or backup restoration."
                : "ИТОГ: ОТКРЫТО 7/7. Нет чтения автомобиля, PIN, записи конфигурации или восстановления.");
    }
}
