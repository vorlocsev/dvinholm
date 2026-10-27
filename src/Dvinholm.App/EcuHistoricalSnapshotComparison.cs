using System.Globalization;

namespace Dvinholm.App;

/// <summary>
/// Safe display-only summary for two independently inspected, historical
/// diagnostic DID snapshots. No raw values, ECU identity assertions or writes.
/// </summary>
internal sealed record EcuHistoricalSnapshotComparison(
    int ComparedCount,
    int ChangedCount,
    IReadOnlyList<ushort> ChangedDataIdentifiers,
    string Summary);

internal static class EcuHistoricalSnapshotComparer
{
    internal const string Warning =
        "Historical diagnostic files only. Matching profile/CAN addressing does NOT " +
        "prove the same physical ECU or firmware. SHA-256 does not authenticate a file. " +
        "No configuration can be restored or written from these snapshots.";

    internal static EcuHistoricalSnapshotComparison Compare(
        EcuConfigurationExportDocument before,
        EcuConfigurationExportDocument after)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);

        var original = ToSnapshot(before);
        var current = ToSnapshot(after);
        var difference = EcuConfigurationSnapshotDiff.Compare(original, current);
        var changed = Array.AsReadOnly(
            difference.Values.Where(value => value.HasChanged)
                .Select(value => value.DataIdentifier).ToArray());

        // Never display actual hexadecimal value bytes, profile names/IDs,
        // VIN-like data or filesystem locations. Only DID numbers and counts.
        var summary =
            "Offline DID snapshot comparison" + Environment.NewLine +
            Warning + Environment.NewLine + Environment.NewLine +
            "Earlier capture UTC: " +
            before.CapturedAtUtc.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) +
            Environment.NewLine +
            "Later capture UTC: " +
            after.CapturedAtUtc.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) +
            Environment.NewLine +
            "Compared DIDs: " + difference.Values.Count.ToString(CultureInfo.InvariantCulture) +
            Environment.NewLine +
            "Changed DIDs: " + changed.Count.ToString(CultureInfo.InvariantCulture) +
            Environment.NewLine +
            "Changed DID identifiers (values hidden): " +
            (changed.Count == 0
                ? "none"
                : string.Join(", ", changed.Select(did =>
                    did.ToString("X4", CultureInfo.InvariantCulture))));

        return new EcuHistoricalSnapshotComparison(
            difference.Values.Count, changed.Count, changed, summary);
    }

    private static EcuConfigurationReadSnapshot ToSnapshot(EcuConfigurationExportDocument document)
    {
        if (document.SchemaVersion != 1 || document.IsRestorableBackup ||
            !document.ContainsPotentiallySensitiveData ||
            document.CapturedAtUtc == default ||
            document.CapturedAtUtc.Offset != TimeSpan.Zero ||
            string.IsNullOrWhiteSpace(document.ProfileId) ||
            string.IsNullOrWhiteSpace(document.ProfileName) ||
            document.Values is null)
            throw new InvalidDataException("Invalid historical diagnostic snapshot metadata.");

        var width = document.IsExtended ? 8 : 3;
        var maximum = document.IsExtended ? 0x1FFFFFFFu : 0x7FFu;
        if (!TryReadCanId(document.RequestCanId, width, maximum, out var requestId) ||
            !TryReadCanId(document.ResponseCanId, width, maximum, out var responseId) ||
            requestId == responseId)
            throw new InvalidDataException("Invalid historical diagnostic CAN identifiers.");

        return new EcuConfigurationReadSnapshot(
            document.ProfileId, document.ProfileName,
            requestId, responseId, document.IsExtended,
            document.ProfileMarkedVerified, document.CapturedAtUtc, document.Values);
    }

    private static bool TryReadCanId(
        string? value, int width, uint maximum, out uint id)
    {
        id = 0;
        return value is not null && value.Length == width &&
               value.All(c => c is >= '0' and <= '9' or >= 'A' and <= 'F') &&
               uint.TryParse(value, NumberStyles.HexNumber,
                   CultureInfo.InvariantCulture, out id) &&
               id <= maximum;
    }
}
