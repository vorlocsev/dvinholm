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

    /// <summary>
    /// Public activity is also shown on the Aurora home screen. Exception
    /// messages can contain adapter paths, raw transport responses or VIN/DID
    /// data, so never route an arbitrary Exception.Message into that activity.
    /// Only the structured UDS service/NRC bytes and a trusted fixed glossary
    /// may be shown for a negative response.
    /// </summary>
    internal static string Error(UdsReadResultKind kind, Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        var operation = OperationName(kind);
        return exception switch
        {
            UdsNegativeResponseException nrc =>
                $"{operation} rejected: UDS 0x{nrc.RequestedService:X2}, NRC 0x{nrc.NegativeResponseCode:X2} " +
                $"({UdsNegativeResponseException.DescribeNegativeResponseCode(nrc.NegativeResponseCode)}).",
            OperationCanceledException => operation + " cancelled.",
            TimeoutException => operation + " timeout. Check the adapter and selected ECU addresses.",
            _ => operation + " failed. Check the adapter connection and diagnostic settings."
        };
    }

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
