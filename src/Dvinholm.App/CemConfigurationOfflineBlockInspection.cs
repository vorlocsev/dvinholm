namespace Dvinholm.App;

/// <summary>
/// Read at most one validated P2 (142-byte) or P3 (254-byte) configuration
/// body from a user-selected local .bin file. Transport/PDU headers must
/// already be removed. This is not a vehicle read or a restorable backup.
/// </summary>
internal static class CemConfigurationOfflineBlockInspection
{
    internal static async Task<CemConfigurationIndexedSnapshot> InspectFileAsync(
        string selectedPath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(selectedPath);
        cancellationToken.ThrowIfCancellationRequested();
        if (!string.Equals(Path.GetExtension(selectedPath), ".bin",
                StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Only a selected local .bin body is supported.");

        await using var stream = new FileStream(selectedPath,
            FileMode.Open, FileAccess.Read, FileShare.Read,
            4096, FileOptions.Asynchronous | FileOptions.SequentialScan);

        var length = stream.Length;
        if (length != CemConfigurationBlockDecoder.P2BodyLength &&
            length != CemConfigurationBlockDecoder.P3BodyLength)
            throw new InvalidDataException(
                "Select only the exact P2 142-byte or P3 254-byte body.");

        var body = new byte[checked((int)length)];
        await stream.ReadExactlyAsync(body.AsMemory(), cancellationToken);
        if (stream.Length != length ||
            await stream.ReadAsync(new byte[1], cancellationToken) != 0)
            throw new InvalidDataException("Local configuration body changed during reading.");

        cancellationToken.ThrowIfCancellationRequested();
        return length == CemConfigurationBlockDecoder.P2BodyLength
            ? CemConfigurationIndexedReview.InspectP2B9Fc(body)
            : CemConfigurationIndexedReview.InspectP3F106(body);
    }
}
