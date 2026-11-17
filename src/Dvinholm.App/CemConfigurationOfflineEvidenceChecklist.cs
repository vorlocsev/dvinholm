using System.Globalization;

namespace Dvinholm.App;

/// <summary>
/// Evidence still required BEFORE any independently authorized work with
/// a physical CEM. A valid offline P2/P3 body is structural data only.
/// </summary>
internal enum CemConfigurationEvidenceGap
{
    IndependentOriginalFileProvenance,
    VerifiedSamePhysicalModuleAssociation,
    AuthenticatedHardwareAndSoftwareRevisions,
    VerifiedOemRawIndexSemantics,
    DocumentedRevisionScopedDiagnosticTransport,
    AuthorizedPhysicalBenchAcceptance,
    VerifiedPermissionAndRecoveryProcedure
}

/// <summary>
/// Payload-free evidence worksheet. The optional observation/checklist are
/// explicitly UNCORRELATED CONTEXT: neither can link local files to a
/// physical CEM or promote research claims to verified OEM mappings.
/// </summary>
internal sealed class CemConfigurationOfflineEvidenceChecklistAssessment
{
    internal CemConfigurationIndexedFormat Format { get; }
    internal int TotalRawOrdinals { get; }
    internal int ChangedRawOrdinals { get; }
    internal int ChangedWithoutExactResearchClaim { get; }
    internal int ChangedWithPossibleTextDifferences { get; }
    internal bool HasUncorrelatedUnattestedPartNumberObservation { get; }
    internal bool HasUncorrelatedSatisfiedSessionPrerequisites { get; }
    internal IReadOnlyList<CemConfigurationEvidenceGap> OpenGates { get; }

    internal bool HasAuthenticatedPhysicalCem => false;
    internal bool HasVerifiedOptionMeanings => false;
    internal bool HasVerifiedSameModuleProvenance => false;
    internal bool MayReadLiveConfiguration => false;
    internal bool MayAcquirePin => false;
    internal bool MayWriteConfiguration => false;
    internal bool IsRestorableBackup => false;

    internal CemConfigurationOfflineEvidenceChecklistAssessment(
        CemConfigurationResearchChangeCoverage research,
        bool uncorrelatedPartNumberObservation,
        bool uncorrelatedSessionPrerequisites)
    {
        ArgumentNullException.ThrowIfNull(research);
        var expected = research.Format switch
        {
            CemConfigurationIndexedFormat.P2B9Fc => CemConfigurationBlockDecoder.P2ValueCount,
            CemConfigurationIndexedFormat.P3F106 => CemConfigurationBlockDecoder.P3ValueCount,
            _ => 0
        };
        if (expected == 0 || research.TotalRawOrdinals != expected ||
            research.ChangedOrdinalCount < 0 ||
            research.ChangedOrdinalCount > expected ||
            research.ChangedWithoutExactClaimCount < 0 ||
            research.ChangedWithoutExactClaimCount > research.ChangedOrdinalCount ||
            research.ChangedWithPotentialSourceTextDifferenceCount < 0 ||
            research.ChangedWithPotentialSourceTextDifferenceCount >
                research.ChangedOrdinalCount)
            throw new InvalidDataException("Invalid offline CEM evidence scope.");

        Format = research.Format;
        TotalRawOrdinals = expected;
        ChangedRawOrdinals = research.ChangedOrdinalCount;
        ChangedWithoutExactResearchClaim = research.ChangedWithoutExactClaimCount;
        ChangedWithPossibleTextDifferences =
            research.ChangedWithPotentialSourceTextDifferenceCount;
        HasUncorrelatedUnattestedPartNumberObservation = uncorrelatedPartNumberObservation;
        HasUncorrelatedSatisfiedSessionPrerequisites = uncorrelatedSessionPrerequisites;
        OpenGates = Array.AsReadOnly(
            Enum.GetValues<CemConfigurationEvidenceGap>());
    }

    internal string CreateRedactedReport(bool english)
    {
        if (OpenGates.Count != 7 || OpenGates.Distinct().Count() != 7 ||
            OpenGates.Any(g => !Enum.IsDefined(g)))
            throw new InvalidDataException("Incomplete CEM evidence checklist.");

        string N(int count) => count.ToString(CultureInfo.InvariantCulture);
        var format = Format == CemConfigurationIndexedFormat.P2B9Fc
            ? "P2 B9FC" : "P3 F106";
        var lines = new List<string>
        {
            english ? "CEM OFFLINE RESEARCH EVIDENCE CHECKPOINT"
                : "CEM: КОНТРОЛЬ СВИДЕТЕЛЬСТВ ОФЛАЙН-ИССЛЕДОВАНИЯ",
            (english ? "Format: " : "Формат: ") + format,
            (english ? "Structurally inspected raw positions: "
                : "Структурно проверено сырых индексов: ") + N(TotalRawOrdinals),
            (english ? "Changed raw positions: " : "Изменено сырых индексов: ") +
                N(ChangedRawOrdinals),
            (english ? "Changed positions without exact UNVERIFIED research claims: "
                : "Изменённые индексы без точных НЕПОДТВЕРЖДЁННЫХ записей: ") +
                N(ChangedWithoutExactResearchClaim),
            (english ? "Changed positions with possible source-text differences: "
                : "Изменённые индексы с возможными различиями текста источников: ") +
                N(ChangedWithPossibleTextDifferences),
            english
                ? "Optional part-number observation (UNCORRELATED, unattested): " +
                  (HasUncorrelatedUnattestedPartNumberObservation ? "present" : "absent")
                : "Наблюдение номера детали (НЕ СВЯЗАНО, не подтверждено): " +
                  (HasUncorrelatedUnattestedPartNumberObservation ? "есть" : "нет"),
            english
                ? "Optional session prerequisites (UNCORRELATED, snapshot only): " +
                  (HasUncorrelatedSatisfiedSessionPrerequisites ? "marked satisfied" : "not satisfied/provided")
                : "Условия сессии (НЕ СВЯЗАНЫ, лишь снимок): " +
                  (HasUncorrelatedSatisfiedSessionPrerequisites ? "отмечены выполненными" : "не выполнены/нет данных"),
            english ? "Independent physical/OEM evidence gates: OPEN 7/7"
                : "Независимые требования физической/OEM-проверки: ОТКРЫТО 7/7"
        };
        foreach (var gap in OpenGates)
            lines.Add("[OPEN] " + Label(gap, english));
        lines.Add(english
            ? "No proof that the two files, research JSON or observation belong to the same physical module."
            : "Не доказано, что оба файла, исследовательский JSON или наблюдение относятся к одному физическому блоку.");
        lines.Add(english
            ? "A structural checksum is not OEM authenticity; research claims are not verified option mappings."
            : "Структурная контрольная сумма не подтверждает подлинность OEM; заявления не являются проверенной картой опций.");
        lines.Add(english
            ? "No VIN, part/revision, PIN, byte payload, path, source text, hash or diagnostic address is disclosed."
            : "Не выводятся VIN, номера/ревизии, PIN, байты, пути, тексты источников, хеши и диагностические адреса.");
        lines.Add(english
            ? "NOT a backup. No diagnostic request was sent; live reads, PIN access and writes remain disabled."
            : "НЕ резервная копия. Диагностических запросов не было; чтение, PIN и запись запрещены.");
        return string.Join(Environment.NewLine, lines);
    }

    private static string Label(CemConfigurationEvidenceGap gap, bool english) => gap switch
    {
        CemConfigurationEvidenceGap.IndependentOriginalFileProvenance =>
            english ? "Independent provenance for both original local bodies and research sources"
                : "Независимое подтверждение происхождения обоих файлов и источников",
        CemConfigurationEvidenceGap.VerifiedSamePhysicalModuleAssociation =>
            english ? "Independently prove all evidence refers to the same physical CEM"
                : "Доказать связь всех свидетельств с одним физическим CEM",
        CemConfigurationEvidenceGap.AuthenticatedHardwareAndSoftwareRevisions =>
            english ? "Authenticate physical CEM hardware/application/diagnostic software"
                : "Подтвердить версии оборудования, прикладного и диагностического ПО CEM",
        CemConfigurationEvidenceGap.VerifiedOemRawIndexSemantics =>
            english ? "Verify OEM meaning and generation-specific encoding for each index"
                : "Проверить OEM-назначение и формат кодирования каждого индекса",
        CemConfigurationEvidenceGap.DocumentedRevisionScopedDiagnosticTransport =>
            english ? "Document revision-scoped read-only services, transport and responses"
                : "Документировать привязанные к ревизии сервисы чтения, транспорт и ответы",
        CemConfigurationEvidenceGap.AuthorizedPhysicalBenchAcceptance =>
            english ? "Complete authorized isolated bench/adapter/timing acceptance"
                : "Провести разрешённую стендовую проверку адаптера и таймингов",
        CemConfigurationEvidenceGap.VerifiedPermissionAndRecoveryProcedure =>
            english ? "Verify service entitlement, safety, rollback and recovery procedure"
                : "Подтвердить полномочия, безопасность, откат и восстановление",
        _ => throw new InvalidDataException("Unknown CEM evidence gate.")
    };
}

/// <summary>
/// Pure offline summary. Passing external, uncorrelated session status can
/// ONLY add an explanatory label; it never closes any physical evidence gate.
/// </summary>
internal static class CemConfigurationOfflineEvidenceChecklist
{
    internal static CemConfigurationOfflineEvidenceChecklistAssessment Assess(
        CemConfigurationResearchChangeCoverage research,
        CemIdentityEvidenceAssessment? uncorrelatedIdentity = null,
        CemReadOnlyPrerequisiteAssessment? uncorrelatedPrerequisites = null)
    {
        ArgumentNullException.ThrowIfNull(research);
        var observed = false;
        if (uncorrelatedIdentity is not null)
        {
            // Reuse existing fail-closed status validation, not a raw
            // part number or another imported source's free-text fields.
            _ = CemIdentityEvidenceReport.Format(uncorrelatedIdentity, english: true);
            observed = uncorrelatedIdentity.Kind ==
                CemIdentityEvidenceKind.UnattestedPartNumberObservation;
        }

        var marked = false;
        if (uncorrelatedPrerequisites is not null)
        {
            _ = CemReadOnlyPrerequisiteReport.Format(
                uncorrelatedPrerequisites, english: true);
            marked = uncorrelatedPrerequisites.EligibleForSourceDefinedPartReadOnly;
        }

        return new(research, observed, marked);
    }
}
