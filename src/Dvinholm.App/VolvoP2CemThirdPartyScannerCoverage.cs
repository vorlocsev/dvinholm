using System.Text;

namespace Dvinholm.App;

/// <summary>
/// Exact source-scoped published third-party scanner feature labels, NOT
/// supported operations in Dvinholm or verified XC90 P2 CEM protocols.
/// The source lists functions by model year; not by CEM firmware revision.
/// </summary>
internal enum VolvoP2CemPublishedFunction
{
    ControlUnitIdentification,
    VehicleConfigurationDataReading,
    ImmobilizerParameterReading,
    ProgrammedValues
}

internal enum VolvoP2CemPublishedModule
{
    Cem,
    CemHs
}

internal enum VolvoP2CemPublishedCoverageKind
{
    ModelYearNotConfirmed,
    PublishedScannerListingOnly,
    NoListingForExactYearAndModule
}

internal sealed record VolvoP2CemPublishedFunctionEntry(
    int ModelYear,
    VolvoP2CemPublishedModule Module,
    VolvoP2CemPublishedFunction Function,
    string PublishedLabel,
    int SourcePdfPage);

internal sealed record VolvoP2CemPublishedCoverageAssessment(
    VolvoP2CemPublishedCoverageKind Kind,
    string Explanation)
{
    // An advertised third-party menu is not an executable service.
    internal bool MayConnectCem => false;
    internal bool HasVerifiedRequestOrResponseCanId => false;
    internal bool HasVerifiedDiagnosticCommand => false;
    internal bool MayReadRealConfiguration => false;
    internal bool MayAcquirePin => false;
    internal bool MayWriteConfiguration => false;
}

/// <summary>
/// Research-only, fixed-source catalogue. Do not merge "CEM" with "CEM-HS"
/// or equate MY2005 "Reading data for vehicle configuration" with
/// MY2007/MY2008 "Programmed values": they are distinct printed labels.
/// </summary>
internal static class VolvoP2CemThirdPartyScannerCoverage
{
    internal const string Source =
        "https://assets.bucketcdn.com/Producten/Foxwell/Foxwell-NT530-Volvo/NT530_Volvo_functions.pdf";
    internal const string ListedScanner = "Foxwell NT530 Volvo";
    internal const string ListedSoftwareVersion = "V1.10.001";

    internal static IReadOnlyList<VolvoP2CemPublishedFunctionEntry> Entries { get; } =
        Array.AsReadOnly(new[]
        {
            new VolvoP2CemPublishedFunctionEntry(2005, VolvoP2CemPublishedModule.Cem,
                VolvoP2CemPublishedFunction.ControlUnitIdentification, "Reading control unit ID", 184),
            new VolvoP2CemPublishedFunctionEntry(2005, VolvoP2CemPublishedModule.Cem,
                VolvoP2CemPublishedFunction.VehicleConfigurationDataReading,
                "Reading data for vehicle configuration", 184),
            new VolvoP2CemPublishedFunctionEntry(2005, VolvoP2CemPublishedModule.Cem,
                VolvoP2CemPublishedFunction.ImmobilizerParameterReading,
                "Reading off parameters, immobilizer function", 184),
            new VolvoP2CemPublishedFunctionEntry(2007, VolvoP2CemPublishedModule.Cem,
                VolvoP2CemPublishedFunction.ControlUnitIdentification, "ECU identification", 128),
            new VolvoP2CemPublishedFunctionEntry(2007, VolvoP2CemPublishedModule.Cem,
                VolvoP2CemPublishedFunction.ProgrammedValues, "Programmed values", 128),
            new VolvoP2CemPublishedFunctionEntry(2007, VolvoP2CemPublishedModule.CemHs,
                VolvoP2CemPublishedFunction.ControlUnitIdentification, "ECU identification", 129),
            new VolvoP2CemPublishedFunctionEntry(2008, VolvoP2CemPublishedModule.Cem,
                VolvoP2CemPublishedFunction.ControlUnitIdentification, "ECU identification", 129),
            new VolvoP2CemPublishedFunctionEntry(2008, VolvoP2CemPublishedModule.Cem,
                VolvoP2CemPublishedFunction.ProgrammedValues, "Programmed values", 129),
            new VolvoP2CemPublishedFunctionEntry(2008, VolvoP2CemPublishedModule.CemHs,
                VolvoP2CemPublishedFunction.ControlUnitIdentification, "ECU identification", 130)
        });

    internal static VolvoP2CemPublishedCoverageAssessment Assess(
        int? modelYear, bool modelYearIndependentlyConfirmed,
        VolvoP2CemPublishedModule module, VolvoP2CemPublishedFunction function)
    {
        if (modelYear is null || !modelYearIndependentlyConfirmed)
            return new(VolvoP2CemPublishedCoverageKind.ModelYearNotConfirmed,
                "A typed year is not independent confirmation of a physical vehicle.");

        return Entries.Any(e => e.ModelYear == modelYear &&
                                e.Module == module && e.Function == function)
            ? new(VolvoP2CemPublishedCoverageKind.PublishedScannerListingOnly,
                "This third-party scanner advertises a menu for this model year; " +
                "no CEM hardware, protocol, DID, security or Dvinholm support was verified.")
            : new(VolvoP2CemPublishedCoverageKind.NoListingForExactYearAndModule,
                "No function listed in this deliberately limited source excerpt. " +
                "Absence is not proof of incompatibility.");
    }

    internal static string BuildOfflineOverview()
    {
        var output = new StringBuilder()
            .AppendLine("XC90 P2 CEM third-party scanner function research (OFFLINE)")
            .Append("Published Foxwell NT530 Volvo feature matrix, software ")
            .AppendLine(ListedSoftwareVersion)
            .AppendLine("SOURCE: THIRD-PARTY TOOL FEATURE LIST, not a Volvo protocol specification.")
            .AppendLine("Listed vehicle model years are vendor labels, NOT verified ECU revisions.")
            .AppendLine("The table has no raw CEM address, DID, bytes, PIN or executable command.")
            .AppendLine("2005 CEM 'Reading data for vehicle configuration' is NOT equivalent")
            .AppendLine("to 2007/2008 CEM 'Programmed values'; meanings are undocumented.")
            .AppendLine("CEM-HS is a SEPARATE listed module, NOT a synonym or confirmed bus address.")
            .AppendLine();
        foreach (var e in Entries)
        {
            output.Append("• MY").Append(e.ModelYear)
                .Append(" ").Append(e.Module == VolvoP2CemPublishedModule.Cem ? "CEM" : "CEM-HS")
                .Append(": ").Append(e.PublishedLabel)
                .Append(" [PDF page ").Append(e.SourcePdfPage).AppendLine("]");
        }
        output.AppendLine();
        output.Append("Evidence: ").AppendLine(Source);
        output.AppendLine("No function can be called. No vehicle data or settings were read.");
        output.AppendLine("No PIN/security access, real configuration read or write is enabled.");
        output.AppendLine("Use independently sourced revision-specific transport and authorized")
            .AppendLine("physical acceptance before any actual CEM implementation.");
        return output.ToString();
    }
}
