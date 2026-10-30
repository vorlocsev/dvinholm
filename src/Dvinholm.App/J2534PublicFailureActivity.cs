namespace Dvinholm.App;

/// <summary>
/// Fixed, user-actionable J2534 failure activity. This text is mirrored from
/// SetStatus to Aurora home, and must never depend on vendor DLL exception
/// messages, arbitrary driver errors, local paths or vehicle data.
/// </summary>
internal enum J2534PublicFailure
{
    ConnectionRejected,
    ConnectionFault,
    DisconnectFault,
    MonitorFaultDuringDisconnect,
    MonitorStartFault,
    FilterApplyRejected,
    FilterRemoveRejected,
    FilterUnexpected
}

internal static class J2534PublicFailureActivity
{
    internal static string Message(J2534PublicFailure failure) => failure switch
    {
        J2534PublicFailure.ConnectionRejected =>
            "J2534 connection failed. Check the selected 32-bit DLL, installed driver and CAN speed.",
        J2534PublicFailure.ConnectionFault =>
            "J2534 connection failed unexpectedly. Recheck the driver and reconnect.",
        J2534PublicFailure.DisconnectFault =>
            "J2534 disconnect failed. Session controls were reset; retry Connect to reinitialize.",
        J2534PublicFailure.MonitorFaultDuringDisconnect =>
            "Disconnecting J2534. The previous CAN monitor stopped with an error.",
        J2534PublicFailure.MonitorStartFault =>
            "CAN monitor could not start. Check the adapter connection and driver.",
        J2534PublicFailure.FilterApplyRejected =>
            "J2534 hardware filter was not applied. Check adapter support and the selected CAN ID.",
        J2534PublicFailure.FilterRemoveRejected =>
            "J2534 hardware filter could not be removed. Check the adapter connection.",
        J2534PublicFailure.FilterUnexpected =>
            "J2534 hardware filter operation failed unexpectedly. Check the adapter connection.",
        _ => throw new ArgumentOutOfRangeException(nameof(failure))
    };
}
