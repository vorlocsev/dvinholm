namespace Dvinholm.App;

/// <summary>
/// A deterministic, in-memory comparison of two complete read-only DID
/// snapshots. Equality of profile/CAN addressing is not proof that the
/// underlying physical ECU or its firmware is unchanged.
/// </summary>
internal sealed record EcuDidValueDifference(
    ushort DataIdentifier,
    string OriginalValueHex,
    string CurrentValueHex,
    bool HasChanged);

internal sealed record EcuConfigurationSnapshotComparison(
    string ProfileId,
    DateTimeOffset OriginalCapturedAtUtc,
    DateTimeOffset CurrentCapturedAtUtc,
    IReadOnlyList<EcuDidValueDifference> Values)
{
    internal int ChangedCount => Values.Count(value => value.HasChanged);
}

/// <summary>
/// Diff for diagnostics and future preview UX only. Does not send CAN/UDS
/// requests, persist private bytes, or authorize configuration writes.
/// It deliberately requires exactly the same DID set on both sides: missing
/// bytes cannot silently be treated as a legitimate configuration change.
/// </summary>
internal static class EcuConfigurationSnapshotDiff
{
    internal static EcuConfigurationSnapshotComparison Compare(
        EcuConfigurationReadSnapshot original,
        EcuConfigurationReadSnapshot current)
    {
        ArgumentNullException.ThrowIfNull(original);
        ArgumentNullException.ThrowIfNull(current);

        // This backend is also callable without the strict historical JSON
        // inspector. Verify BOTH sides before comparing any sensitive DID
        // bytes or returning a result that could be mistaken for a baseline.
        ValidateCaptureMetadata(original);
        ValidateCaptureMetadata(current);

        if (!string.Equals(original.ProfileId, current.ProfileId, StringComparison.OrdinalIgnoreCase) ||
            original.RequestId != current.RequestId ||
            original.ResponseId != current.ResponseId ||
            original.IsExtended != current.IsExtended)
            throw new InvalidDataException(
                "Configuration snapshots use different profile identities or CAN addressing.");

        var before = Normalize(original.Values);
        var after = Normalize(current.Values);

        if (!before.Keys.SequenceEqual(after.Keys))
            throw new InvalidDataException(
                "Configuration snapshots must contain exactly the same DID set.");

        var comparisons = before.Select(pair =>
        {
            var latest = after[pair.Key];
            return new EcuDidValueDifference(
                pair.Key,
                pair.Value,
                latest,
                !string.Equals(pair.Value, latest, StringComparison.Ordinal));
        }).ToArray();

        return new EcuConfigurationSnapshotComparison(
            original.ProfileId,
            original.CapturedAtUtc,
            current.CapturedAtUtc,
            Array.AsReadOnly(comparisons));
    }

    private static void ValidateCaptureMetadata(EcuConfigurationReadSnapshot snapshot)
    {
        var maximumCanId = snapshot.IsExtended ? 0x1FFFFFFFu : 0x7FFu;
        if (snapshot.CapturedAtUtc == default ||
            snapshot.CapturedAtUtc.Offset != TimeSpan.Zero ||
            snapshot.RequestId > maximumCanId ||
            snapshot.ResponseId > maximumCanId ||
            snapshot.RequestId == snapshot.ResponseId ||
            string.IsNullOrWhiteSpace(snapshot.ProfileId) ||
            snapshot.ProfileId.Length > 120 ||
            string.IsNullOrWhiteSpace(snapshot.ProfileName) ||
            snapshot.ProfileName.Length > 160)
            throw new InvalidDataException(
                "Configuration snapshot has invalid capture provenance or CAN metadata.");
    }

    private static SortedDictionary<ushort, string> Normalize(
        IReadOnlyList<EcuDidReadback>? values)
    {
        if (values is null || values.Count is < 1 or > EcuConfigurationReadback.MaximumDataIdentifiers)
            throw new InvalidDataException("Configuration snapshot DID count is invalid.");

        var normalized = new SortedDictionary<ushort, string>();
        foreach (var item in values)
        {
            if (item is null || !IsValidHex(item.ValueHex) ||
                !normalized.TryAdd(item.DataIdentifier, item.ValueHex.ToUpperInvariant()))
                throw new InvalidDataException(
                    "Configuration snapshot contains a duplicate DID or invalid hexadecimal data.");
        }

        return normalized;
    }

    private static bool IsValidHex(string? value)
    {
        // The bytes originate from an ISO-TP DID read. Bound external inputs
        // as well, so a later imported snapshot cannot exhaust the UI.
        if (string.IsNullOrEmpty(value) || value.Length > 16_384 ||
            (value.Length & 1) != 0)
            return false;

        return value.All(c => c is >= '0' and <= '9' or >= 'A' and <= 'F' or >= 'a' and <= 'f');
    }
}
