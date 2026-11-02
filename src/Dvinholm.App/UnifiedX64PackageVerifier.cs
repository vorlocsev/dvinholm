using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace Dvinholm.App;

/// <summary>
/// Offline validator of an already downloaded, local x64 ZIP. No network access,
/// extraction, process launch or in-place replacement takes place here.
/// Package integrity is not code-signature/provenance or safe-install approval.
/// </summary>
internal sealed record UnifiedX64VerifiedArchive(string Sha256, int EntryCount, long ExpandedBytes);

internal static class UnifiedX64PackageVerifier
{
    internal const long MaximumZipBytes = 500L * 1024 * 1024;
    internal const long MaximumExpandedBytes = 1024L * 1024 * 1024;
    internal const int MaximumEntries = 10_000;
    internal const int MaximumManifestChars = 64 * 1024;

    internal static UnifiedX64VerifiedArchive Verify(
        string zipPath, string checksumManifest, long expectedZipBytes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(zipPath);
        ArgumentNullException.ThrowIfNull(checksumManifest);
        if (expectedZipBytes <= 0 || expectedZipBytes > MaximumZipBytes)
            throw new InvalidDataException("Invalid x64 ZIP expected size.");

        // There must be exactly one checksum for this file; other artifacts
        // may be listed by a release, but neither aliases nor duplicate x64
        // records may override its digest.
        var expectedHash = GetExpectedDigest(checksumManifest);
        using var input = new FileStream(zipPath, FileMode.Open, FileAccess.Read,
            FileShare.Read, 64 * 1024, FileOptions.SequentialScan);
        if (input.Length != expectedZipBytes)
            throw new InvalidDataException("x64 ZIP size mismatch.");

        var actualHash = SHA256.HashData(input);
        if (!CryptographicOperations.FixedTimeEquals(actualHash, expectedHash))
            throw new InvalidDataException("x64 ZIP checksum mismatch.");
        input.Position = 0;

        using var archive = new ZipArchive(input, ZipArchiveMode.Read, leaveOpen: true);
        if (archive.Entries.Count is 0 or > MaximumEntries)
            throw new InvalidDataException("Invalid x64 ZIP entry count.");

        long total = 0;
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in archive.Entries)
        {
            var name = entry.FullName;
            if (!ValidEntryName(name) || !names.Add(name))
                throw new InvalidDataException("Unsafe or duplicate ZIP entry.");
            // Reject symlinks, which can redirect extraction outside staging.
            if (((entry.ExternalAttributes >> 16) & 0xF000) == 0xA000)
                throw new InvalidDataException("Symlink entry is not allowed.");
            if (entry.Length < 0 || entry.Length > MaximumExpandedBytes ||
                total > MaximumExpandedBytes - entry.Length)
                throw new InvalidDataException("x64 ZIP expanded size limit exceeded.");
            total += entry.Length;
        }

        // Archive root is the executable directory, not the Actions artifact
        // wrapper. Never accept an x86 EXE with a renamed x64 ZIP.
        var exe = archive.GetEntry("Dvinholm.exe");
        if (exe is null || exe.Length < 512 || !IsAmd64Pe(exe))
            throw new InvalidDataException("x64 ZIP does not contain an AMD64 Dvinholm.exe.");
        foreach (var component in new[] { "coreclr.dll", "hostfxr.dll", "Microsoft.UI.Xaml.dll" })
        {
            if (!names.Any(name => name.Equals(component, StringComparison.OrdinalIgnoreCase) ||
                name.EndsWith("/" + component, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidDataException("Missing self-contained component: " + component);
        }
        if (names.Any(name => name.Equals("Launcher.exe", StringComparison.OrdinalIgnoreCase) ||
             name.EndsWith("/Launcher.exe", StringComparison.OrdinalIgnoreCase) ||
             name.EndsWith("/Dvinholm.Wpf.exe", StringComparison.OrdinalIgnoreCase)))
            throw new InvalidDataException("Unexpected second launcher in unified app.");

        return new UnifiedX64VerifiedArchive(Convert.ToHexString(actualHash).ToLowerInvariant(),
            archive.Entries.Count, total);
    }

    internal static byte[] GetExpectedDigest(string manifest)
    {
        if (manifest.Length is 0 or > MaximumManifestChars)
            throw new InvalidDataException("Invalid checksum manifest length.");
        byte[]? digest = null;
        foreach (var line in manifest.Split('\n'))
        {
            var record = line.TrimEnd('\r');
            if (string.IsNullOrEmpty(record))
                continue;
            if (record.Length > 600)
                throw new InvalidDataException("Checksum record is too long.");
            var match = Regex.Match(record, @"^([0-9a-fA-F]{64})  ([A-Za-z0-9._-]+)$",
                RegexOptions.CultureInvariant);
            if (!match.Success)
                throw new InvalidDataException("Malformed checksum record.");
            if (!string.Equals(match.Groups[2].Value,
                UnifiedX64ReleaseFeed.PackageAsset, StringComparison.Ordinal))
                continue;
            if (digest is not null)
                throw new InvalidDataException("Duplicate x64 checksum record.");
            digest = Convert.FromHexString(match.Groups[1].Value);
        }
        return digest ?? throw new InvalidDataException("Missing x64 ZIP SHA-256 record.");
    }

    internal static bool ValidEntryName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 1024 ||
            name[0] == '/' || name.Contains('\\') || name.Contains(':') ||
            name.Any(char.IsControl))
            return false;
        var segments = name.Split('/');
        for (var i = 0; i < segments.Length; i++)
        {
            var part = segments[i];
            // Exactly one empty trailing segment is allowed for directories.
            if (part.Length == 0 && i == segments.Length - 1 && name.EndsWith('/'))
                continue;
            if (part.Length == 0 || part is "." or ".." ||
                part.EndsWith('.') || part.EndsWith(' '))
                return false;
        }
        return true;
    }

    private static bool IsAmd64Pe(ZipArchiveEntry entry)
    {
        Span<byte> headers = stackalloc byte[4096];
        using var stream = entry.Open();
        var offset = 0;
        while (offset < headers.Length)
        {
            var n = stream.Read(headers[offset..]);
            if (n == 0) break;
            offset += n;
        }
        if (offset < 512 || headers[0] != 'M' || headers[1] != 'Z')
            return false;
        var pe = System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(headers[0x3C..]);
        return pe >= 64 && pe + 6 <= offset &&
            headers[pe] == 'P' && headers[pe + 1] == 'E' &&
            headers[pe + 2] == 0 && headers[pe + 3] == 0 &&
            System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(
                headers.Slice(pe + 4, 2)) == 0x8664;
    }
}
