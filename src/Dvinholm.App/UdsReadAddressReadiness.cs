using System.Globalization;

namespace Dvinholm.App;

/// <summary>
/// Pure, fail-closed readiness check for existing read-only UDS buttons.
/// A syntactically valid manual address pair does NOT establish ECU identity,
/// transport support, authorization or vehicle safety.
/// No J2534 session, CAN traffic or ECU command is initiated here.
/// </summary>
internal static class UdsReadAddressReadiness
{
    internal static bool CanRead(
        bool connected,
        bool busy,
        bool acceptanceMode,
        int formatIndex,
        string? requestText,
        string? responseText)
    {
        if (!connected || busy || acceptanceMode ||
            formatIndex is not (0 or 1) ||
            string.IsNullOrWhiteSpace(requestText) ||
            string.IsNullOrWhiteSpace(responseText))
            return false;

        var extended = formatIndex == 1;
        return MainWindow.TryParseCanId(requestText, extended, out var requestId, out _) &&
               MainWindow.TryParseCanId(responseText, extended, out var responseId, out _) &&
               requestId != responseId;
    }

    internal static bool ValidDid(string? didText)
    {
        if (string.IsNullOrWhiteSpace(didText))
            return false;

        var value = didText.Trim();
        if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            value = value[2..];

        return value.Length is > 0 and <= 4 &&
               ushort.TryParse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out _);
    }
}
