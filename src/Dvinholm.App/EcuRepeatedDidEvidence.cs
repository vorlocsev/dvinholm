using System.Globalization;
using System.Text;

namespace Dvinholm.App;

/// <summary>
/// Offline hints for planning real, consented XC90 P2/CEM research. Two
/// historical readings do NOT constitute a module fingerprint or proof
/// that both files came from the same vehicle.
/// </summary>
internal sealed record EcuRepeatedDidObservation(
    ushort DataIdentifier,
    int EarlierBytes,
    int LaterBytes,
    bool ValueChanged)
{
    internal bool LengthChanged => EarlierBytes != LaterBytes;
}

internal sealed record EcuRepeatedDidEvidenceReport(
    IReadOnlyList<EcuRepeatedDidObservation> Observations,
    string Summary)
{
    internal int SameValueCount => Observations.Count(x => !x.ValueChanged);
    internal int ChangedValueCount => Observations.Count(x => x.ValueChanged);
    internal int LengthChangedCount => Observations.Count(x => x.LengthChanged);

    internal bool IsVerifiedCemIdentity => false;
    internal bool HasVerifiedFirmwareMapping => false;
    internal bool CanAcquireOrAuthorizePin => false;
    internal bool CanReadOrWriteConfiguration => false;
    internal bool CanSendDiagnosticRequest => false;
}

/// <summary>
/// Read-only metadata derived from *two already validated* historical DID
/// exports. Reuses strict document and DID-set checks; no new J2534/UDS
/// reads, snapshot writes, credentials, VIN decoding or PID guesses.
/// </summary>
internal static class EcuRepeatedDidEvidence
{
    internal static EcuRepeatedDidEvidenceReport Compare(
        EcuConfigurationExportDocument earlier,
        EcuConfigurationExportDocument later)
    {
        // Reuse strict schema, UTC, CAN/profile matching, DID bounds and
        // complete value comparison. Mismatched profiles fail closed.
        var compared = EcuHistoricalSnapshotComparer.Compare(earlier, later);

        if (later.CapturedAtUtc <= earlier.CapturedAtUtc)
            throw new InvalidDataException(
                "Repeated historical DID research requires strictly increasing UTC captures.");

        var earlierByDid = earlier.Values.ToDictionary(value => value.DataIdentifier);
        var laterByDid = later.Values.ToDictionary(value => value.DataIdentifier);
        var changed = compared.ChangedDataIdentifiers.ToHashSet();

        var rows = earlierByDid.Keys.Order().Select(did =>
            new EcuRepeatedDidObservation(
                did,
                earlierByDid[did].ValueHex.Length / 2,
                laterByDid[did].ValueHex.Length / 2,
                changed.Contains(did))).ToArray();

        var summary = new StringBuilder();
        summary.AppendLine("P2/CEM offline repeated-DID research observations");
        summary.AppendLine("These are TWO HISTORICAL OBSERVATIONS only. Identical profile/CAN");
        summary.AppendLine("IDs and matching DID values do NOT prove the same physical ECU,");
        summary.AppendLine("a stable firmware identity, Volvo P2/CEM compatibility, or a");
        summary.AppendLine("verified configuration parameter. SHA-256 is not provenance.");
        summary.AppendLine("No PIN, security access, vehicle write or restorable backup exists.");
        summary.AppendLine();
        summary.Append("DID observations: ").AppendLine(
            rows.Length.ToString(CultureInfo.InvariantCulture));
        summary.Append("Unchanged values in these captures: ").AppendLine(
            rows.Count(x => !x.ValueChanged).ToString(CultureInfo.InvariantCulture));
        summary.Append("Changed values in these captures: ").AppendLine(
            rows.Count(x => x.ValueChanged).ToString(CultureInfo.InvariantCulture));
        summary.Append("Changed byte lengths: ").AppendLine(
            rows.Count(x => x.LengthChanged).ToString(CultureInfo.InvariantCulture));
        foreach (var row in rows)
        {
            summary.Append("DID ").Append(row.DataIdentifier.ToString("X4", CultureInfo.InvariantCulture))
                .Append(": earlier ").Append(row.EarlierBytes)
                .Append(" byte(s), later ").Append(row.LaterBytes)
                .Append(" byte(s); ")
                .AppendLine(row.ValueChanged ? "value changed (hidden)" : "value unchanged (hidden)");
        }
        summary.AppendLine();
        summary.AppendLine("Next: independently verify ECU hardware/software, authorized");
        summary.AppendLine("transport and DID meanings on compatible physical test equipment.");
        summary.AppendLine("CAN/DID identifiers and lengths may fingerprint a vehicle. Do not");
        summary.AppendLine("share unreviewed observations or treat them as a PIN/ECU unlock.");

        return new EcuRepeatedDidEvidenceReport(Array.AsReadOnly(rows), summary.ToString());
    }
}
