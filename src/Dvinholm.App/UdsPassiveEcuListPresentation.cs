using System.Globalization;
using System.Text;

namespace Dvinholm.App;

/// <summary>
/// Presentation of passive UDS response observations. A response frame is
/// not proof of a particular ECU module or an authorized request address.
/// All results remain read-only and no guessed request ID is sent from here.
/// </summary>
internal static class UdsPassiveEcuListPresentation
{
    internal static string SelectorLabel(UdsEcuCandidate candidate, bool complete)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        var observed = Hex(candidate.ResponseId, candidate.IsExtended);
        var prefix = complete ? "Observed" : "Partial observation";
        var request = candidate.RequestId is uint id
            ? $"request 0x{Hex(id, candidate.IsExtended)} (INFERRED)"
            : "request UNKNOWN";
        return $"{prefix}: response 0x{observed} ({(candidate.IsExtended ? "29-bit" : "11-bit")}); {request}";
    }

    internal static string ScanSummary(int count, bool complete)
    {
        if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
        if (!complete)
            return $"INCOMPLETE: {count} response ID candidate(s) observed. Selection disabled; repeat scan.";
        return count == 0
            ? "No UDS response IDs observed. No ECU identity established."
            : $"{count} response ID candidate(s) observed. Choose one; request IDs are not verified.";
    }

    internal static string SelectionDetails(UdsEcuCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        if (candidate.ResponseCount < 0 || candidate.MaxDataLength is < 0)
            throw new ArgumentOutOfRangeException(nameof(candidate));

        var response = Hex(candidate.ResponseId, candidate.IsExtended);
        var request = candidate.RequestId is uint id
            ? "0x" + Hex(id, candidate.IsExtended) + " (INFERRED ONLY)"
            : "UNKNOWN";
        var result = new StringBuilder()
            .AppendLine("PASSIVE ECU RESPONSE OBSERVATION (NOT ECU IDENTIFICATION)")
            .Append("Observed response ID: 0x").AppendLine(response)
            .Append("CAN format: ").AppendLine(candidate.IsExtended ? "29-bit" : "11-bit")
            .Append("Suggested request ID: ").AppendLine(request)
            .Append("Observed response frames: ")
            .AppendLine(candidate.ResponseCount.ToString(CultureInfo.InvariantCulture))
            .Append("Maximum observed CAN data length: ")
            .Append(candidate.MaxDataLength?.ToString(CultureInfo.InvariantCulture) ?? "unknown")
            .AppendLine(" bytes")
            .AppendLine("No active ECU request was sent during this passive scan.")
            .AppendLine("The responder's actual module identity and request address are UNVERIFIED.")
            .AppendLine("Request ID remains BLANK. Independently verify and enter it manually")
            .AppendLine("before any active read-only UDS operation; selecting a row does not transmit.")
            .AppendLine("This is not a CEM/ECM identity, PIN, configuration or backup.");
        return result.ToString();
    }

    internal static string Hex(uint id, bool extended) =>
        id.ToString(extended ? "X8" : "X3", CultureInfo.InvariantCulture);
}
