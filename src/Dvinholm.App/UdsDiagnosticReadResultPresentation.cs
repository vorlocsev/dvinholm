namespace Dvinholm.App;

/// <summary>
/// Read-only diagnostic response categories already supported by the
/// existing UDS client. This type does not initiate new vehicle requests.
/// </summary>
internal enum UdsReadResultKind
{
    Did,
    Vin,
    Dtc
}

/// <summary>
/// Keeps user-requested raw diagnostics in a selectable workspace panel,
/// while the status bar / Aurora last-event summary receives ONLY fixed,
/// payload-free text. VIN and DID values may identify a vehicle.
/// </summary>
internal sealed record UdsDiagnosticReadPresentation(string Activity, string Details);

internal static class UdsDiagnosticReadResultPresentation
{
    internal const string NoCompletedResult =
        "No completed UDS read in this session. A new read replaces the prior result.";

    internal static string OperationName(UdsReadResultKind kind) => kind switch
    {
        UdsReadResultKind.Did => "Read DID",
        UdsReadResultKind.Vin => "Read VIN",
        UdsReadResultKind.Dtc => "Read DTC",
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    internal static UdsDiagnosticReadPresentation Completed(
        UdsReadResultKind kind, string formattedResponse)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(formattedResponse);

        var operation = OperationName(kind);
        var caution = kind switch
        {
            UdsReadResultKind.Did => "DID payloads may contain identifiers or sensitive vehicle information.",
            UdsReadResultKind.Vin => "VIN identifies a vehicle; review this text before sharing.",
            UdsReadResultKind.Dtc => "These are ECU-reported codes, not confirmed causes or a vehicle-wide scan.",
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };

        return new(
            operation + " completed. Result available in the Diagnostics panel.",
            operation + " | selected ECU response" + Environment.NewLine +
            caution + Environment.NewLine +
            "Read-only response. No DTC clear, ECU programming or configuration write." +
            Environment.NewLine + Environment.NewLine +
            formattedResponse);
    }
}
