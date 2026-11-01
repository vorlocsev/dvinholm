using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace Dvinholm.App;

/// <summary>Windows DPAPI current-user protection; no machine-wide flag or UI.</summary>
internal static class CemPinWindowsProtection
{
    [StructLayout(LayoutKind.Sequential)]
    private struct Blob { internal int Length; internal IntPtr Data; }

    [DllImport("crypt32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptProtectData(ref Blob input, string? description,
        ref Blob entropy, IntPtr reserved, IntPtr prompt, uint flags, out Blob output);

    [DllImport("crypt32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptUnprotectData(ref Blob input, IntPtr description,
        ref Blob entropy, IntPtr reserved, IntPtr prompt, uint flags, out Blob output);

    [DllImport("kernel32.dll")]
    private static extern IntPtr LocalFree(IntPtr memory);

    internal static unsafe byte[] Transform(byte[] input, Guid recordId, bool protect)
    {
        var entropy = new byte[48];
        "Dvinholm.CemPinStore.v1"u8.CopyTo(entropy);
        recordId.TryWriteBytes(entropy.AsSpan(32));
        Blob output = default;
        try
        {
            fixed (byte* data = input, salt = entropy)
            {
                var source = new Blob { Length = input.Length, Data = (IntPtr)data };
                var extra = new Blob { Length = entropy.Length, Data = (IntPtr)salt };
                const uint uiForbidden = 1; // Deliberately omit CRYPTPROTECT_LOCAL_MACHINE.
                var ok = protect
                    ? CryptProtectData(ref source, null, ref extra, IntPtr.Zero, IntPtr.Zero, uiForbidden, out output)
                    : CryptUnprotectData(ref source, IntPtr.Zero, ref extra, IntPtr.Zero, IntPtr.Zero, uiForbidden, out output);
                if (!ok || output.Data == IntPtr.Zero || output.Length is <= 0 or > 8192)
                    throw new CryptographicException("PIN record protection is unavailable.");
                var result = new byte[output.Length];
                Marshal.Copy(output.Data, result, 0, result.Length);
                return result;
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(entropy);
            if (output.Data != IntPtr.Zero)
            {
                if (output.Length is > 0 and <= 8192)
                    CryptographicOperations.ZeroMemory(new Span<byte>((void*)output.Data, output.Length));
                LocalFree(output.Data);
            }
        }
    }
}
