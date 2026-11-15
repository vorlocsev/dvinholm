namespace Dvinholm.App;

/// <summary>
/// Pure offline parser for already acquired P2 B9FC and P3 F106 configuration
/// data bodies. No adapter connection, requests, authentication or write path.
/// Adapted from the independently licensed MIT ReadOnlyFormats reference;
/// retain the license in Assets/ReadOnlyFormats-MIT.txt.
/// </summary>
internal static class CemConfigurationBlockDecoder
{
    internal const int P2ValueCount = 140;
    internal const int P3ValueCount = 252;
    internal const int P2BodyLength = P2ValueCount + 2;
    internal const int P3BodyLength = P3ValueCount + 2;

    // The caller must already have removed D2 transport framing.
    internal static byte[] DecodeP2B9Fc(ReadOnlySpan<byte> body) =>
        DecodeBody(body, P2ValueCount);

    // The caller must already have removed the positive UDS header 62 F1 06.
    internal static byte[] DecodeP3F106(ReadOnlySpan<byte> body) =>
        DecodeBody(body, P3ValueCount);

    private static byte[] DecodeBody(ReadOnlySpan<byte> body, int valueCount)
    {
        if (body.Length != valueCount + 2 || body[0] != valueCount)
            throw new FormatException("Configuration body count or length is invalid.");

        int checksum = 0;
        foreach (var value in body[2..])
            checksum = (checksum + value) & 0xFF;

        if (body[1] != checksum)
            throw new FormatException("Configuration body checksum is invalid.");

        // Detached bytes; an unknown option value is not a structural error
        // or evidence of a valid vehicle configuration.
        return body[2..].ToArray();
    }
}
