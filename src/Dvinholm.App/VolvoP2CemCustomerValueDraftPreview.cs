using System.Globalization;
using System.Text;

namespace Dvinholm.App;

/// <summary>
/// Ephemeral UI preview for ONE user-proposed historical XC90 CEM customer
/// parameter value. No storage, ECU/session/profile object, CAN bytes,
/// PIN, write plan, authorization or vehicle identity is accepted.
/// </summary>
internal static class VolvoP2CemCustomerValueDraftPreview
{
    internal static string Build(
        string? claimedModelYearText,
        bool userSaysYearIndependentlyChecked,
        VolvoP2CemCustomerParameterKind parameter,
        string? proposedValue,
        string? proposedSeatHighValue = null)
    {
        // Never echo arbitrary untrusted input, including accidental VINs,
        // PINs or passwords. Print proposed values ONLY after matching an
        // exact, short canonical token in the historical research catalog.
        int? year = claimedModelYearText is { Length: 4 } &&
                    int.TryParse(claimedModelYearText, NumberStyles.None,
                        CultureInfo.InvariantCulture, out var parsedYear) &&
                    claimedModelYearText == parsedYear.ToString(CultureInfo.InvariantCulture)
            ? parsedYear : null;

        var assessment = VolvoP2CemHistoricalCustomerValuePreflight.Assess(
            year, userSaysYearIndependentlyChecked,
            parameter, proposedValue, proposedSeatHighValue);

        var text = new StringBuilder()
            .AppendLine("XC90 P2/CEM HISTORICAL VALUE DRAFT (OFFLINE ONLY)")
            .AppendLine("An unverified USER proposal, not a current ECU value or a saved setting.")
            .AppendLine("The model-year check is self-reported, NOT authenticated to a vehicle.")
            .AppendLine("No diagnostic connection, DID, firmware encoding, PIN or write plan exists.")
            .Append("Review result: ").AppendLine(assessment.Kind.ToString())
            .AppendLine(assessment.Explanation);

        if (assessment.Kind == VolvoP2CemCustomerValueReviewKind.ListedInHistoricalSourceOnly)
        {
            text.Append("Historical setting label: ").AppendLine(parameter switch
            {
                VolvoP2CemCustomerParameterKind.SeatHeaterLowHighCelsius =>
                    "Heated seat LOW and HIGH (degrees Celsius)",
                VolvoP2CemCustomerParameterKind.ApproachLightsSeconds =>
                    "Approach lighting (seconds)",
                VolvoP2CemCustomerParameterKind.PostDrying =>
                    "Post-drying switch",
                VolvoP2CemCustomerParameterKind.DaytimeRunningLamps =>
                    "Daytime running lamps (country-specific legal review required)",
                _ => "Unknown"
            });
            text.Append("Proposed research notation: ").Append(proposedValue);
            if (parameter == VolvoP2CemCustomerParameterKind.SeatHeaterLowHighCelsius)
                text.Append(" LOW; ").Append(proposedSeatHighValue).Append(" HIGH");
            text.AppendLine();
        }
        else
        {
            text.AppendLine("Proposed input is NOT approved or printed; enter a source-listed canonical value.");
        }

        text.AppendLine("Current vehicle value: UNKNOWN (no ECU read was performed).")
            .AppendLine("Vehicle configuration file and Volvo software products are DIFFERENT operations.")
            .AppendLine("Closing this dialog discards this draft. No file, profile or setting is changed.")
            .AppendLine("No CAN/J2534/UDS, PIN/security access or CEM configuration write is available.");
        return text.ToString();
    }
}
