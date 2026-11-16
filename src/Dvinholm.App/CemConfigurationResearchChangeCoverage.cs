using System.Globalization;

namespace Dvinholm.App;

/// <summary>
/// Cross-tabulation of two locally decoded body differences against the
/// FULL research-catalog coverage for exactly the same asserted scope.
/// Index positions, not option meanings, are the only join key.
/// </summary>
internal sealed class CemConfigurationResearchChangeCoverage
{
    internal CemConfigurationIndexedFormat Format { get; }
    internal int TotalRawOrdinals { get; }
    internal int ChangedOrdinalCount { get; }
    internal int FullExactUnverifiedClaimCount { get; }
    internal int FullNoIndexedClaimCount { get; }
    internal int ChangedExactUnverifiedClaimCount { get; }
    internal int ChangedOtherSoftwareRevisionCount { get; }
    internal int ChangedOtherHardwarePartCount { get; }
    internal int ChangedNoIndexedClaimCount { get; }
    internal int UnchangedExactUnverifiedClaimCount =>
        FullExactUnverifiedClaimCount - ChangedExactUnverifiedClaimCount;
    internal int UnchangedNoIndexedClaimCount =>
        FullNoIndexedClaimCount - ChangedNoIndexedClaimCount;
    internal int ChangedWithoutExactClaimCount =>
        ChangedOrdinalCount - ChangedExactUnverifiedClaimCount;
    internal double FullExactClaimPercent =>
        100.0 * FullExactUnverifiedClaimCount / TotalRawOrdinals;
    internal double ChangedExactClaimPercent =>
        ChangedOrdinalCount == 0 ? 0 : 100.0 * ChangedExactUnverifiedClaimCount / ChangedOrdinalCount;
    internal double ChangedNoClaimPercent =>
        ChangedOrdinalCount == 0 ? 0 : 100.0 * ChangedNoIndexedClaimCount / ChangedOrdinalCount;

    internal IReadOnlyList<int> ChangedWithoutExactClaimOrdinals { get; }
    internal IReadOnlyList<int> ChangedWithPotentialSourceTextDifferences { get; }
    internal int ChangedWithPotentialSourceTextDifferenceCount =>
        ChangedWithPotentialSourceTextDifferences.Count;

    internal bool HasVerifiedCemIdentity => false;
    internal bool HasVerifiedOptionMeanings => false;
    internal bool IsRestorableBackup => false;
    internal bool MayReadLiveConfiguration => false;
    internal bool MayWriteConfiguration => false;
    internal bool MayAcquirePin => false;

    private CemConfigurationResearchChangeCoverage(
        CemConfigurationIndexedFormat format,
        int total,
        int changed,
        int fullExact,
        int fullMissing,
        int changedExact,
        int changedSoftware,
        int changedHardware,
        int changedMissing,
        IReadOnlyList<int> changedWithoutExact,
        IReadOnlyList<int> changedWithTextDifferences)
    {
        Format = format;
        TotalRawOrdinals = total;
        ChangedOrdinalCount = changed;
        FullExactUnverifiedClaimCount = fullExact;
        FullNoIndexedClaimCount = fullMissing;
        ChangedExactUnverifiedClaimCount = changedExact;
        ChangedOtherSoftwareRevisionCount = changedSoftware;
        ChangedOtherHardwarePartCount = changedHardware;
        ChangedNoIndexedClaimCount = changedMissing;
        ChangedWithoutExactClaimOrdinals = Array.AsReadOnly(changedWithoutExact.ToArray());
        ChangedWithPotentialSourceTextDifferences =
            Array.AsReadOnly(changedWithTextDifferences.ToArray());
    }

    internal static CemConfigurationResearchChangeCoverage Combine(
        CemConfigurationRevisionResearchAssessment changed,
        CemConfigurationResearchCoverageAssessment wholeFormat)
    {
        ArgumentNullException.ThrowIfNull(changed);
        ArgumentNullException.ThrowIfNull(wholeFormat);
        if (changed.Format != wholeFormat.Format ||
            changed.TotalValueCount != wholeFormat.TotalOrdinals ||
            changed.Statistics.ChangedOrdinalCount != changed.ChangedCount ||
            changed.Statistics.InvalidClaimedScopeCount != 0)
            throw new InvalidDataException("CEM changed and full-coverage scope mismatch.");

        var fullExact = wholeFormat.ExactUnverifiedClaimCount;
        var fullMissing = wholeFormat.NoIndexedResearchClaimCount;
        var statusCounts = new int[4];
        var withoutExact = new List<int>();
        var changedText = new List<int>();
        var textFlags = wholeFormat.PotentialCrossSourceTextDifferenceOrdinals.ToHashSet();

        var lastOrdinal = 0;
        foreach (var row in changed.ChangedOrdinals)
        {
            var ordinal = row.Index1Based;
            if (ordinal <= lastOrdinal || ordinal > wholeFormat.TotalOrdinals)
                throw new InvalidDataException("Invalid changed CEM index sequence.");
            lastOrdinal = ordinal;

            var expected = wholeFormat.Ordinals[ordinal - 1];
            var kind = row.Disposition switch
            {
                CemConfigurationRevisionResearchDisposition.ExactUnverifiedUserClaim =>
                    CemConfigurationResearchCoverageKind.ExactUnverifiedClaim,
                CemConfigurationRevisionResearchDisposition.OnlyOtherSoftwareRevisionClaim =>
                    CemConfigurationResearchCoverageKind.OnlyOtherClaimedSoftwareRevision,
                CemConfigurationRevisionResearchDisposition.OnlyOtherHardwarePartClaim =>
                    CemConfigurationResearchCoverageKind.OnlyOtherClaimedHardwarePart,
                CemConfigurationRevisionResearchDisposition.NoClaimForChangedOrdinal =>
                    CemConfigurationResearchCoverageKind.NoIndexedResearchClaim,
                _ => throw new InvalidDataException("Invalid changed CEM research status.")
            };
            if (expected.Index1Based != ordinal || expected.Kind != kind)
                throw new InvalidDataException(
                    "Changed-index claim scope differs from full research coverage.");
            statusCounts[(int)kind]++;
            if (kind != CemConfigurationResearchCoverageKind.ExactUnverifiedClaim)
                withoutExact.Add(ordinal);
            if (textFlags.Contains(ordinal))
                changedText.Add(ordinal);
        }

        if (statusCounts.Sum() != changed.ChangedCount ||
            statusCounts[0] > fullExact || statusCounts[3] > fullMissing ||
            statusCounts[0] != changed.Statistics.ExactUnverifiedClaimCount ||
            statusCounts[1] != changed.Statistics.OtherSoftwareRevisionClaimCount ||
            statusCounts[2] != changed.Statistics.OtherHardwarePartClaimCount ||
            statusCounts[3] != changed.Statistics.MissingResearchClaimCount)
            throw new InvalidDataException("Inconsistent CEM change coverage totals.");

        return new(wholeFormat.Format, wholeFormat.TotalOrdinals,
            changed.ChangedCount, fullExact, fullMissing,
            statusCounts[0], statusCounts[1], statusCounts[2],
            statusCounts[3], withoutExact, changedText);
    }

    internal string CreateRedactedReport(bool english)
    {
        var format = Format == CemConfigurationIndexedFormat.P2B9Fc ? "P2 B9FC" : "P3 F106";
        string Number(int value) => value.ToString(CultureInfo.InvariantCulture);
        string Percent(double value) => value.ToString("F1", CultureInfo.InvariantCulture) + "%";
        string Ordinals(IReadOnlyList<int> items) => items.Count == 0
            ? (english ? "none" : "нет")
            : string.Join(", ", items.Select(Number));

        return string.Join(Environment.NewLine, new[]
        {
            english ? "CEM CHANGED-INDEX VS CATALOG COVERAGE (OFFLINE, UNVERIFIED)"
                : "CEM: ИЗМЕНЕНИЯ И ПОКРЫТИЕ КАТАЛОГА (ОФЛАЙН, НЕ ПОДТВЕРЖДЕНО)",
            (english ? "Format: " : "Формат: ") + format,
            (english ? "Total raw indices: " : "Всего сырых индексов: ") + Number(TotalRawOrdinals),
            (english ? "Changed raw indices: " : "Изменено сырых индексов: ") + Number(ChangedOrdinalCount),
            (english ? "Whole-format exact user-claim scopes (UNVERIFIED): "
                : "Всего точных совпадений заявлений (НЕ ПОДТВЕРЖДЕНО): ") +
                Number(FullExactUnverifiedClaimCount) + " (" + Percent(FullExactClaimPercent) + ")",
            (english ? "Changed indices with exact unverified claims: "
                : "Изменённые индексы с точными неподтверждёнными записями: ") +
                Number(ChangedExactUnverifiedClaimCount) + " (" + Percent(ChangedExactClaimPercent) + ")",
            (english ? "Changed indices with other claimed software revisions: "
                : "Изменённые индексы с другими заявленными версиями ПО: ") +
                Number(ChangedOtherSoftwareRevisionCount),
            (english ? "Changed indices with other claimed hardware parts: "
                : "Изменённые индексы с другими заявленными аппаратными номерами: ") +
                Number(ChangedOtherHardwarePartCount),
            (english ? "Whole-format indices without research claims: "
                : "Все индексы без исследовательских записей: ") + Number(FullNoIndexedClaimCount),
            (english ? "Changed indices without research claims: "
                : "Изменённые индексы без исследовательских записей: ") +
                Number(ChangedNoIndexedClaimCount) + " (" + Percent(ChangedNoClaimPercent) + ")",
            (english ? "Unchanged exact-claim indices: "
                : "Неизменённые индексы с точными записями: ") +
                Number(UnchangedExactUnverifiedClaimCount),
            (english ? "Unchanged indices without research claims: "
                : "Неизменённые индексы без записей: ") + Number(UnchangedNoIndexedClaimCount),
            (english ? "Changed indices lacking an exact user claim: "
                : "Изменённые индексы без точной заявленной записи: ") +
                Ordinals(ChangedWithoutExactClaimOrdinals),
            (english ? "Changed indices with possible source-text differences: "
                : "Изменённые индексы с возможными различиями текста источников: ") +
                Ordinals(ChangedWithPotentialSourceTextDifferences),
            english
                ? "Claim coverage is NOT OEM coverage; a source-text difference is NOT a proven conflict."
                : "Покрытие заявлений НЕ является OEM-покрытием; различия текста НЕ доказывают конфликт.",
            english
                ? "No source text, raw bytes, VIN, part/revision, local paths or hashes included."
                : "Нет текста источников, сырых байтов, VIN, номеров, ревизий, путей или хешей.",
            english ? "NOT a backup. Vehicle reads, PIN acquisition and writes remain disabled."
                : "НЕ резервная копия. Чтение автомобиля, PIN и запись остаются запрещены."
        });
    }
}
