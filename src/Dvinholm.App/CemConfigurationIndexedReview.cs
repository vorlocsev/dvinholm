using System.Globalization;
using System.Security.Cryptography;

namespace Dvinholm.App;

/// <summary>
/// Index-only offline view of independently decoded P2 B9FC or P3 F106 bodies.
/// Index1Based is an ordinal in a format body, not an authenticated Volvo
/// option identifier or a validated offset in CEM memory.
/// </summary>
internal enum CemConfigurationIndexedFormat { P2B9Fc, P3F106 }

internal readonly record struct CemConfigurationIndexedValue(int Index1Based, byte Value)
{
    internal string ValueHex => Value.ToString("X2", CultureInfo.InvariantCulture);
}

internal readonly record struct CemConfigurationIndexedDifference(
    int Index1Based, byte Before, byte After);

internal sealed class CemConfigurationIndexedSnapshot
{
    internal CemConfigurationIndexedFormat Format { get; }
    internal string BodySha256 { get; }
    internal IReadOnlyList<CemConfigurationIndexedValue> Values { get; }

    // Source bytes and metadata are untrusted; neither an ordinal nor
    // a matching hash authenticates a vehicle or provides a parameter mapping.
    internal bool HasVerifiedCemIdentity => false;
    internal bool HasVerifiedParameterMeanings => false;
    internal bool MayReadOrWriteVehicle => false;
    internal bool IsRestorableBackup => false;

    internal CemConfigurationIndexedSnapshot(
        CemConfigurationIndexedFormat format,
        string bodySha256,
        IReadOnlyList<CemConfigurationIndexedValue> values)
    {
        Format = format;
        BodySha256 = bodySha256;
        Values = Array.AsReadOnly(values.ToArray());
    }
}

/// <summary>
/// Offline-only indexed inspection and comparison; deliberately does not
/// join the 140/252 ordinals to Dvinholm's vendor/OEM research labels.
/// </summary>
internal static class CemConfigurationIndexedReview
{
    internal static CemConfigurationIndexedSnapshot InspectP2B9Fc(ReadOnlySpan<byte> body) =>
        Build(CemConfigurationIndexedFormat.P2B9Fc, body,
            CemConfigurationBlockDecoder.DecodeP2B9Fc(body));

    internal static CemConfigurationIndexedSnapshot InspectP3F106(ReadOnlySpan<byte> body) =>
        Build(CemConfigurationIndexedFormat.P3F106, body,
            CemConfigurationBlockDecoder.DecodeP3F106(body));

    private static CemConfigurationIndexedSnapshot Build(
        CemConfigurationIndexedFormat format, ReadOnlySpan<byte> body, byte[] data)
    {
        var values = new CemConfigurationIndexedValue[data.Length];
        for (var index = 0; index < data.Length; index++)
            values[index] = new CemConfigurationIndexedValue(index + 1, data[index]);
        var checksum = Convert.ToHexString(SHA256.HashData(body));
        return new CemConfigurationIndexedSnapshot(format, checksum, values);
    }

    internal static IReadOnlyList<CemConfigurationIndexedDifference> Compare(
        CemConfigurationIndexedSnapshot before, CemConfigurationIndexedSnapshot after)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);
        if (before.Format != after.Format ||
            !Enum.IsDefined(before.Format))
            throw new InvalidDataException("P2/P3 indexed snapshots cannot be interchanged.");

        var expectedCount = before.Format == CemConfigurationIndexedFormat.P2B9Fc
            ? CemConfigurationBlockDecoder.P2ValueCount
            : CemConfigurationBlockDecoder.P3ValueCount;

        if (!HasValidOrdinals(before.Values, expectedCount) ||
            !HasValidOrdinals(after.Values, expectedCount))
            throw new InvalidDataException("Invalid indexed configuration snapshot.");

        var differences = new List<CemConfigurationIndexedDifference>();
        for (var index = 0; index < expectedCount; index++)
        {
            var oldValue = before.Values[index].Value;
            var newValue = after.Values[index].Value;
            if (oldValue != newValue)
                differences.Add(new CemConfigurationIndexedDifference(index + 1, oldValue, newValue));
        }
        return Array.AsReadOnly(differences.ToArray());
    }

    private static bool HasValidOrdinals(
        IReadOnlyList<CemConfigurationIndexedValue>? values, int count)
    {
        if (values is null || values.Count != count)
            return false;
        for (var index = 0; index < count; index++)
            if (values[index].Index1Based != index + 1)
                return false;
        return true;
    }
}
