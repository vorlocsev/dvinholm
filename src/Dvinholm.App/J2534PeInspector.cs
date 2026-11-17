using System.Buffers.Binary;

namespace Dvinholm.App;

/// <summary>
/// Read-only PE machine inspection. This never loads a vendor DLL, calls
/// PassThruOpen, opens a vehicle session or executes vendor-supplied code.
/// It proves only file architecture, not driver functionality or trust.
/// </summary>
internal enum J2534PeArchitecture { Unknown, X86, X64, Arm64 }

internal static class J2534PeInspector
{
    private const int MaxPeHeaderOffset = 1024 * 1024;

    internal static J2534PeArchitecture Inspect(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return J2534PeArchitecture.Unknown;

        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete, 4096, FileOptions.SequentialScan);
            if (stream.Length < 70)
                return J2534PeArchitecture.Unknown;

            Span<byte> dos = stackalloc byte[64];
            stream.ReadExactly(dos);
            if (dos[0] != (byte)'M' || dos[1] != (byte)'Z')
                return J2534PeArchitecture.Unknown;

            var offset = BinaryPrimitives.ReadInt32LittleEndian(dos[0x3c..]);
            // Read only the fixed PE header, never arbitrary image sections.
            if (offset < 64 || offset > MaxPeHeaderOffset ||
                offset > stream.Length - 6)
                return J2534PeArchitecture.Unknown;

            stream.Position = offset;
            Span<byte> pe = stackalloc byte[6];
            stream.ReadExactly(pe);
            if (pe[0] != (byte)'P' || pe[1] != (byte)'E' ||
                pe[2] != 0 || pe[3] != 0)
                return J2534PeArchitecture.Unknown;

            return BinaryPrimitives.ReadUInt16LittleEndian(pe[4..]) switch
            {
                0x014c => J2534PeArchitecture.X86,
                0x8664 => J2534PeArchitecture.X64,
                0xaa64 => J2534PeArchitecture.Arm64,
                _ => J2534PeArchitecture.Unknown
            };
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
            ArgumentException or NotSupportedException or System.Security.SecurityException)
        {
            // Unreadable, missing, malformed or disappearing files are never
            // considered safe to load into the current process.
            return J2534PeArchitecture.Unknown;
        }
    }

    internal static bool CanLoad(J2534PeArchitecture architecture, bool is64BitProcess) =>
        is64BitProcess ? architecture == J2534PeArchitecture.X64
            : architecture == J2534PeArchitecture.X86;
}
