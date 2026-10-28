namespace Dvinholm.App;

/// <summary>
/// One ECU DID value captured by the existing UDS ReadDataByIdentifier (0x22)
/// service. This value is opaque: DID semantics must be verified separately
/// for each vehicle and software version.
/// </summary>
internal sealed record EcuDidReadback(ushort DataIdentifier, string ValueHex);

/// <summary>
/// A volatile, read-only record of one completed ECU read session. This is
/// NOT a restorable configuration backup and never authorizes ECU writes.
/// </summary>
internal sealed record EcuConfigurationReadSnapshot(
    string ProfileId,
    string ProfileName,
    uint RequestId,
    uint ResponseId,
    bool IsExtended,
    bool ProfileMarkedVerified,
    DateTimeOffset CapturedAtUtc,
    IReadOnlyList<EcuDidReadback> Values);

/// <summary>
/// Read-only baseline capture for future ECU configuration comparison.
/// Sends no raw CAN commands or write/security UDS services. The production
/// overload delegates exclusively to UdsClient.ReadDataByIdentifier (0x22).
/// The injected overload allows deterministic virtual/offline testing.
/// </summary>
internal static class EcuConfigurationReadback
{
    internal const int MaximumDataIdentifiers = 128;
    // ISO-TP 4095-byte frame payload includes the positive UDS 0x62 SID
    // and two echoed DID bytes, leaving 4092 bytes for the value.
    internal const int MaximumDidValueBytes = IsoTp.MaxPayloadLength - 3;

    internal static EcuConfigurationReadSnapshot Capture(
        EcuProfile profile,
        UdsClient client,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(client);
        return Capture(profile, (did, ct) => client.ReadDataByIdentifier(did, ct), cancellationToken);
    }

    internal static EcuConfigurationReadSnapshot Capture(
        EcuProfile profile,
        Func<ushort, CancellationToken, byte[]> readDid,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(readDid);

        cancellationToken.ThrowIfCancellationRequested();
        var dids = profile.ReadableDataIdentifiers.Order().ToArray();
        if (dids.Length is 0 or > MaximumDataIdentifiers)
            throw new InvalidDataException("A configuration readback requires 1–128 profile DIDs.");

        // Never return a partial record if a DID fails, is malformed, or the
        // operation is cancelled. An incomplete read is not a baseline.
        var values = new List<EcuDidReadback>(dids.Length);
        foreach (var did in dids)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var response = readDid(did, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            // UdsClient normally verifies the DID echo itself, but validate
            // again at this boundary to reject malformed injected responses.
            if (response is null || response.Length <= 2 ||
                response[0] != (byte)(did >> 8) || response[1] != (byte)did)
                throw new InvalidDataException($"ECU configuration DID 0x{did:X4} has no valid echoed DID and value.");

            // An injected reader can return arbitrarily long buffers even
            // though production Classic CAN ISO-TP enforces its wire ceiling.
            // Reject before hex conversion; never return a partial baseline.
            if (response.Length - 2 > MaximumDidValueBytes)
                throw new InvalidDataException(
                    $"ECU configuration DID 0x{did:X4} exceeds the Classic CAN ISO-TP value limit.");

            // Encode bytes without guessing undocumented ECU encodings.
            // Do not persist, log or export configuration data here.
            values.Add(new EcuDidReadback(did, Convert.ToHexString(response.AsSpan(2))));
        }

        cancellationToken.ThrowIfCancellationRequested();
        return new EcuConfigurationReadSnapshot(
            profile.Id,
            profile.Name,
            profile.RequestId,
            profile.ResponseId,
            profile.IsExtended,
            profile.IsVerified,
            DateTimeOffset.UtcNow,
            Array.AsReadOnly(values.ToArray()));
    }
}
