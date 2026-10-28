using System.Globalization;
using System.Text;

namespace Dvinholm.App;

/// <summary>
/// Compares two user-typed historical source notations ONLY. The "reference"
/// is explicitly an unauthenticated manual note, NOT a current ECU value.
/// No file, ECU session, profile, CAN transport, PIN or write plan is used.
/// </summary>
internal enum VolvoP2CemManualValueComparisonKind
{
    ReferenceMissing,
    ReferenceInvalidOrUnconfirmed,
    ProposalInvalidOrUnconfirmed,
    SameManualNotation,
    DifferentManualNotation
}

internal sealed record VolvoP2CemManualValueComparisonAssessment(
    VolvoP2CemManualValueComparisonKind Kind)
{
    internal bool HasAuthenticatedReferenceFromEcu => false;
    internal bool HasVerifiedFirmwareValueMapping => false;
    internal bool MayConnectOrReadCem => false;
    internal bool MayPrepareOrWriteConfiguration => false;
    internal bool MayRequestPinOrSecurityAccess => false;
}

internal static class VolvoP2CemManualValueComparison
{
    internal static VolvoP2CemManualValueComparisonAssessment Assess(
        string? claimedYear, bool yearIndependentlyCheckedByUser,
        VolvoP2CemCustomerParameterKind kind,
        string? manualReference, string? manualReferenceHigh,
        string? proposal, string? proposalHigh)
    {
        int? year = claimedYear is { Length: 4 } &&
                    int.TryParse(claimedYear, NumberStyles.None,
                        CultureInfo.InvariantCulture, out var parsed) &&
                    string.Equals(claimedYear, parsed.ToString(CultureInfo.InvariantCulture),
                        StringComparison.Ordinal)
            ? parsed : null;

        if (string.IsNullOrEmpty(manualReference) &&
            string.IsNullOrEmpty(manualReferenceHigh))
            return new(VolvoP2CemManualValueComparisonKind.ReferenceMissing);

        var reference = VolvoP2CemHistoricalCustomerValuePreflight.Assess(
            year, yearIndependentlyCheckedByUser,
            kind, manualReference, manualReferenceHigh);
        if (reference.Kind != VolvoP2CemCustomerValueReviewKind.ListedInHistoricalSourceOnly)
            return new(VolvoP2CemManualValueComparisonKind.ReferenceInvalidOrUnconfirmed);

        var desired = VolvoP2CemHistoricalCustomerValuePreflight.Assess(
            year, yearIndependentlyCheckedByUser,
            kind, proposal, proposalHigh);
        if (desired.Kind != VolvoP2CemCustomerValueReviewKind.ListedInHistoricalSourceOnly)
            return new(VolvoP2CemManualValueComparisonKind.ProposalInvalidOrUnconfirmed);

        var same = string.Equals(manualReference, proposal, StringComparison.Ordinal) &&
                   string.Equals(manualReferenceHigh, proposalHigh, StringComparison.Ordinal);
        return new(same
            ? VolvoP2CemManualValueComparisonKind.SameManualNotation
            : VolvoP2CemManualValueComparisonKind.DifferentManualNotation);
    }

    internal static string BuildReadOnlyReport(
        string? claimedYear, bool yearIndependentlyCheckedByUser,
        VolvoP2CemCustomerParameterKind kind,
        string? manualReference, string? manualReferenceHigh,
        string? proposal, string? proposalHigh)
    {
        var assessment = Assess(claimedYear, yearIndependentlyCheckedByUser,
            kind, manualReference, manualReferenceHigh, proposal, proposalHigh);
        var report = new StringBuilder()
            .AppendLine("XC90 P2/CEM MANUAL NOTE VS PROPOSAL (OFFLINE)")
            .AppendLine("BOTH inputs are user-typed UNVERIFIED research, NOT an ECU read.")
            .AppendLine("The year checkbox is self-reported, NOT independent software authentication.")
            .Append("Research comparison: ").AppendLine(assessment.Kind.ToString());

        // Never print invalid/untrusted input. Only values independently
        // accepted by the strictly bounded historical candidate catalog
        // are included in an entirely in-memory read-only report.
        if (assessment.Kind is VolvoP2CemManualValueComparisonKind.SameManualNotation
            or VolvoP2CemManualValueComparisonKind.DifferentManualNotation)
        {
            report.Append("Manual reference notation (NOT current ECU): ")
                .Append(manualReference);
            if (kind == VolvoP2CemCustomerParameterKind.SeatHeaterLowHighCelsius)
                report.Append(" LOW; ").Append(manualReferenceHigh).Append(" HIGH");
            report.AppendLine()
                .Append("Proposed research notation: ").Append(proposal);
            if (kind == VolvoP2CemCustomerParameterKind.SeatHeaterLowHighCelsius)
                report.Append(" LOW; ").Append(proposalHigh).Append(" HIGH");
            report.AppendLine()
                .AppendLine(assessment.Kind == VolvoP2CemManualValueComparisonKind.SameManualNotation
                    ? "Text comparison: SAME (two manual claims, not an ECU baseline)."
                    : "Text comparison: DIFFERENT (not evidence a vehicle setting changed).");
        }
        else
        {
            report.AppendLine("Reference/proposal values withheld: missing, unconfirmed or")
                .AppendLine("outside exact historical source notation; no raw user input echoed.");
        }

        report.AppendLine("Actual CURRENT CEM value: UNKNOWN; no diagnostic request was made.")
            .AppendLine("No authenticated CEM revision, verified byte mapping or legal service PIN.")
            .AppendLine("No vehicle-config backup, write plan, J2534/UDS, PIN or ECU change.")
            .AppendLine("Close to discard both inputs. Nothing saved to disk or ECU.")
            .AppendLine("Comparing manual strings cannot grant any physical vehicle capability.");
        return report.ToString();
    }
}
