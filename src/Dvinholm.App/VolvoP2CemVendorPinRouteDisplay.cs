using System.Globalization;
using System.Text;

namespace Dvinholm.App;

/// <summary>
/// Pure renderer for a manual, source-scoped third-party vendor claim.
/// It does not parse VIN/PIN/credentials or communicate with any ECU.
/// Invalid raw text is never echoed in a report.
/// </summary>
internal static class VolvoP2CemVendorPinRouteDisplay
{
    internal static string Build(
        string? yearText, bool userReportsYearChecked,
        VolvoP2CemClaimedEngine claimedEngine, VolvoP2CemVendorPinTool tool)
    {
        var validYear = yearText is { Length: 4 } &&
            yearText.All(c => c is >= '0' and <= '9') &&
            int.TryParse(yearText, NumberStyles.None,
                CultureInfo.InvariantCulture, out _);
        int? year = validYear
            ? int.Parse(yearText!, CultureInfo.InvariantCulture)
            : null;

        var assessment = VolvoP2CemVendorPinRouteResearch.Assess(
            year, userReportsYearChecked, claimedEngine, tool);

        var engine = claimedEngine switch
        {
            VolvoP2CemClaimedEngine.OtherFiveCylinder => "other five-cylinder",
            VolvoP2CemClaimedEngine.D5_147Kw => "D5 147 kW",
            VolvoP2CemClaimedEngine.D5_120Kw => "D5 120 kW",
            VolvoP2CemClaimedEngine.InlineSix32 => "3.2 inline six",
            VolvoP2CemClaimedEngine.V8_44 => "4.4 V8",
            _ => "unknown / unclassified"
        };
        var product = tool switch
        {
            VolvoP2CemVendorPinTool.Vdash => "VDASH",
            VolvoP2CemVendorPinTool.Vdd => "VDD",
            _ => "UNKNOWN"
        };
        var classification = assessment.Kind switch
        {
            VolvoP2CemVendorPinClaimKind.AdvertisedForSomeP2Only =>
                "VENDOR ADVERTISES SOME P2 ONLY; NOT VERIFIED",
            VolvoP2CemVendorPinClaimKind.AdvertisedWithSeparateKeyProgrammingCaveat =>
                "VENDOR ADVERTISES DECODING; KEY PROGRAMMING SEPARATE",
            VolvoP2CemVendorPinClaimKind.ExcludedByVendorForThisEngine =>
                "VENDOR EXCLUDES THIS ENGINE FROM THIS PRODUCT CLAIM",
            VolvoP2CemVendorPinClaimKind.UnspecifiedForThisVariant =>
                "VENDOR EXAMPLE DOES NOT ESTABLISH THIS VARIANT",
            _ => "UNKNOWN / UNCONFIRMED"
        };

        return new StringBuilder()
            .AppendLine("OFFLINE VENDOR CLAIM COMPARISON - NOT ECU SUPPORT")
            .AppendLine("Source: D5T5 public VDASH/VDD descriptions only.")
            .AppendLine("Claimed XC90 model year: " +
                (year?.ToString(CultureInfo.InvariantCulture) ?? "UNKNOWN"))
            .AppendLine("Year checked: " +
                (userReportsYearChecked ? "USER CLAIM ONLY" : "NOT REPORTED"))
            .AppendLine("Claimed engine: " + engine)
            .AppendLine("Vendor product: " + product)
            .AppendLine("Classification: " + classification)
            .AppendLine("Vendor text: " + assessment.Explanation)
            .AppendLine("Separate key-programming caveat: " +
                (assessment.SeparateKeyProgrammingCaveat ? "YES" : "NOT SHOWN BY THIS SOURCE"))
            .AppendLine("CEM hardware/software identity: NOT VERIFIED")
            .AppendLine("Physical diagnostic transport: NOT VERIFIED")
            .AppendLine("PIN acquisition, key programming and ECU configuration: DISABLED")
            .AppendLine("No file, credentials, vehicle query or configuration edit.")
            .ToString();
    }
}
