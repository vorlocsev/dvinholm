using System.Globalization;
using System.Text;

namespace Dvinholm.App;

/// <summary>
/// Categories mentioned in a third-party reproduction of a MY2007 XC90
/// workshop manual. Categories describe human-facing VIDA functions, NOT
/// protocol message or byte formats. They must not be conflated.
/// </summary>
internal enum VolvoP2CemReadSurface
{
    ControlModuleIdentity,
    LiveDiagnosticParameter,
    FrozenDiagnosticContext,
    CustomerProgrammedData,
    VehicleConfigurationFile
}

internal enum VolvoP2CemReadSurfaceEvidenceStatus
{
    NoUsableClaimedYear,
    OutsideExactPublishedExampleYear,
    WorkshopFunctionReferenceOnly
}

internal sealed record VolvoP2CemReadSurfaceReference(
    VolvoP2CemReadSurface Surface,
    int ExampleModelYear,
    string Meaning,
    string SourceUrl,
    bool MayContainVehicleIdentity);

internal sealed record VolvoP2CemReadSurfaceAssessment(
    VolvoP2CemReadSurfaceReference Reference,
    VolvoP2CemReadSurfaceEvidenceStatus Status)
{
    // Neither the source nor a user-entered year supplies a wire-format
    // decoder, session, CEM identity or physical test.
    internal bool HasDocumentedDiagnosticAddressing => false;
    internal bool HasRevisionScopedWireEncoding => false;
    internal bool HasAuthenticatedPhysicalModule => false;
    internal bool MayReadLiveCemData => false;
    internal bool IsRestorableBackup => false;
    internal bool MayAcquirePin => false;
    internal bool MayWriteConfiguration => false;
}

/// <summary>
/// An immutable, source-scoped, offline classification of five different
/// MY2007 CEM read surfaces. Even a matching claimed MY is NOT evidence
/// that the functions are supported by the user's CEM revision.
/// </summary>
internal static class VolvoP2CemReadSurfaceEvidence
{
    internal const int SourceExampleModelYear = 2007;
    internal const string DiagnosticFunctionsSource =
        "https://lemon-manuals.org.ua/Volvo/2007/XC90%20FWD%20L6-3.2L%20VIN%2098%20B6324S/" +
        "Repair%20and%20Diagnosis/Body%20and%20Frame/Body%20Control%20Systems/" +
        "Description%20and%20Operation/Central%20Electronic%20Module%20%28CEM%29/Diagnostic%20Functions/";
    internal const string SystemOverviewSource =
        "https://lemon-manuals.org.ua/Volvo/2007/XC90%20FWD%20L6-3.2L%20VIN%2098%20B6324S/" +
        "Repair%20and%20Diagnosis/Body%20and%20Frame/Body%20Control%20Systems/" +
        "Description%20and%20Operation/Central%20Electronic%20Module%20%28CEM%29/System%20Overview/";

    private static readonly IReadOnlyList<VolvoP2CemReadSurfaceReference> Catalog =
        Array.AsReadOnly(new[]
        {
            new VolvoP2CemReadSurfaceReference(
                VolvoP2CemReadSurface.ControlModuleIdentity, SourceExampleModelYear,
                "VIDA identification displays hardware, software and diagnostic-software part numbers.",
                DiagnosticFunctionsSource, false),
            new VolvoP2CemReadSurfaceReference(
                VolvoP2CemReadSurface.LiveDiagnosticParameter, SourceExampleModelYear,
                "Status/value displayed through a VIDA diagnostic parameter function, not a saved configuration.",
                DiagnosticFunctionsSource, false),
            new VolvoP2CemReadSurfaceReference(
                VolvoP2CemReadSurface.FrozenDiagnosticContext, SourceExampleModelYear,
                "Parameters/status/counters captured in the context of a DTC, not a live settings file.",
                DiagnosticFunctionsSource, false),
            new VolvoP2CemReadSurfaceReference(
                VolvoP2CemReadSurface.CustomerProgrammedData, SourceExampleModelYear,
                "VIDA read/program customer data category; no CEM service IDs, byte mappings or safe write method published.",
                DiagnosticFunctionsSource, false),
            new VolvoP2CemReadSurfaceReference(
                VolvoP2CemReadSurface.VehicleConfigurationFile, SourceExampleModelYear,
                "CEM vehicle configuration file includes VIN/structure week/equipment; must not be confused with customer settings.",
                SystemOverviewSource, true)
        });

    internal static IReadOnlyList<VolvoP2CemReadSurfaceReference> Entries => Catalog;

    internal static VolvoP2CemReadSurfaceAssessment Assess(
        int? claimedModelYear, VolvoP2CemReadSurface surface)
    {
        var reference = Catalog.SingleOrDefault(x => x.Surface == surface)
            ?? throw new ArgumentOutOfRangeException(nameof(surface),
                "Undefined CEM read category.");
        var status = claimedModelYear is null or < 2003 or > 2014
            ? VolvoP2CemReadSurfaceEvidenceStatus.NoUsableClaimedYear
            : claimedModelYear.Value == reference.ExampleModelYear
                ? VolvoP2CemReadSurfaceEvidenceStatus.WorkshopFunctionReferenceOnly
                : VolvoP2CemReadSurfaceEvidenceStatus.OutsideExactPublishedExampleYear;
        return new(reference, status);
    }

    internal static string BuildOfflineReport(int? claimedModelYear)
    {
        var report = new StringBuilder()
            .AppendLine("XC90 P2/CEM READ FORMAT RESEARCH (OFFLINE)")
            .AppendLine("Exact source example: XC90 MY2007 workshop description (third-party reproduction).")
            .AppendLine("A claimed year is not an authenticated vehicle/model identity.")
            .AppendLine("These are FIVE DIFFERENT VIDA-facing data categories, NOT protocol byte layouts.");

        foreach (var source in Catalog)
        {
            var assessment = Assess(claimedModelYear, source.Surface);
            report.Append("• ").Append(source.Surface).Append(": ")
                .AppendLine(assessment.Status.ToString())
                .Append("  ").AppendLine(source.Meaning)
                .Append("  Example source year: ")
                .AppendLine(source.ExampleModelYear.ToString(CultureInfo.InvariantCulture));
        }

        return report
            .AppendLine("MISSING for every category: exact physical CEM revision and identity,")
            .AppendLine("diagnostic transport/address, documented request/response encoding,")
            .AppendLine("lengths, negative responses, physical J2534 acceptance and authorization.")
            .AppendLine("The generic UDS DID snapshot is neither CEM config nor a restorable backup.")
            .AppendLine("Vehicle configuration may contain identifying data; do not export or log it.")
            .AppendLine("No new CAN/K-line/UDS request. PIN, CEM configuration reads and writes: DISABLED.")
            .ToString();
    }
}
