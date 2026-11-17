using System.Globalization;

namespace Dvinholm.App;

/// <summary>
/// Counts coverage by raw ordinal for an asserted part/revision and the
/// user-supplied research JSON only. No vehicle/OEM identity is established.
/// </summary>
internal enum CemConfigurationResearchCoverageKind
{
    ExactUnverifiedClaim,
    OnlyOtherClaimedSoftwareRevision,
    OnlyOtherClaimedHardwarePart,
    NoIndexedResearchClaim
}

internal readonly record struct CemConfigurationResearchCoverageRow(
    int Index1Based,
    CemConfigurationResearchCoverageKind Kind)
{
    internal bool HasVerifiedOptionMeaning => false;
    internal bool MayWriteConfiguration => false;
}

internal sealed class CemConfigurationResearchCoverageAssessment
{
    internal CemConfigurationIndexedFormat Format { get; }
    internal IReadOnlyList<CemConfigurationResearchCoverageRow> Ordinals { get; }
    internal IReadOnlyList<int> PotentialCrossSourceTextDifferenceOrdinals { get; }
    internal int TotalOrdinals => Ordinals.Count;
    internal int ExactUnverifiedClaimCount { get; }
    internal int OtherSoftwareRevisionClaimCount { get; }
    internal int OtherHardwarePartClaimCount { get; }
    internal int NoIndexedResearchClaimCount { get; }
    internal int PotentialCrossSourceTextDifferenceCount =>
        PotentialCrossSourceTextDifferenceOrdinals.Count;
    internal double ExactUserClaimScopePercent =>
        100.0 * ExactUnverifiedClaimCount / TotalOrdinals;

    internal bool HasAuthenticatedPhysicalCem => false;
    internal bool HasVerifiedSoftwareRevision => false;
    internal bool HasVerifiedOptionMeanings => false;
    internal bool IsRestorableBackup => false;
    internal bool MayReadLiveConfiguration => false;
    internal bool MayWriteConfiguration => false;
    internal bool MayAcquirePin => false;

    internal CemConfigurationResearchCoverageAssessment(
        CemConfigurationIndexedFormat format,
        IReadOnlyList<CemConfigurationResearchCoverageRow> ordinals,
        IReadOnlyList<int> possibleTextDifferences)
    {
        ArgumentNullException.ThrowIfNull(ordinals);
        ArgumentNullException.ThrowIfNull(possibleTextDifferences);
        var expected = format switch
        {
            CemConfigurationIndexedFormat.P2B9Fc => CemConfigurationBlockDecoder.P2ValueCount,
            CemConfigurationIndexedFormat.P3F106 => CemConfigurationBlockDecoder.P3ValueCount,
            _ => 0
        };
        if (expected == 0 || ordinals.Count != expected)
            throw new InvalidDataException("Invalid P2/P3 coverage domain.");

        var rows = ordinals.ToArray();
        var counts = new int[4];
        for (var i = 0; i < rows.Length; i++)
        {
            if (rows[i].Index1Based != i + 1 || !Enum.IsDefined(rows[i].Kind))
                throw new InvalidDataException("Invalid coverage status or ordinal.");
            counts[(int)rows[i].Kind] = checked(counts[(int)rows[i].Kind] + 1);
        }

        var differences = possibleTextDifferences.ToArray();
        for (var i = 0; i < differences.Length; i++)
            if (differences[i] < 1 || differences[i] > expected ||
                (i > 0 && differences[i - 1] >= differences[i]))
                throw new InvalidDataException("Invalid text-difference ordinal sequence.");

        Format = format;
        Ordinals = Array.AsReadOnly(rows);
        PotentialCrossSourceTextDifferenceOrdinals = Array.AsReadOnly(differences);
        ExactUnverifiedClaimCount = counts[(int)CemConfigurationResearchCoverageKind.ExactUnverifiedClaim];
        OtherSoftwareRevisionClaimCount =
            counts[(int)CemConfigurationResearchCoverageKind.OnlyOtherClaimedSoftwareRevision];
        OtherHardwarePartClaimCount =
            counts[(int)CemConfigurationResearchCoverageKind.OnlyOtherClaimedHardwarePart];
        NoIndexedResearchClaimCount =
            counts[(int)CemConfigurationResearchCoverageKind.NoIndexedResearchClaim];
    }

    internal string CreateRedactedReport(bool english)
    {
        var generation = Format == CemConfigurationIndexedFormat.P2B9Fc
            ? "P2 B9FC" : "P3 F106";
        var coverage = ExactUserClaimScopePercent.ToString("F1",
            CultureInfo.InvariantCulture);
        var flags = PotentialCrossSourceTextDifferenceCount == 0
            ? (english ? "none" : "нет")
            : string.Join(", ", PotentialCrossSourceTextDifferenceOrdinals.Select(
                item => item.ToString(CultureInfo.InvariantCulture)));

        return string.Join(Environment.NewLine, new[]
        {
            english ? "CEM RESEARCH CATALOG ORDINAL COVERAGE (OFFLINE, UNVERIFIED)"
                : "CEM: ПОКРЫТИЕ ИНДЕКСОВ ИССЛЕДОВАТЕЛЬСКИМ КАТАЛОГОМ (ОФЛАЙН, НЕ ПОДТВЕРЖДЕНО)",
            (english ? "Format: " : "Формат: ") + generation,
            (english ? "Total raw ordinals: " : "Всего сырых индексов: ") +
                TotalOrdinals.ToString(CultureInfo.InvariantCulture),
            (english ? "Exact user-claimed scopes (UNVERIFIED): "
                : "Точные совпадения заявлений (НЕ ПОДТВЕРЖДЕНО): ") +
                ExactUnverifiedClaimCount.ToString(CultureInfo.InvariantCulture),
            (english ? "Exact user-claim scope coverage: "
                : "Покрытие заявленными совпадениями: ") + coverage + "%",
            (english ? "Only other claimed software revisions: "
                : "Только другие заявленные версии ПО: ") +
                OtherSoftwareRevisionClaimCount.ToString(CultureInfo.InvariantCulture),
            (english ? "Only other claimed hardware parts: "
                : "Только другие заявленные аппаратные номера: ") +
                OtherHardwarePartClaimCount.ToString(CultureInfo.InvariantCulture),
            (english ? "No indexed research claim: "
                : "Нет исследовательских записей по индексу: ") +
                NoIndexedResearchClaimCount.ToString(CultureInfo.InvariantCulture),
            (english ? "Possible text differences across sources/revisions: "
                : "Возможные текстовые расхождения источников/ревизий: ") +
                PotentialCrossSourceTextDifferenceCount.ToString(CultureInfo.InvariantCulture),
            (english ? "Potential text-difference one-based ordinals: "
                : "Индексы с возможными расхождениями (с 1): ") + flags,
            english
                ? "Differences in text across claimed revisions may be legitimate changes, NOT proven contradictions."
                : "Различия текста между заявленными ревизиями могут быть нормальными изменениями, это НЕ доказанные противоречия.",
            english
                ? "Scope duplicates for the SAME claimed part/software revision are rejected, not counted."
                : "Дубликаты одной заявленной пары номер/версия отклоняются, а не учитываются.",
            english
                ? "No OEM verification; source text, claimed part/revision, paths, VIN, hashes and raw byte values omitted."
                : "Нет проверки OEM; описания, номера, ревизии, пути, VIN, хеши и сырые байты не выводятся.",
            english
                ? "NOT a restorable backup; CEM read, PIN and write remain disabled."
                : "НЕ резервная копия; чтение CEM, PIN и запись остаются запрещены."
        });
    }
}

/// <summary>
/// Research coverage uses the *whole format* (140 P2 or 252 P3 indices),
/// not only indices changed in a pair of files. It never reads binary
/// data or authorizes OEM option meanings.
/// </summary>
internal static class CemConfigurationResearchCoverage
{
    internal static CemConfigurationResearchCoverageAssessment Assess(
        CemConfigurationResearchCatalogDocument catalog,
        CemConfigurationIndexedFormat format,
        string? claimedHardwarePartNumber,
        string? claimedSoftwareRevision)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        var count = format switch
        {
            CemConfigurationIndexedFormat.P2B9Fc => CemConfigurationBlockDecoder.P2ValueCount,
            CemConfigurationIndexedFormat.P3F106 => CemConfigurationBlockDecoder.P3ValueCount,
            _ => 0
        };
        if (count == 0)
            throw new InvalidDataException("Unsupported CEM research coverage generation.");
        if (!CemConfigurationMappingResearch.ValidPartNumber(claimedHardwarePartNumber) ||
            !CemConfigurationMappingResearch.ValidSoftwareRevision(claimedSoftwareRevision))
            throw new InvalidDataException("Research coverage requires valid asserted part/revision.");

        // A handmade document is untrusted just like imported JSON.
        // The entire catalog must pass the existing strict gate before
        // any format-specific coverage can be returned.
        if (catalog.Format != CemConfigurationResearchCatalogImport.Format ||
            catalog.SchemaVersion != 1 || !catalog.IsUnverifiedResearch ||
            catalog.IsRestorableBackup || catalog.MayAccessEcu ||
            catalog.Claims is null ||
            catalog.Claims.Count is < 1 or > CemConfigurationMappingResearch.MaximumRecords)
            throw new InvalidDataException("Unsafe CEM research catalog.");

        var claims = catalog.Claims.ToArray();
        if (CemConfigurationMappingResearch.Validate(claims).Count != 0)
            throw new InvalidDataException("Invalid CEM research catalog claims.");

        var relevant = claims.Where(claim => claim.Format == format).ToArray();
        var rows = new CemConfigurationResearchCoverageRow[count];
        for (var index = 1; index <= count; index++)
        {
            var atIndex = relevant.Where(claim =>
                claim.Index1Based == index).ToArray();
            var kind = atIndex.Any(claim =>
                    string.Equals(claim.HardwarePartNumber,
                        claimedHardwarePartNumber, StringComparison.Ordinal) &&
                    string.Equals(claim.SoftwareRevision,
                        claimedSoftwareRevision, StringComparison.Ordinal))
                ? CemConfigurationResearchCoverageKind.ExactUnverifiedClaim
                : atIndex.Any(claim =>
                    string.Equals(claim.HardwarePartNumber,
                        claimedHardwarePartNumber, StringComparison.Ordinal))
                    ? CemConfigurationResearchCoverageKind.OnlyOtherClaimedSoftwareRevision
                    : atIndex.Length != 0
                        ? CemConfigurationResearchCoverageKind.OnlyOtherClaimedHardwarePart
                        : CemConfigurationResearchCoverageKind.NoIndexedResearchClaim;
            rows[index - 1] = new(index, kind);
        }

        // Only differences between at least TWO separate source references
        // AND distinct written claims for the SAME asserted hardware part
        // and ordinal are flagged. The exact research scope includes the
        // revision, and duplicate exact scopes were already rejected.
        // Different claimed revisions may legitimately describe differences.
        var textDifferences = relevant
            .Where(claim => string.Equals(claim.HardwarePartNumber,
                claimedHardwarePartNumber, StringComparison.Ordinal))
            .GroupBy(claim => claim.Index1Based)
            .Where(group =>
                group.Select(x => x.EvidenceReference).Distinct(StringComparer.Ordinal).Count() > 1 &&
                group.Select(x => x.ClaimedMeaning).Distinct(StringComparer.Ordinal).Count() > 1)
            .Select(group => group.Key)
            .OrderBy(index => index)
            .ToArray();

        return new(format, rows, textDifferences);
    }
}
