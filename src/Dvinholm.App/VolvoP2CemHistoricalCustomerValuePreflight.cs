using System.Globalization;
using System.Text;

namespace Dvinholm.App;

/// <summary>
/// Historical XC90 customer-parameter value notation. This is an OFFLINE
/// source-research preflight, not a decoded CEM setting, authenticated
/// firmware dictionary, CAN command or executable programming procedure.
/// </summary>
internal enum VolvoP2CemCustomerParameterKind
{
    SeatHeaterLowHighCelsius,
    ApproachLightsSeconds,
    PostDrying,
    DaytimeRunningLamps
}

internal enum VolvoP2CemCustomerValueReviewKind
{
    VehicleYearUnconfirmed,
    OutsideHistoricalSourceScope,
    UnknownParameter,
    MissingDesiredValue,
    NotListedOrInvalidRelationship,
    ListedInHistoricalSourceOnly
}

internal sealed record VolvoP2CemCustomerValueReview(
    VolvoP2CemCustomerValueReviewKind Kind, string Explanation)
{
    internal bool HasConfirmedCurrentVehicleValue => false;
    internal bool HasAuthenticatedCemFirmwareMapping => false;
    internal bool MayReadLiveConfiguration => false;
    internal bool MaySendJ2534OrUds => false;
    internal bool MayWriteConfiguration => false;
    internal bool MayAccessPinOrSecurity => false;
}

/// <summary>
/// Captures specific published workshop-value ranges for early XC90 P2.
/// Historical source is a third-party republication, NOT an original
/// per-firmware Volvo value-to-byte map. Do not extrapolate beyond scope.
/// </summary>
internal static class VolvoP2CemHistoricalCustomerValuePreflight
{
    internal const int FirstResearchModelYear = 2003;
    internal const int LastResearchModelYear = 2006;
    internal const string Source =
        "https://diagnostdata.com/volvo/xc90/i-2002-2006/remont/miscellaneous-electrical-design-and-function/reading-off-and-programming-data/reading-off-and-programming-data/";

    internal static IReadOnlyList<int> SeatTemperatureCelsius { get; } =
        Array.AsReadOnly(new[] { 0, 10, 28, 30, 31, 32, 33, 34, 35,
                                 36, 37, 38, 39, 40, 41, 43 });
    internal static IReadOnlyList<int> ApproachLightSeconds { get; } =
        Array.AsReadOnly(new[] { 0, 30, 60, 90 });
    internal static IReadOnlyList<string> SwitchValues { get; } =
        Array.AsReadOnly(new[] { "OFF", "ON" });
    internal static IReadOnlyList<string> DaytimeLampModes { get; } =
        Array.AsReadOnly(new[] { "FLEX_ZERO", "FLEX_STANDARD", "FLEX_LOW" });

    /// <summary>
    /// For SeatHeaterLowHighCelsius, desiredValue is LOW and secondValue is
    /// HIGH, in degrees C. All other kinds use a single canonical value.
    /// This does not look at actual car data or choose any ECU service.
    /// </summary>
    internal static VolvoP2CemCustomerValueReview Assess(
        int? independentlyObservedModelYear,
        bool modelYearIndependentlyConfirmed,
        VolvoP2CemCustomerParameterKind parameter,
        string? desiredValue,
        string? secondValue = null)
    {
        if (independentlyObservedModelYear is null || !modelYearIndependentlyConfirmed)
            return new(VolvoP2CemCustomerValueReviewKind.VehicleYearUnconfirmed,
                "An independently confirmed XC90 model year is required even for this offline source comparison.");

        if (independentlyObservedModelYear is < FirstResearchModelYear or > LastResearchModelYear)
            return new(VolvoP2CemCustomerValueReviewKind.OutsideHistoricalSourceScope,
                "Historical 2002-2006 document cannot establish values for this XC90 model year.");

        if (!Enum.IsDefined(parameter))
            return new(VolvoP2CemCustomerValueReviewKind.UnknownParameter,
                "Unknown parameter label. No programming or read capability exists.");

        if (string.IsNullOrEmpty(desiredValue) ||
            (parameter == VolvoP2CemCustomerParameterKind.SeatHeaterLowHighCelsius &&
             string.IsNullOrEmpty(secondValue)))
            return new(VolvoP2CemCustomerValueReviewKind.MissingDesiredValue,
                "Complete the proposed offline value or both seat-heater levels.");

        bool listed = parameter switch
        {
            VolvoP2CemCustomerParameterKind.SeatHeaterLowHighCelsius =>
                IntegerListed(desiredValue, SeatTemperatureCelsius, out var low) &&
                IntegerListed(secondValue, SeatTemperatureCelsius, out var high) &&
                high > low,
            VolvoP2CemCustomerParameterKind.ApproachLightsSeconds =>
                secondValue is null && IntegerListed(desiredValue, ApproachLightSeconds, out _),
            VolvoP2CemCustomerParameterKind.PostDrying =>
                secondValue is null && SwitchValues.Contains(desiredValue, StringComparer.Ordinal),
            VolvoP2CemCustomerParameterKind.DaytimeRunningLamps =>
                secondValue is null && DaytimeLampModes.Contains(desiredValue, StringComparer.Ordinal),
            _ => false
        };

        return listed
            ? new(VolvoP2CemCustomerValueReviewKind.ListedInHistoricalSourceOnly,
                "The proposed notation appears in a historical workshop-source reproduction. " +
                "This is NOT a verified current value, firmware mapping or executable ECU command.")
            : new(VolvoP2CemCustomerValueReviewKind.NotListedOrInvalidRelationship,
                "Unlisted, noncanonical or inconsistent proposed value. No operation is permitted.");
    }

    internal static string BuildOfflineOverview()
    {
        var text = new StringBuilder()
            .AppendLine("XC90 P2 CEM customer-parameter value research (OFFLINE)")
            .AppendLine("Source scope: historical XC90 first-generation MY2003-2006 only.")
            .AppendLine("Reproduced workshop text, not independent CEM revision/encoding verification.")
            .AppendLine("Documented CUSTOMER PARAMETERS are distinct from the CEM vehicle-configuration")
            .AppendLine("file, Volvo central-database software products and immobilizer/PIN authorization.")
            .AppendLine("Seat heater: separate left/right and LOW/HIGH, HIGH must exceed LOW.")
            .Append("  Historical Celsius candidates: ")
            .AppendLine(string.Join(", ", SeatTemperatureCelsius))
            .Append("Approach lights: ")
            .Append(string.Join(", ", ApproachLightSeconds))
            .AppendLine(" seconds (historical 30-second default).")
            .AppendLine("Post drying: OFF / ON (documented workshop setting names only).")
            .AppendLine("Daytime running lamps: FLEX_ZERO / FLEX_STANDARD / FLEX_LOW.")
            .AppendLine("  Country-specific road lighting rules and installed equipment must be verified.")
            .AppendLine("No live current values, actual parameter IDs, bytes, DIDs, CAN addresses,")
            .AppendLine("firmware mapping, rollback procedure or validated power conditions exist.")
            .AppendLine("Values may be compared offline only after independently confirming year.")
            .AppendLine("No PIN, live config read, J2534 request, ECU edit or write enabled.")
            .Append("Source: ").AppendLine(Source);
        return text.ToString();
    }

    private static bool IntegerListed(string? text,
        IReadOnlyList<int> candidates, out int parsed)
    {
        parsed = 0;
        return text is { Length: > 0 } &&
               int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out parsed) &&
               string.Equals(text, parsed.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal) &&
               candidates.Contains(parsed);
    }
}
