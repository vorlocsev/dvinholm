using System.Globalization;

namespace Dvinholm.App;

/// <summary>
/// A per-role conclusion about USER-CLAIMED observation times and local
/// file fingerprints, not an authenticated clock, capture or physical CEM.
/// </summary>
internal enum CemOfflineEvidenceFreshnessKind
{
    InitialLocalDigestMismatch,
    NoRepeatInspection,
    ClaimedRepeatBeforeOriginalObservation,
    ClaimedRepeatAfterReviewTime,
    LatestLocalDigestMismatch,
    LatestClaimedRepeatOlderThanPolicy,
    LocallyConsistentClaimUnverified
}

internal readonly record struct CemOfflineEvidenceFreshnessRow(
    CemOfflineProvenanceRole Role,
    CemOfflineEvidenceFreshnessKind Kind,
    int RepeatCount,
    bool AnyRepeatDigestDrift)
{
    internal bool HasAuthenticatedCaptureTime => false;
    internal bool HasVerifiedPhysicalCem => false;
}

internal sealed class CemOfflineEvidenceFreshnessAssessment
{
    internal CemOfflineEvidenceLedgerAssessment Ledger { get; }
    internal IReadOnlyList<CemOfflineEvidenceFreshnessRow> Sources { get; }
    internal int LocallyConsistentUnverifiedCount =>
        Sources.Count(x => x.Kind ==
            CemOfflineEvidenceFreshnessKind.LocallyConsistentClaimUnverified);
    internal int NeedsNewLocalReviewCount =>
        Sources.Count(x => x.Kind !=
            CemOfflineEvidenceFreshnessKind.LocallyConsistentClaimUnverified);
    internal int SourcesWithPriorDigestDrift =>
        Sources.Count(x => x.AnyRepeatDigestDrift);
    internal bool HasUserClaimLinkConflicts =>
        Ledger.LinkTextConflictCount > 0 || Ledger.SharedSourceLabelCount > 0;
    internal bool HasChronologyConcerns =>
        Ledger.ClaimedRecheckChronologyIssueCount > 0 ||
        Sources.Any(x => x.Kind is
            CemOfflineEvidenceFreshnessKind.ClaimedRepeatBeforeOriginalObservation or
            CemOfflineEvidenceFreshnessKind.ClaimedRepeatAfterReviewTime);
    internal bool HasAuthenticatedPhysicalCem => false;
    internal bool HasVerifiedSourceIndependence => false;
    internal bool HasVerifiedOptionMeaning => false;
    internal bool MayReadLiveConfiguration => false;
    internal bool MayAcquirePin => false;
    internal bool MayWriteConfiguration => false;
    internal bool IsRestorableBackup => false;

    internal CemOfflineEvidenceFreshnessAssessment(
        CemOfflineEvidenceLedgerAssessment ledger,
        IReadOnlyList<CemOfflineEvidenceFreshnessRow> sources)
    {
        ArgumentNullException.ThrowIfNull(ledger);
        ArgumentNullException.ThrowIfNull(sources);
        if (sources.Count != 3 ||
            sources.Select(x => x.Role).Distinct().Count() != 3 ||
            sources.Any(x => !Enum.IsDefined(x.Role) ||
                !Enum.IsDefined(x.Kind) || x.RepeatCount < 0 ||
                x.RepeatCount > CemOfflineEvidenceLedger.MaximumRechecks) ||
            ledger.OpenPhysicalEvidenceGates.Count != 7)
            throw new InvalidDataException("Invalid CEM freshness assessment.");
        Ledger = ledger;
        Sources = Array.AsReadOnly(sources.ToArray());
    }

    internal string CreateRedactedReport(bool english)
    {
        var lines = new List<string>
        {
            english ? "CEM LOCAL RECHECK FRESHNESS (CLAIMED UTC, OFFLINE)"
                : "CEM: АКТУАЛЬНОСТЬ ЛОКАЛЬНЫХ ПЕРЕПРОВЕРОК (ЗАЯВЛЕННОЕ UTC, ОФЛАЙН)",
            (english ? "Locally consistent user claims (NOT VERIFIED): " :
                "Локально согласованные заявления (НЕ ПОДТВЕРЖДЕНО): ") +
                LocallyConsistentUnverifiedCount.ToString(CultureInfo.InvariantCulture) + "/3",
            (english ? "Sources requiring another review: " :
                "Источники, требующие повторной проверки: ") +
                NeedsNewLocalReviewCount.ToString(CultureInfo.InvariantCulture),
            (english ? "Sources with previously changed digests: " :
                "Источники с историей изменения хеша: ") +
                SourcesWithPriorDigestDrift.ToString(CultureInfo.InvariantCulture),
            (english ? "Claimed link conflicts or reused source labels: " :
                "Расхождения связей или общие метки источников: ") +
                (HasUserClaimLinkConflicts ? (english ? "yes" : "да") :
                    (english ? "no" : "нет")),
            (english ? "Claimed chronology concerns: " :
                "Вопросы к заявленной хронологии: ") +
                (HasChronologyConcerns ? (english ? "yes" : "да") :
                    (english ? "no" : "нет"))
        };
        foreach (var row in Sources.OrderBy(x => x.Role))
            lines.Add(Role(row.Role, english) + ": " +
                Label(row.Kind, english) +
                (english ? "; repeat checks: " : "; перепроверок: ") +
                row.RepeatCount.ToString(CultureInfo.InvariantCulture) +
                (row.AnyRepeatDigestDrift
                    ? (english ? "; previous hash drift detected" :
                        "; ранее обнаружено изменение хеша") : ""));
        lines.Add(english ? "Independent physical/OEM evidence gates: OPEN 7/7"
            : "Независимые физические/OEM-требования: ОТКРЫТО 7/7");
        lines.Add(english
            ? "Staleness is relative to a CALLER-CLAIMED UTC review time and policy window. Neither is time-attested."
            : "Давность рассчитана относительно ЗАЯВЛЕННОГО UTC проверки и заданного срока. Ни одно время не удостоверено.");
        lines.Add(english
            ? "No original timestamps, source identifiers, VIN, part/revision, paths, hashes or raw data included."
            : "Не включены исходные даты, метки источников, VIN, номера/ревизии, пути, хеши или сырые данные.");
        lines.Add(english
            ? "Local hash agreement is not independent proof. NOT a backup; vehicle reads, PIN and writes are disabled."
            : "Совпадение локальных хешей не является независимым доказательством. НЕ резервная копия; чтение, PIN и запись запрещены.");
        return string.Join(Environment.NewLine, lines) +
            Environment.NewLine + Ledger.CreateRedactedReport(english);
    }

    private static string Role(CemOfflineProvenanceRole role, bool english) => role switch
    {
        CemOfflineProvenanceRole.PreviousBody => english ? "Previous .bin" : "Предыдущий .bin",
        CemOfflineProvenanceRole.CurrentBody => english ? "Current .bin" : "Текущий .bin",
        CemOfflineProvenanceRole.ResearchCatalog => english ? "Research JSON" : "Исследовательский JSON",
        _ => throw new InvalidDataException("Unknown CEM source role.")
    };

    private static string Label(CemOfflineEvidenceFreshnessKind kind, bool english) => kind switch
    {
        CemOfflineEvidenceFreshnessKind.InitialLocalDigestMismatch =>
            english ? "initial declared/local digest mismatch" :
                "первоначальный заявленный и локальный хеши различаются",
        CemOfflineEvidenceFreshnessKind.NoRepeatInspection =>
            english ? "no repeat local inspection" : "нет повторной локальной проверки",
        CemOfflineEvidenceFreshnessKind.ClaimedRepeatBeforeOriginalObservation =>
            english ? "claimed recheck precedes claimed original observation" :
                "перепроверка заявлена раньше первоначального наблюдения",
        CemOfflineEvidenceFreshnessKind.ClaimedRepeatAfterReviewTime =>
            english ? "claimed recheck is later than claimed audit date" :
                "перепроверка заявлена позднее даты аудита",
        CemOfflineEvidenceFreshnessKind.LatestLocalDigestMismatch =>
            english ? "latest recheck digest differs from declared original" :
                "последний хеш не совпадает с первоначально заявленным",
        CemOfflineEvidenceFreshnessKind.LatestClaimedRepeatOlderThanPolicy =>
            english ? "latest claimed recheck is older than selected policy window" :
                "последняя заявленная перепроверка старше заданного срока",
        CemOfflineEvidenceFreshnessKind.LocallyConsistentClaimUnverified =>
            english ? "locally consistent user claim only (UNVERIFIED)" :
                "только локальное совпадение заявления (НЕ ПОДТВЕРЖДЕНО)",
        _ => throw new InvalidDataException("Unknown freshness state.")
    };
}

/// <summary>
/// Pure deterministic assessment. Do not use DateTime.Now to imply an
/// authenticated clock; review UTC is supplied explicitly by the caller.
/// </summary>
internal static class CemOfflineEvidenceFreshness
{
    internal static void ValidateReviewClockAndWindow(
        DateTimeOffset claimedReviewUtc, TimeSpan maximumClaimedAge)
    {
        if (claimedReviewUtc.Offset != TimeSpan.Zero ||
            claimedReviewUtc.Year is < 2000 or > 2100 ||
            maximumClaimedAge < TimeSpan.FromHours(1) ||
            maximumClaimedAge > TimeSpan.FromDays(30))
            throw new InvalidDataException(
                "Invalid claimed UTC review time or CEM recheck policy window.");
    }

    internal static CemOfflineEvidenceFreshnessAssessment Assess(
        IReadOnlyList<CemOfflineProvenanceDeclaration> declarations,
        CemOfflineProvenanceAssessment provenance,
        IReadOnlyList<CemOfflineEvidenceLink> links,
        IReadOnlyList<CemOfflineEvidenceRecheck> rechecks,
        DateTimeOffset claimedReviewUtc,
        TimeSpan maximumClaimedAge)
    {
        ValidateReviewClockAndWindow(claimedReviewUtc, maximumClaimedAge);
        var ledger = CemOfflineEvidenceLedger.Assess(declarations,
            provenance, links, rechecks);
        var byRole = declarations.ToDictionary(x => x.Role);
        var initial = provenance.Fingerprints.ToDictionary(x => x.Role);
        var rows = new List<CemOfflineEvidenceFreshnessRow>(3);

        foreach (var role in Enum.GetValues<CemOfflineProvenanceRole>())
        {
            var history = rechecks.Where(x => x.Role == role).ToArray();
            var latest = history.LastOrDefault();
            var declared = byRole[role];
            var changed = history.Skip(1).Where((row, index) =>
                !string.Equals(row.LocallyObservedSha256,
                    history[index].LocallyObservedSha256,
                    StringComparison.Ordinal)).Any();

            CemOfflineEvidenceFreshnessKind kind;
            if (!initial[role].MatchesLocalSha256)
                kind = CemOfflineEvidenceFreshnessKind.InitialLocalDigestMismatch;
            else if (latest is null)
                kind = CemOfflineEvidenceFreshnessKind.NoRepeatInspection;
            else if (latest.ClaimedCheckedUtc < declared.ClaimedObservedUtc)
                kind = CemOfflineEvidenceFreshnessKind.ClaimedRepeatBeforeOriginalObservation;
            else if (latest.ClaimedCheckedUtc > claimedReviewUtc)
                kind = CemOfflineEvidenceFreshnessKind.ClaimedRepeatAfterReviewTime;
            else if (!string.Equals(latest.LocallyObservedSha256,
                declared.ExpectedSha256, StringComparison.Ordinal))
                kind = CemOfflineEvidenceFreshnessKind.LatestLocalDigestMismatch;
            else if (claimedReviewUtc - latest.ClaimedCheckedUtc > maximumClaimedAge)
                kind = CemOfflineEvidenceFreshnessKind.LatestClaimedRepeatOlderThanPolicy;
            else
                kind = CemOfflineEvidenceFreshnessKind.LocallyConsistentClaimUnverified;

            rows.Add(new(role, kind, history.Length, changed));
        }

        return new(ledger, rows);
    }
}
