using System.Globalization;

namespace Dvinholm.App;

/// <summary>
/// Describes ONLY which unverified user-authored claim scopes exist for
/// a raw changed ordinal. This never identifies a Volvo option or ECU.
/// </summary>
internal enum CemConfigurationRevisionResearchDisposition
{
    ExactUnverifiedUserClaim,
    OnlyOtherSoftwareRevisionClaim,
    OnlyOtherHardwarePartClaim,
    NoClaimForChangedOrdinal,
    AssertedScopeMissingOrInvalid
}

internal readonly record struct CemConfigurationRevisionResearchRow(
    int Index1Based,
    CemConfigurationRevisionResearchDisposition Disposition)
{
    internal bool HasVerifiedCemMeaning => false;
    internal bool MayWriteConfiguration => false;
}

/// <summary>
/// A bounded, immutable, redacted-by-default summary of user-asserted
/// metadata against changed raw ordinals in two already validated files.
/// </summary>
internal sealed class CemConfigurationRevisionResearchAssessment
{
    internal CemConfigurationIndexedFormat Format { get; }
    internal int TotalValueCount { get; }
    internal IReadOnlyList<CemConfigurationRevisionResearchRow> ChangedOrdinals { get; }
    internal int ChangedCount => ChangedOrdinals.Count;
    internal CemConfigurationRevisionResearchStatistics Statistics { get; }

    internal bool HasAuthenticatedPhysicalCem => false;
    internal bool HasVerifiedSoftwareRevision => false;
    internal bool HasVerifiedParameterMeaning => false;
    internal bool IsRestorableBackup => false;
    internal bool MayReadLiveConfiguration => false;
    internal bool MayWriteConfiguration => false;
    internal bool MayAcquirePin => false;

    internal CemConfigurationRevisionResearchAssessment(
        CemConfigurationIndexedFormat format,
        int totalValueCount,
        IReadOnlyList<CemConfigurationRevisionResearchRow> changedOrdinals)
    {
        Format = format;
        TotalValueCount = totalValueCount;
        ArgumentNullException.ThrowIfNull(changedOrdinals);
        ChangedOrdinals = Array.AsReadOnly(changedOrdinals.ToArray());
        Statistics = CemConfigurationRevisionResearchStatistics.Count(ChangedOrdinals);
        if (Statistics.ChangedOrdinalCount != ChangedCount)
            throw new InvalidDataException("Incomplete CEM ordinal research statistics.");
    }

    internal string CreateRedactedReport(bool english)
    {
        var prefix = english
            ? "CEM REVISION CLAIM TRIAGE (LOCAL, UNVERIFIED)"
            : "CEM: АНАЛИЗ ЗАЯВЛЕННЫХ РЕВИЗИЙ (ОФЛАЙН, НЕ ПОДТВЕРЖДЕНО)";
        var format = Format == CemConfigurationIndexedFormat.P2B9Fc
            ? "P2 B9FC" : "P3 F106";
        var rows = ChangedOrdinals.Select(row =>
            (english ? "Ordinal " : "Индекс ") +
            row.Index1Based.ToString(CultureInfo.InvariantCulture) + ": " +
            Label(row.Disposition, english));
        return string.Join(Environment.NewLine,
            new[]
            {
                prefix,
                (english ? "Format: " : "Формат: ") + format,
                (english ? "Changed raw ordinals: " : "Изменено сырых индексов: ") +
                ChangedCount.ToString(CultureInfo.InvariantCulture),
                english ? "Exact user-claim scopes (UNVERIFIED): " +
                    Statistics.ExactUnverifiedClaimCount.ToString(CultureInfo.InvariantCulture)
                    : "Точные совпадения заявлений (НЕ ПОДТВЕРЖДЕНО): " +
                    Statistics.ExactUnverifiedClaimCount.ToString(CultureInfo.InvariantCulture),
                english ? "Other claimed software revisions: " +
                    Statistics.OtherSoftwareRevisionClaimCount.ToString(CultureInfo.InvariantCulture)
                    : "Другие заявленные версии ПО: " +
                    Statistics.OtherSoftwareRevisionClaimCount.ToString(CultureInfo.InvariantCulture),
                english ? "Other claimed hardware parts: " +
                    Statistics.OtherHardwarePartClaimCount.ToString(CultureInfo.InvariantCulture)
                    : "Другие заявленные аппаратные номера: " +
                    Statistics.OtherHardwarePartClaimCount.ToString(CultureInfo.InvariantCulture),
                english ? "Incompatible claimed scopes (total): " +
                    Statistics.IncompatibleClaimScopeCount.ToString(CultureInfo.InvariantCulture)
                    : "Несовместимые заявленные ревизии (всего): " +
                    Statistics.IncompatibleClaimScopeCount.ToString(CultureInfo.InvariantCulture),
                english ? "Changed ordinals without research claims: " +
                    Statistics.MissingResearchClaimCount.ToString(CultureInfo.InvariantCulture)
                    : "Индексы без исследовательских записей: " +
                    Statistics.MissingResearchClaimCount.ToString(CultureInfo.InvariantCulture),
                english ? "Missing/invalid claimed part or revision: " +
                    Statistics.InvalidClaimedScopeCount.ToString(CultureInfo.InvariantCulture)
                    : "Отсутствующие/некорректные данные о ревизии: " +
                    Statistics.InvalidClaimedScopeCount.ToString(CultureInfo.InvariantCulture)
            }.Concat(rows).Append(english
                ? "Only user-supplied research claim scopes are compared. No hardware or firmware authentication."
                : "Сопоставлены только заявления пользователя. Аппаратный блок и прошивка не проверены.")
            .Append(english
                ? "Part numbers, revisions, meanings, references, paths, hashes and byte values omitted."
                : "Номера, ревизии, описания, источники, пути, хеши и байты не выводятся.")
            .Append(english
                ? "NOT a restorable backup. Live CEM read, PIN and configuration write are disabled."
                : "НЕ резервная копия. Чтение CEM, PIN и запись конфигурации запрещены."));
    }

    private static string Label(
        CemConfigurationRevisionResearchDisposition disposition,
        bool english) =>
        disposition switch
        {
            CemConfigurationRevisionResearchDisposition.ExactUnverifiedUserClaim =>
                english ? "exact user claim scope (UNVERIFIED)"
                    : "точное совпадение заявления (НЕ ПОДТВЕРЖДЕНО)",
            CemConfigurationRevisionResearchDisposition.OnlyOtherSoftwareRevisionClaim =>
                english ? "claim only for a different claimed software revision"
                    : "запись лишь для другой заявленной версии ПО",
            CemConfigurationRevisionResearchDisposition.OnlyOtherHardwarePartClaim =>
                english ? "claim only for a different claimed hardware part"
                    : "запись лишь для другого заявленного номера блока",
            CemConfigurationRevisionResearchDisposition.NoClaimForChangedOrdinal =>
                english ? "no indexed research claim"
                    : "нет исследовательской записи по индексу",
            CemConfigurationRevisionResearchDisposition.AssertedScopeMissingOrInvalid =>
                english ? "claimed part/software scope missing or invalid"
                    : "заявленный номер блока или версия ПО отсутствуют/некорректны",
            _ => throw new InvalidDataException("Unknown research disposition.")
        };
}

/// <summary>
/// Joins only changed one-based indices with validated, research-only JSON
/// scopes. No source is a trusted OEM mapping; exact-string matching cannot
/// authenticate a vehicle. No raw values or private source text are retained.
/// </summary>
internal static class CemConfigurationRevisionResearchTriage
{
    internal static CemConfigurationRevisionResearchAssessment Assess(
        CemConfigurationOfflineComparisonResult comparison,
        CemConfigurationResearchCatalogDocument catalog,
        string? claimedHardwarePartNumber,
        string? claimedSoftwareRevision)
    {
        ArgumentNullException.ThrowIfNull(comparison);
        ArgumentNullException.ThrowIfNull(catalog);
        ValidateComparison(comparison);
        ValidateCatalog(catalog);

        var scopeValid = CemConfigurationMappingResearch.ValidPartNumber(
            claimedHardwarePartNumber) &&
            CemConfigurationMappingResearch.ValidSoftwareRevision(
                claimedSoftwareRevision);
        var rows = new CemConfigurationRevisionResearchRow[
            comparison.ChangedOrdinals.Count];

        for (var i = 0; i < rows.Length; i++)
        {
            var ordinal = comparison.ChangedOrdinals[i];
            var sameOrdinal = catalog.Claims.Where(claim =>
                claim.Format == comparison.Format &&
                claim.Index1Based == ordinal).ToArray();

            CemConfigurationRevisionResearchDisposition status;
            if (!scopeValid)
                status = CemConfigurationRevisionResearchDisposition.AssertedScopeMissingOrInvalid;
            else if (sameOrdinal.Any(claim =>
                string.Equals(claim.HardwarePartNumber,
                    claimedHardwarePartNumber, StringComparison.Ordinal) &&
                string.Equals(claim.SoftwareRevision,
                    claimedSoftwareRevision, StringComparison.Ordinal)))
                status = CemConfigurationRevisionResearchDisposition.ExactUnverifiedUserClaim;
            else if (sameOrdinal.Any(claim =>
                string.Equals(claim.HardwarePartNumber,
                    claimedHardwarePartNumber, StringComparison.Ordinal)))
                status = CemConfigurationRevisionResearchDisposition.OnlyOtherSoftwareRevisionClaim;
            else if (sameOrdinal.Length != 0)
                status = CemConfigurationRevisionResearchDisposition.OnlyOtherHardwarePartClaim;
            else
                status = CemConfigurationRevisionResearchDisposition.NoClaimForChangedOrdinal;

            rows[i] = new(ordinal, status);
        }

        return new(comparison.Format, comparison.RawValueCount, rows);
    }

    private static void ValidateCatalog(CemConfigurationResearchCatalogDocument catalog)
    {
        if (catalog.Format != CemConfigurationResearchCatalogImport.Format ||
            catalog.SchemaVersion != 1 || !catalog.IsUnverifiedResearch ||
            catalog.IsRestorableBackup || catalog.MayAccessEcu ||
            catalog.Claims is null ||
            catalog.Claims.Count is < 1 or > CemConfigurationMappingResearch.MaximumRecords ||
            CemConfigurationMappingResearch.Validate(catalog.Claims).Count != 0)
            throw new InvalidDataException("Invalid or unsafe CEM mapping research catalog.");
    }

    private static void ValidateComparison(CemConfigurationOfflineComparisonResult comparison)
    {
        var expected = comparison.Format switch
        {
            CemConfigurationIndexedFormat.P2B9Fc => CemConfigurationBlockDecoder.P2ValueCount,
            CemConfigurationIndexedFormat.P3F106 => CemConfigurationBlockDecoder.P3ValueCount,
            _ => 0
        };

        if (expected == 0 || comparison.RawValueCount != expected ||
            !IsUppercaseDigest(comparison.FirstBodySha256) ||
            !IsUppercaseDigest(comparison.SecondBodySha256) ||
            comparison.ChangedOrdinals is null ||
            comparison.ChangedOrdinals.Count > expected ||
            comparison.ChangedOrdinals.Where((ordinal, index) =>
                ordinal < 1 || ordinal > expected ||
                (index > 0 && comparison.ChangedOrdinals[index - 1] >= ordinal)).Any() ||
            // An empty diff with unequal raw-body hashes, or a nonempty diff
            // with equal hashes, is inconsistent with an actual byte comparison.
            (comparison.ChangedCount == 0) !=
                string.Equals(comparison.FirstBodySha256,
                    comparison.SecondBodySha256, StringComparison.Ordinal))
            throw new InvalidDataException("Invalid CEM offline comparison structure.");
    }

    private static bool IsUppercaseDigest(string? digest) =>
        digest is { Length: 64 } &&
        digest.All(c => c is >= '0' and <= '9' or >= 'A' and <= 'F');
}
