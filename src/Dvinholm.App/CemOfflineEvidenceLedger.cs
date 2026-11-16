using System.Globalization;

namespace Dvinholm.App;

/// <summary>
/// Explicit links between UNVERIFIED local-source claims. Link assertions
/// can expose textual disagreement, never physical-module identity.
/// </summary>
internal enum CemOfflineEvidenceLinkClaim
{
    SameClaimedModule,
    SameClaimedHardwareAndSoftware,
    DistinctClaimedSource
}

internal enum CemOfflineEvidenceLinkFinding
{
    UserClaimTextAgreesUnverified,
    UserClaimTextConflicts,
    SameUserSourceLabelNotIndependent,
    DistinctUserSourceLabelsUnattested
}

internal sealed record CemOfflineEvidenceLink(
    CemOfflineProvenanceRole FirstRole,
    CemOfflineProvenanceRole SecondRole,
    CemOfflineEvidenceLinkClaim Claim);

internal readonly record struct CemOfflineEvidenceLinkResult(
    CemOfflineProvenanceRole FirstRole,
    CemOfflineProvenanceRole SecondRole,
    CemOfflineEvidenceLinkClaim Claim,
    CemOfflineEvidenceLinkFinding Finding);

/// <summary>
/// A repeated LOCAL fingerprint inspection, not a time-attested or
/// independently authenticated capture. Caller-supplied UTC is claimed.
/// </summary>
internal sealed record CemOfflineEvidenceRecheck(
    CemOfflineProvenanceRole Role,
    DateTimeOffset ClaimedCheckedUtc,
    string LocallyObservedSha256);

internal readonly record struct CemOfflineEvidenceRecheckResult(
    CemOfflineProvenanceRole Role,
    int OrdinalForRole,
    bool MatchesOriginallyDeclaredSha256,
    bool ClaimedTimeAfterOriginalObservation,
    bool DigestDiffersFromPriorRecheck);

internal sealed class CemOfflineEvidenceLedgerAssessment
{
    internal IReadOnlyList<CemOfflineEvidenceLinkResult> LinkResults { get; }
    internal IReadOnlyList<CemOfflineEvidenceRecheckResult> RecheckJournal { get; }
    internal int InitiallyMatchingLocalDigests { get; }
    internal int LinkTextConflictCount => LinkResults.Count(x =>
        x.Finding == CemOfflineEvidenceLinkFinding.UserClaimTextConflicts);
    internal int SharedSourceLabelCount => LinkResults.Count(x =>
        x.Finding == CemOfflineEvidenceLinkFinding.SameUserSourceLabelNotIndependent);
    internal int RepeatLocalHashMismatchCount => RecheckJournal.Count(x =>
        !x.MatchesOriginallyDeclaredSha256);
    internal int RecheckDigestChangedCount => RecheckJournal.Count(x =>
        x.DigestDiffersFromPriorRecheck);
    internal int ClaimedRecheckChronologyIssueCount => RecheckJournal.Count(x =>
        !x.ClaimedTimeAfterOriginalObservation);
    internal IReadOnlyList<CemConfigurationEvidenceGap> OpenPhysicalEvidenceGates { get; }

    internal bool HasIndependentSourceAuthentication => false;
    internal bool HasVerifiedSamePhysicalCem => false;
    internal bool HasVerifiedFirmwareOrOptionMeaning => false;
    internal bool MayReadLiveConfiguration => false;
    internal bool MayAcquirePin => false;
    internal bool MayWriteConfiguration => false;
    internal bool IsRestorableBackup => false;

    internal CemOfflineEvidenceLedgerAssessment(
        IReadOnlyList<CemOfflineEvidenceLinkResult> links,
        IReadOnlyList<CemOfflineEvidenceRecheckResult> rechecks,
        CemOfflineProvenanceAssessment provenance)
    {
        ArgumentNullException.ThrowIfNull(links);
        ArgumentNullException.ThrowIfNull(rechecks);
        ArgumentNullException.ThrowIfNull(provenance);
        if (provenance.OpenPhysicalEvidenceGates.Count != 7 ||
            provenance.OpenPhysicalEvidenceGates.Distinct().Count() != 7 ||
            provenance.OpenPhysicalEvidenceGates.Any(x => !Enum.IsDefined(x)) ||
            provenance.Fingerprints.Count != 3 ||
            provenance.Fingerprints.Select(x => x.Role).Distinct().Count() != 3 ||
            provenance.Fingerprints.Any(x => !Enum.IsDefined(x.Role)))
            throw new InvalidDataException("Incomplete local CEM provenance evidence.");

        LinkResults = Array.AsReadOnly(links.ToArray());
        RecheckJournal = Array.AsReadOnly(rechecks.ToArray());
        InitiallyMatchingLocalDigests = provenance.LocallyMatchingFingerprintCount;
        OpenPhysicalEvidenceGates =
            Array.AsReadOnly(provenance.OpenPhysicalEvidenceGates.ToArray());
    }

    internal string CreateRedactedReport(bool english)
    {
        string N(int n) => n.ToString(CultureInfo.InvariantCulture);
        var report = new List<string>
        {
            english ? "CEM LOCAL EVIDENCE LINKS AND RECHECK JOURNAL (OFFLINE)"
                : "CEM: СВЯЗИ СВИДЕТЕЛЬСТВ И ЖУРНАЛ ПОВТОРНЫХ ПРОВЕРОК (ОФЛАЙН)",
            (english ? "Initial local fingerprint matches: " :
                "Первичные совпадения локальных хешей: ") + N(InitiallyMatchingLocalDigests) + "/3",
            (english ? "User-declared evidence links: " :
                "Заявленные связи свидетельств: ") + N(LinkResults.Count),
            (english ? "Conflicting claimed text links: " :
                "Связи с расхождениями заявленных данных: ") + N(LinkTextConflictCount),
            (english ? "Shared user source-label warnings: " :
                "Предупреждения об общей метке источника: ") + N(SharedSourceLabelCount),
            (english ? "Repeat local checks: " :
                "Повторных локальных проверок: ") + N(RecheckJournal.Count),
            (english ? "Repeat digest mismatches: " :
                "Несовпадения хешей при перепроверке: ") + N(RepeatLocalHashMismatchCount),
            (english ? "Changed digest between repeated checks: " :
                "Изменение хеша между повторными проверками: ") + N(RecheckDigestChangedCount),
            (english ? "Claimed timestamp-order concerns: " :
                "Вопросы к заявленному порядку времени: ") +
                N(ClaimedRecheckChronologyIssueCount)
        };
        foreach (var result in LinkResults)
            report.Add((english ? "Link " : "Связь ") +
                Role(result.FirstRole, english) + " / " +
                Role(result.SecondRole, english) + ": " +
                Finding(result.Finding, english));
        foreach (var result in RecheckJournal)
            report.Add((english ? "Recheck " : "Перепроверка ") +
                Role(result.Role, english) + " #" + N(result.OrdinalForRole) + ": " +
                (result.MatchesOriginallyDeclaredSha256
                    ? (english ? "declared local hash MATCH" :
                        "совпадает с первоначально заявленным локальным хешем")
                    : (english ? "declared local hash MISMATCH" :
                        "НЕ совпадает с первоначально заявленным локальным хешем")) +
                (result.DigestDiffersFromPriorRecheck
                    ? (english ? "; digest changed since prior recheck" :
                        "; хеш изменился относительно предыдущей перепроверки") : "") +
                (!result.ClaimedTimeAfterOriginalObservation
                    ? (english ? "; claimed time precedes observation" :
                        "; заявленное время раньше наблюдения") : ""));
        report.Add(english ? "Independent physical/OEM evidence gates: OPEN 7/7" :
            "Независимые физические/OEM-требования: ОТКРЫТО 7/7");
        report.Add(english
            ? "Distinct source labels and matching local SHA-256 are NOT independent source verification."
            : "Разные метки источников и совпадающие локальные SHA-256 НЕ подтверждают независимость источников.");
        report.Add(english
            ? "No original source labels, claimed revisions, UTC timestamps, hashes, paths, VIN or raw configuration bytes disclosed."
            : "Не выводятся метки источников, заявленные ревизии, время UTC, хеши, пути, VIN и сырые байты.");
        report.Add(english
            ? "NOT a restorable backup. Live CEM reads, PIN access and writes remain disabled."
            : "НЕ резервная копия. Чтение CEM, PIN и запись остаются запрещены.");
        return string.Join(Environment.NewLine, report);
    }

    private static string Role(CemOfflineProvenanceRole role, bool english) => role switch
    {
        CemOfflineProvenanceRole.PreviousBody => english ? "previous body" : "предыдущий блок",
        CemOfflineProvenanceRole.CurrentBody => english ? "current body" : "текущий блок",
        CemOfflineProvenanceRole.ResearchCatalog => english ? "research JSON" : "исследовательский JSON",
        _ => throw new InvalidDataException("Unknown evidence role.")
    };

    private static string Finding(CemOfflineEvidenceLinkFinding kind, bool english) => kind switch
    {
        CemOfflineEvidenceLinkFinding.UserClaimTextAgreesUnverified =>
            english ? "claimed text agrees, UNVERIFIED" :
                "заявленный текст совпадает, НЕ ПОДТВЕРЖДЕНО",
        CemOfflineEvidenceLinkFinding.UserClaimTextConflicts =>
            english ? "conflicting user-claimed text" : "расхождение заявленных данных",
        CemOfflineEvidenceLinkFinding.SameUserSourceLabelNotIndependent =>
            english ? "identical user source label, NOT independent" :
                "общая метка источника, НЕ независимо",
        CemOfflineEvidenceLinkFinding.DistinctUserSourceLabelsUnattested =>
            english ? "distinct source labels only, UNATTESTED" :
                "только разные метки источников, НЕ подтверждены",
        _ => throw new InvalidDataException("Unknown evidence finding.")
    };
}

/// <summary>
/// No persistent database or diagnostic request. Input claims and rechecks
/// can be USER-EDITED: only the bounded local-file fingerprint helper is
/// based on bytes that this process itself read.
/// </summary>
internal static class CemOfflineEvidenceLedger
{
    internal const int MaximumLinks = 12;
    internal const int MaximumRechecks = 64;

    internal static CemOfflineEvidenceLedgerAssessment Assess(
        IReadOnlyList<CemOfflineProvenanceDeclaration> declarations,
        CemOfflineProvenanceAssessment provenance,
        IReadOnlyList<CemOfflineEvidenceLink> links,
        IReadOnlyList<CemOfflineEvidenceRecheck> rechecks)
    {
        CemOfflineProvenanceAudit.ValidateDeclarations(declarations);
        ArgumentNullException.ThrowIfNull(provenance);
        ArgumentNullException.ThrowIfNull(links);
        ArgumentNullException.ThrowIfNull(rechecks);
        if (links.Count > MaximumLinks || rechecks.Count > MaximumRechecks)
            throw new InvalidDataException("CEM local evidence ledger exceeds its limits.");

        var byRole = declarations.ToDictionary(x => x.Role);
        var results = new List<CemOfflineEvidenceLinkResult>(links.Count);
        var uniqueLinks = new HashSet<(int First, int Second, CemOfflineEvidenceLinkClaim Claim)>();
        foreach (var link in links)
        {
            if (link is null ||
                !Enum.IsDefined(link.FirstRole) ||
                !Enum.IsDefined(link.SecondRole) ||
                !Enum.IsDefined(link.Claim) ||
                link.FirstRole == link.SecondRole)
                throw new InvalidDataException("Invalid CEM evidence link.");

            var ordered = (Math.Min((int)link.FirstRole, (int)link.SecondRole),
                Math.Max((int)link.FirstRole, (int)link.SecondRole), link.Claim);
            if (!uniqueLinks.Add(ordered))
                throw new InvalidDataException("Duplicate CEM evidence link assertion.");

            var a = byRole[link.FirstRole];
            var b = byRole[link.SecondRole];
            var finding = link.Claim switch
            {
                CemOfflineEvidenceLinkClaim.SameClaimedModule =>
                    string.Equals(a.ClaimedModuleAssociation, b.ClaimedModuleAssociation,
                        StringComparison.Ordinal)
                        ? CemOfflineEvidenceLinkFinding.UserClaimTextAgreesUnverified
                        : CemOfflineEvidenceLinkFinding.UserClaimTextConflicts,
                CemOfflineEvidenceLinkClaim.SameClaimedHardwareAndSoftware =>
                    string.Equals(a.ClaimedHardwarePartNumber, b.ClaimedHardwarePartNumber,
                        StringComparison.Ordinal) &&
                    string.Equals(a.ClaimedSoftwareRevision, b.ClaimedSoftwareRevision,
                        StringComparison.Ordinal)
                        ? CemOfflineEvidenceLinkFinding.UserClaimTextAgreesUnverified
                        : CemOfflineEvidenceLinkFinding.UserClaimTextConflicts,
                CemOfflineEvidenceLinkClaim.DistinctClaimedSource =>
                    string.Equals(a.SourceReference, b.SourceReference, StringComparison.Ordinal)
                        ? CemOfflineEvidenceLinkFinding.SameUserSourceLabelNotIndependent
                        : CemOfflineEvidenceLinkFinding.DistinctUserSourceLabelsUnattested,
                _ => throw new InvalidDataException("Unknown CEM link declaration.")
            };
            results.Add(new(link.FirstRole, link.SecondRole, link.Claim, finding));
        }

        var history = new List<CemOfflineEvidenceRecheckResult>(rechecks.Count);
        var latest = new Dictionary<CemOfflineProvenanceRole,
            (DateTimeOffset Time, string Digest, int Ordinal)>();
        foreach (var recheck in rechecks)
        {
            if (recheck is null || !Enum.IsDefined(recheck.Role) ||
                !ValidUtc(recheck.ClaimedCheckedUtc) ||
                !ValidDigest(recheck.LocallyObservedSha256))
                throw new InvalidDataException("Invalid CEM repeat-inspection record.");

            var hadPrior = latest.TryGetValue(recheck.Role, out var previous);
            if (hadPrior && recheck.ClaimedCheckedUtc <= previous.Time)
                throw new InvalidDataException("CEM repeated checks are not in claimed UTC order.");

            var declaration = byRole[recheck.Role];
            history.Add(new(recheck.Role, hadPrior ? previous.Ordinal + 1 : 1,
                string.Equals(recheck.LocallyObservedSha256,
                    declaration.ExpectedSha256, StringComparison.Ordinal),
                recheck.ClaimedCheckedUtc >= declaration.ClaimedObservedUtc,
                hadPrior && !string.Equals(recheck.LocallyObservedSha256,
                    previous.Digest, StringComparison.Ordinal)));
            latest[recheck.Role] = (recheck.ClaimedCheckedUtc,
                recheck.LocallyObservedSha256, hadPrior ? previous.Ordinal + 1 : 1);
        }

        return new(results, history, provenance);
    }

    /// <summary>
    /// Computes a fresh bounded LOCAL checksum and records the caller's
    /// claimed UTC time. Does not timestamp-attest or save the event.
    /// </summary>
    internal static async Task<CemOfflineEvidenceRecheck> CaptureLocalRecheckAsync(
        string selectedPath, CemOfflineProvenanceRole role,
        DateTimeOffset claimedCheckedUtc,
        CancellationToken cancellationToken = default)
    {
        if (!ValidUtc(claimedCheckedUtc))
            throw new InvalidDataException("Use a claimed UTC time for CEM recheck.");
        var digest = await CemOfflineProvenanceAudit.ReadLocalSha256Async(
            selectedPath, role, cancellationToken);
        return new(role, claimedCheckedUtc, digest);
    }

    private static bool ValidDigest(string? digest) =>
        digest is { Length: 64 } &&
        digest.All(c => c is >= '0' and <= '9' or >= 'A' and <= 'F');

    private static bool ValidUtc(DateTimeOffset timestamp) =>
        timestamp.Offset == TimeSpan.Zero &&
        timestamp.Year is >= 2000 and <= 2100;
}
