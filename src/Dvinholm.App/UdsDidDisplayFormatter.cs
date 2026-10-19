using System.Text;

namespace Dvinholm.App;

/// <summary>
/// Displays a read-only UDS ReadDataByIdentifier result without guessing
/// vehicle-specific units or assigning meanings to undocumented DIDs.
/// </summary>
internal static class UdsDidDisplayFormatter
{
    internal static string Format(ushort requestedDid, byte[] didAndData)
    {
        ArgumentNullException.ThrowIfNull(didAndData);

        // UdsClient.ReadDataByIdentifier returns the two DID bytes followed by
        // the ECU's value bytes. Keep those bytes in the output for diagnostics.
        if (didAndData.Length < 2 ||
            didAndData[0] != (byte)(requestedDid >> 8) ||
            didAndData[1] != (byte)requestedDid)
        {
            throw new InvalidOperationException(
                $"UDS response does not contain requested DID 0x{requestedDid:X4}.");
        }

        var raw = BitConverter.ToString(didAndData).Replace('-', ' ');
        var value = didAndData.AsSpan(2);
        var prefix = $"DID 0x{requestedDid:X4}";

        // F190 is a standardized identifier. Use the same strict VIN
        // validation as the dedicated Read VIN operation.
        if (requestedDid == 0xF190)
        {
            try
            {
                var vin = UdsClient.ParseVinData(didAndData);
                return $"{prefix} (VIN): {vin} | raw: {raw}";
            }
            catch (InvalidOperationException)
            {
                // Read DID must still expose the raw reply if an ECU returns
                // nonstandard VIN data. Do not mislabel it as a valid VIN.
                return $"{prefix} (VIN format invalid): raw: {raw}";
            }
        }

        if (value.IsEmpty)
            return $"{prefix}: no value bytes | raw: {raw}";

        // This is a convenience preview, NOT a diagnostic interpretation.
        // Binary payloads and vehicle-specific encodings require documented
        // definitions before values or physical units can be assigned.
        if (value.Length >= 3 && value.Length <= 128 && IsPrintableAscii(value))
        {
            var preview = Encoding.ASCII.GetString(value);
            return $"{prefix}: ASCII preview (unverified): \"{preview}\" | raw: {raw}";
        }

        return $"{prefix}: raw: {raw}";
    }

    private static bool IsPrintableAscii(ReadOnlySpan<byte> value)
    {
        foreach (var b in value)
        {
            if (b is < 0x20 or > 0x7E)
                return false;
        }

        return true;
    }
}
