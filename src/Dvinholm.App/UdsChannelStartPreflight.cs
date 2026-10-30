namespace Dvinholm.App;

/// <summary>
/// Pure preflight for an existing read-only UDS channel. The UI must validate
/// again immediately before opening ISO-TP: enabled buttons alone are not a
/// security or session boundary. No bus access or ECU request happens here.
/// </summary>
internal static class UdsChannelStartPreflight
{
    internal const string ChannelFailureActivity =
        "Unable to initialize the UDS channel. Check the adapter and selected CAN IDs.";

    internal static bool TryPrepare(
        bool connected,
        int formatIndex,
        string? requestText,
        string? responseText,
        out uint requestId,
        out uint responseId,
        out bool extended,
        out string error)
    {
        requestId = 0;
        responseId = 0;
        extended = false;
        error = string.Empty;

        if (!connected)
        {
            error = "Connect to J2534 first.";
            return false;
        }

        if (formatIndex is not (0 or 1))
        {
            error = "Select 11-bit or 29-bit CAN ID format.";
            return false;
        }

        extended = formatIndex == 1;
        if (string.IsNullOrWhiteSpace(requestText) ||
            string.IsNullOrWhiteSpace(responseText))
        {
            error = "Enter both request and response CAN IDs before reading.";
            return false;
        }

        if (!MainWindow.TryParseCanId(requestText, extended, out requestId, out error) ||
            !MainWindow.TryParseCanId(responseText, extended, out responseId, out error))
        {
            requestId = 0;
            responseId = 0;
            return false;
        }

        if (requestId == responseId)
        {
            requestId = 0;
            responseId = 0;
            error = "Request and response CAN IDs must be different.";
            return false;
        }

        return true;
    }
}
