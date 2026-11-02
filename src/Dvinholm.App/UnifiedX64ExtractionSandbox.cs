using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace Dvinholm.App;

/// <summary>
/// Experimental offline extraction into a deliberately marked temp fixture.
/// NO production installer, WinUI hook, file replacement, restart or recovery
/// authorization is provided by this class.
/// </summary>
internal static class UnifiedX64ExtractionSandbox
{
    private const string Marker = ".dvinholm-offline-extract";
    private const string MarkerValue = "Dvinholm extraction fixture v1";
    private static readonly Regex FixtureName = new(
        @"^dvinholm-extract-fixture-[a-f0-9]{32}$", RegexOptions.CultureInvariant);
    private static readonly Regex PendingName = new(
        @"^pending-[a-f0-9]{32}$", RegexOptions.CultureInvariant);
    private static readonly Regex ExpandedName = new(
        @"^expanded-[a-f0-9]{32}$", RegexOptions.CultureInvariant);

    internal sealed record Receipt(string Directory, int Files, long Bytes, string ZipSha256);

    internal static void MarkForTests(string root)
    {
        var path = Path.GetFullPath(root);
        var tmp = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath()));
        if (!string.Equals(Path.GetDirectoryName(path), tmp, StringComparison.OrdinalIgnoreCase) ||
            !FixtureName.IsMatch(Path.GetFileName(path)) || Directory.Exists(path))
            throw new InvalidOperationException("Only a fresh direct temp fixture is permitted.");
        Directory.CreateDirectory(path);
        File.WriteAllText(Path.Combine(path, Marker), MarkerValue);
    }

    internal static Receipt ExtractFixture(
        UnifiedX64StagedPackage staged, string fixtureRoot,
        CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(staged);
        var root = RequireFixture(fixtureRoot);
        AssertStaged(staged, root);
        VerifyZip(staged);

        var nonce = Guid.NewGuid().ToString("N");
        var scratch = Path.Combine(root, ".extract-" + nonce);
        var completed = Path.Combine(root, "expanded-" + nonce);
        Directory.CreateDirectory(scratch);
        try
        {
            int files = 0;
            long total = 0;
            using var archive = ZipFile.OpenRead(staged.ZipPath);
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in archive.Entries)
            {
                token.ThrowIfCancellationRequested();
                if (!UnifiedX64PackageVerifier.ValidEntryName(entry.FullName) ||
                    !SafeWindowsName(entry.FullName) ||
                    !seen.Add(entry.FullName) ||
                    ((entry.ExternalAttributes >> 16) & 0xF000) == 0xA000)
                    throw new InvalidDataException("Unsafe x64 payload entry.");

                var path = Path.Combine(scratch,
                    entry.FullName.Replace('/', Path.DirectorySeparatorChar));
                if (entry.FullName.EndsWith('/'))
                {
                    Directory.CreateDirectory(path);
                    continue;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                using var input = entry.Open();
                using var output = new FileStream(path, FileMode.CreateNew,
                    FileAccess.Write, FileShare.None, 64 * 1024, FileOptions.WriteThrough);
                var buffer = new byte[64 * 1024];
                long size = 0;
                int n;
                while ((n = input.Read(buffer)) != 0)
                {
                    token.ThrowIfCancellationRequested();
                    if (size > entry.Length - n ||
                        total > UnifiedX64PackageVerifier.MaximumExpandedBytes - n)
                        throw new InvalidDataException("Decompressed entry exceeded declared bounds.");
                    output.Write(buffer, 0, n);
                    size += n;
                    total += n;
                }
                output.Flush(flushToDisk: true);
                if (size != entry.Length ||
                    ++files > UnifiedX64PackageVerifier.MaximumEntries)
                    throw new InvalidDataException("Incomplete decompression.");
            }
            if (archive.Entries.Count != staged.EntryCount ||
                total != staged.ExpandedBytes)
                throw new InvalidDataException("Expanded package inventory mismatch.");

            // Recheck ZIP after extraction to detect changed compressed bytes.
            VerifyZip(staged);
            VerifyFiles(scratch, staged, files, total, token);
            Directory.Move(scratch, completed);
            return new Receipt(completed, files, total, staged.Sha256);
        }
        catch
        {
            if (Directory.Exists(scratch))
                Directory.Delete(scratch, recursive: true);
            throw;
        }
    }

    internal static bool VerifyFixture(
        Receipt receipt, UnifiedX64StagedPackage staged, string fixtureRoot)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        var root = RequireFixture(fixtureRoot);
        AssertStaged(staged, root);
        var path = Path.GetFullPath(receipt.Directory);
        if (!ExpandedName.IsMatch(Path.GetFileName(path)) ||
            !string.Equals(Path.GetDirectoryName(path), root, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(receipt.ZipSha256, staged.Sha256, StringComparison.Ordinal))
            throw new InvalidDataException("Untrusted extracted folder receipt.");
        VerifyZip(staged);
        VerifyFiles(path, staged, receipt.Files, receipt.Bytes, CancellationToken.None);
        return true;
    }

    private static void VerifyFiles(string folder, UnifiedX64StagedPackage staged,
        int expectedFiles, long expectedBytes, CancellationToken token)
    {
        if (!Directory.Exists(folder) ||
            (File.GetAttributes(folder) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidDataException("Missing or reparse-point extracted folder.");

        var expected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        long total = 0;
        using var zip = ZipFile.OpenRead(staged.ZipPath);
        foreach (var entry in zip.Entries.Where(e => !e.FullName.EndsWith('/')))
        {
            token.ThrowIfCancellationRequested();
            expected.Add(entry.FullName);
            var destination = Path.Combine(folder,
                entry.FullName.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(destination) ||
                (File.GetAttributes(destination) & FileAttributes.ReparsePoint) != 0 ||
                new FileInfo(destination).Length != entry.Length)
                throw new InvalidDataException("Extracted file missing or unsafe.");
            using var fromArchive = entry.Open();
            using var fromDisk = new FileStream(destination, FileMode.Open,
                FileAccess.Read, FileShare.Read);
            var archiveHash = SHA256.HashData(fromArchive);
            var diskHash = SHA256.HashData(fromDisk);
            if (!CryptographicOperations.FixedTimeEquals(archiveHash, diskHash))
                throw new InvalidDataException("Extracted file was modified.");
            if (total > UnifiedX64PackageVerifier.MaximumExpandedBytes - entry.Length)
                throw new InvalidDataException("Extracted bytes exceeded limit.");
            total += entry.Length;
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var stack = new Stack<string>();
        stack.Push(folder);
        while (stack.Count > 0)
        {
            foreach (var item in Directory.EnumerateFileSystemEntries(stack.Pop()))
            {
                var attrs = File.GetAttributes(item);
                if ((attrs & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidDataException("Unsafe extraction link.");
                if ((attrs & FileAttributes.Directory) != 0) stack.Push(item);
                else if (!seen.Add(Path.GetRelativePath(folder, item).Replace('\\', '/')))
                    throw new InvalidDataException("Duplicate extracted file.");
            }
        }
        if (!seen.SetEquals(expected) || seen.Count != expectedFiles ||
            total != expectedBytes)
            throw new InvalidDataException("Unexpected extracted package inventory.");
    }

    private static bool SafeWindowsName(string name) =>
        name.Split('/', StringSplitOptions.RemoveEmptyEntries).All(part =>
            !Regex.IsMatch(part,
                @"^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])(?:\.|$)",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant));

    private static void AssertStaged(UnifiedX64StagedPackage staged, string root)
    {
        var dir = Path.GetFullPath(staged.Directory);
        if (!PendingName.IsMatch(Path.GetFileName(dir)) ||
            !string.Equals(Path.GetDirectoryName(dir), root, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(Path.GetFullPath(staged.ZipPath),
                Path.Combine(dir, UnifiedX64ReleaseFeed.PackageAsset),
                StringComparison.OrdinalIgnoreCase) ||
            !File.Exists(staged.ZipPath) ||
            (File.GetAttributes(staged.ZipPath) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidDataException("Untrusted staged ZIP path.");
        for (DirectoryInfo? parent = new(dir); parent is not null; parent = parent.Parent)
            if (parent.Exists && (parent.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException("Linked staged ZIP directory.");
    }

    private static void VerifyZip(UnifiedX64StagedPackage staged)
    {
        var manifest = staged.Sha256 + "  " + UnifiedX64ReleaseFeed.PackageAsset + "\n";
        var verified = UnifiedX64PackageVerifier.Verify(
            staged.ZipPath, manifest, staged.ZipBytes);
        if (!string.Equals(verified.Sha256, staged.Sha256, StringComparison.Ordinal) ||
            verified.EntryCount != staged.EntryCount ||
            verified.ExpandedBytes != staged.ExpandedBytes)
            throw new InvalidDataException("Staged ZIP verification changed.");
    }

    private static string RequireFixture(string rootPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(rootPath));
        var temp = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath()));
        if (!string.Equals(Path.GetDirectoryName(root), temp, StringComparison.OrdinalIgnoreCase) ||
            !FixtureName.IsMatch(Path.GetFileName(root)))
            throw new InvalidOperationException("Only offline extraction fixtures are permitted.");
        for (DirectoryInfo? parent = new(root); parent is not null; parent = parent.Parent)
            if (!parent.Exists || (parent.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new InvalidOperationException("Missing or linked fixture ancestor.");
        var marker = Path.Combine(root, Marker);
        if (!File.Exists(marker) ||
            (File.GetAttributes(marker) & FileAttributes.ReparsePoint) != 0 ||
            File.ReadAllText(marker) != MarkerValue)
            throw new InvalidOperationException("Offline fixture marker missing.");
        return root;
    }
}
