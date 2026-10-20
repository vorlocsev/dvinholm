using System.Text;

namespace Dvinholm.App;

/// <summary>
/// Deterministic, entirely local fixtures for demonstrating the read-only
/// UDS display formatters. Never opens a J2534 adapter or issues CAN/UDS I/O.
/// </summary>
internal static class UdsOfflineSample
{
    internal const string Disclaimer =
        "OFFLINE SYNTHETIC UDS SAMPLE — no adapter, ECU, or vehicle was queried. " +
        "All identifiers, values, and fault codes below are fictional.";

    internal static string Format()
    {
        // This is a fictional 17-character string accepted by the project's
        // format-only VIN parser; it is not a VIN read from any automobile.
        const string fictionalVin = "TEST0000000000ABC";
        var vinPayload = new byte[] { 0xF1, 0x90 }
            .Concat(Encoding.ASCII.GetBytes(fictionalVin))
            .ToArray();

        var didPayload = new byte[] { 0xF1, 0x87 }
            .Concat(Encoding.ASCII.GetBytes("DEMO-ECM"))
            .ToArray();

        UdsDtcRecord[] dtcs =
        [
            new UdsDtcRecord(0x123456, 0x08),
            new UdsDtcRecord(0x00C0DE, 0x04)
        ];

        return string.Join(Environment.NewLine,
        [
            Disclaimer,
            "",
            "Synthetic VIN (DID F190):",
            UdsDidDisplayFormatter.Format(0xF190, vinPayload),
            "",
            "Synthetic generic DID F187:",
            UdsDidDisplayFormatter.Format(0xF187, didPayload),
            "",
            "Synthetic DTC records (status mask FF):",
            UdsDtcDisplayFormatter.Format(dtcs, 0xFF)
        ]);
    }
}
