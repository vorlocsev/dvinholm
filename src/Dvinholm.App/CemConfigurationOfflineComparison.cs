using System.Globalization;

namespace Dvinholm.App;

/// <summary>
/// Detached, redacted-by-default result of two separately selected local
/// P2 B9FC or P3 F106 bodies. Equal bytes do not authenticate a CEM.
/// </summary>
internal sealed class CemConfigurationOfflineComparisonResult
{
    internal CemConfigurationIndexedFormat Format { get; }
    internal int RawValueCount { get; }
    internal string FirstBodySha256 { get; }
    internal string SecondBodySha256 { get; }
    internal IReadOnlyList<int> ChangedOrdinals { get; }
    internal int ChangedCount => ChangedOrdinals.Count;
    internal bool RawValuesEqual => ChangedOrdinals.Count == 0;

    internal bool HasVerifiedCemIdentity => false;
    internal bool HasVerifiedOptionMeanings => false;
    internal bool IsRestorableBackup => false;
    internal bool MayReadLiveConfiguration => false;
    internal bool MayWriteConfiguration => false;
    internal bool MayAcquirePin => false;

    internal CemConfigurationOfflineComparisonResult(
        CemConfigurationIndexedFormat format,
        int rawValueCount,
        string firstBodySha256,
        string secondBodySha256,
        IReadOnlyList<int> changedOrdinals)
    {
        Format = format;
        RawValueCount = rawValueCount;
        FirstBodySha256 = firstBodySha256;
        SecondBodySha256 = secondBodySha256;
        ChangedOrdinals = Array.AsReadOnly(changedOrdinals.ToArray());
    }

    /// <summary>
    /// The report omits source paths, body SHA fingerprints, raw bytes,
    /// names, VINs, purported option meanings and research references.
    /// Ordinal indices identify only positions in the P2/P3 body.
    /// </summary>
    internal string CreateRedactedReport(bool english)
    {
        if (Format is not (CemConfigurationIndexedFormat.P2B9Fc or
                          CemConfigurationIndexedFormat.P3F106) ||
            RawValueCount != (Format == CemConfigurationIndexedFormat.P2B9Fc
                ? CemConfigurationBlockDecoder.P2ValueCount
                : CemConfigurationBlockDecoder.P3ValueCount) ||
            ChangedOrdinals.Count > RawValueCount ||
            ChangedOrdinals.Where((ordinal, index) =>
                ordinal < 1 || ordinal > RawValueCount ||
                (index > 0 && ChangedOrdinals[index - 1] >= ordinal)).Any())
            throw new InvalidDataException("Invalid offline CEM comparison result.");

        var generation = Format == CemConfigurationIndexedFormat.P2B9Fc
            ? "P2 B9FC" : "P3 F106";
        var changed = ChangedCount == 0
            ? (english ? "none" : "нет")
            : string.Join(", ", ChangedOrdinals.Select(
                value => value.ToString(CultureInfo.InvariantCulture)));

        return string.Join(Environment.NewLine, new[]
        {
            english
                ? "Dvinholm CEM RAW INDEX COMPARISON (OFFLINE)"
                : "Dvinholm: СРАВНЕНИЕ СЫРЫХ ИНДЕКСОВ CEM (ОФЛАЙН)",
            english ? "Format: " + generation : "Формат: " + generation,
            english ? "Raw value count: " +
                RawValueCount.ToString(CultureInfo.InvariantCulture)
                : "Количество сырых значений: " +
                RawValueCount.ToString(CultureInfo.InvariantCulture),
            english ? "Changed count: " +
                ChangedCount.ToString(CultureInfo.InvariantCulture)
                : "Изменённых значений: " +
                ChangedCount.ToString(CultureInfo.InvariantCulture),
            english ? "Changed one-based ordinals: " + changed
                : "Изменённые индексы (с 1): " + changed,
            english
                ? "User-selected local files only; raw bytes, paths and hashes omitted."
                : "Только локальные файлы; байты, пути и хеши не включены.",
            english
                ? "No authenticated CEM identity, OEM option mapping, VIN, PIN or live access."
                : "Нет подтверждённой идентификации CEM, значений опций, VIN, PIN или живого доступа.",
            english
                ? "NOT a restorable backup; read/write operations remain disabled."
                : "НЕ резервная копия для восстановления; чтение/запись автомобиля запрещены."
        });
    }
}

/// <summary>
/// Compares two user-selected, exactly sized .bin bodies in memory, reusing
/// the existing strict count/checksum decoder and index-only comparison.
/// Never touches CAN, J2534, firmware, PIN or a diagnostic session.
/// </summary>
internal static class CemConfigurationOfflineComparison
{
    internal static async Task<CemConfigurationOfflineComparisonResult> CompareFilesAsync(
        string firstSelectedPath,
        string secondSelectedPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(firstSelectedPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(secondSelectedPath);
        cancellationToken.ThrowIfCancellationRequested();

        var before = await CemConfigurationOfflineBlockInspection.InspectFileAsync(
            firstSelectedPath, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        var after = await CemConfigurationOfflineBlockInspection.InspectFileAsync(
            secondSelectedPath, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        // The core comparison rejects cross-generation data rather than
        // silently aligning 140 P2 values with 252 P3 values.
        var differences = CemConfigurationIndexedReview.Compare(before, after);
        var ordinals = differences.Select(item => item.Index1Based).ToArray();

        return new CemConfigurationOfflineComparisonResult(
            before.Format,
            before.Values.Count,
            before.BodySha256,
            after.BodySha256,
            ordinals);
    }
}
