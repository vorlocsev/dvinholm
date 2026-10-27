namespace Dvinholm.App;

/// <summary>
/// UI preflight for a read-only J2534 DID capture. A saved ECU profile is not
/// proof of ECU identity; never silently capture from edited CAN IDs or run
/// this operation during disconnected/offline-acceptance sessions.
/// </summary>
internal static class EcuSnapshotCaptureUiGate
{
    internal static bool CanCapture(
        EcuProfile? selectedProfile,
        bool isConnected,
        bool isAcceptanceSession,
        bool isBusy,
        uint? currentRequestId,
        uint? currentResponseId,
        bool? isExtended)
    {
        if (!isConnected || isAcceptanceSession || isBusy ||
            selectedProfile is null ||
            selectedProfile.ReadableDataIdentifiers.Count is < 1 or > EcuConfigurationReadback.MaximumDataIdentifiers ||
            currentRequestId is null || currentResponseId is null || isExtended is null)
            return false;

        return selectedProfile.RequestId == currentRequestId.Value &&
               selectedProfile.ResponseId == currentResponseId.Value &&
               selectedProfile.IsExtended == isExtended.Value;
    }
}
