using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace Dvinholm.App;

/// <summary>
/// Read-only dry run for a future Windows-x64 backup/rollback transaction.
/// Installation is not implemented; all on-disk discovery is bounded and
/// does not copy, remove or replace live files.
/// </summary>
internal sealed record UnifiedX64RecoveryPlan(
    string InstallDirectory,
    string StagingDirectory,
    string BackupDirectory,
    int FilesToBackUp,
    long BytesToBackUp,
    IReadOnlyList<string> Blockers)
{
    internal bool CanPlanBackup => Blockers.Count == 0;
    // The release signing root, atomic handoff and restart/rollback helper
    // have not passed acceptance. A valid inventory is NOT install approval.
    internal bool CanInstall => false;
}

internal static class UnifiedX64RecoveryPreflight
{
    internal const int MaximumFiles = 10_000;
    internal const long MaximumBackupBytes = 1024L * 1024 * 1024;

    internal static UnifiedX64RecoveryPlan Inspect(
        string installDirectory,
        UnifiedX64StagedPackage staged,
        string backupRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(installDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(backupRoot);
        ArgumentNullException.ThrowIfNull(staged);

        var install = Path.GetFullPath(installDirectory);
        var stage = Path.GetFullPath(staged.Directory);
        var backup = Path.GetFullPath(backupRoot);
        var blockers = new List<string>();
        var inventoryCount = 0;
        long inventoryBytes = 0;

        if (!Directory.Exists(install) || IsRoot(install) ||
            HasReparsePointInAncestors(install))
            blockers.Add("INSTALL_PATH_UNSAFE");
        if (IsRoot(backup) || HasReparsePointInAncestors(backup) ||
            Overlap(backup, install) || Overlap(backup, stage))
            blockers.Add("BACKUP_PATH_UNSAFE");
        if (!Directory.Exists(stage) || IsRoot(stage) ||
            HasReparsePointInAncestors(stage) ||
            Overlap(install, stage) ||
            !Regex.IsMatch(Path.GetFileName(Path.TrimEndingDirectorySeparator(stage)),
                @"^pending-[a-f0-9]{32}$", RegexOptions.CultureInvariant) ||
            !string.Equals(Path.GetFullPath(staged.ZipPath),
                Path.Combine(stage, UnifiedX64ReleaseFeed.PackageAsset),
                StringComparison.OrdinalIgnoreCase))
            blockers.Add("STAGING_PATH_UNSAFE");

        if (!blockers.Contains("STAGING_PATH_UNSAFE"))
        {
            try
            {
                var zipFile = new FileInfo(staged.ZipPath);
                if (!zipFile.Exists || (zipFile.Attributes & FileAttributes.ReparsePoint) != 0 ||
                    zipFile.Length != staged.ZipBytes || staged.ZipBytes <= 0 ||
                    staged.Sha256.Length != 64 ||
                    !Regex.IsMatch(staged.Sha256, @"^[0-9a-f]{64}$") ||
                    !string.Equals(
                        Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(staged.ZipPath))),
                        staged.Sha256, StringComparison.OrdinalIgnoreCase))
                    blockers.Add("STAGING_EVIDENCE_MISMATCH");
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                blockers.Add("STAGING_UNREADABLE");
            }
        }

        if (!blockers.Contains("INSTALL_PATH_UNSAFE"))
        {
            var exe = Path.Combine(install, "Dvinholm.exe");
            if (!File.Exists(exe) || !HasAmd64Header(exe))
                blockers.Add("INSTALLED_EXE_NOT_AMD64");

            var pending = new Stack<string>();
            pending.Push(install);
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            while (pending.Count > 0)
            {
                var directory = pending.Pop();
                try
                {
                    foreach (var entry in Directory.EnumerateFileSystemEntries(directory))
                    {
                        var info = File.GetAttributes(entry);
                        var relative = Path.GetRelativePath(install, entry).Replace('\\', '/');
                        if (!UnifiedX64PackageVerifier.ValidEntryName(relative) ||
                            !seen.Add(relative) || (info & FileAttributes.ReparsePoint) != 0)
                        {
                            blockers.Add("UNSAFE_INSTALLED_ENTRY");
                            break;
                        }
                        if ((info & FileAttributes.Directory) != 0)
                        {
                            pending.Push(entry);
                            continue;
                        }
                        inventoryCount++;
                        var length = new FileInfo(entry).Length;
                        if (inventoryCount > MaximumFiles || length > MaximumBackupBytes ||
                            inventoryBytes > MaximumBackupBytes - length)
                        {
                            blockers.Add("BACKUP_LIMIT_EXCEEDED");
                            break;
                        }
                        inventoryBytes += length;
                    }
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    blockers.Add("INSTALL_UNREADABLE");
                }
                if (blockers.Count != 0) break;
            }
            if (inventoryCount == 0)
                blockers.Add("INSTALL_HAS_NO_FILES");
        }

        // A randomized backup name is a *proposal* only. No copy, delete,
        // extraction or modification is performed by this method.
        var backupPath = Path.Combine(backup,
            "backup-" + Guid.NewGuid().ToString("N"));
        return new UnifiedX64RecoveryPlan(
            install, stage, backupPath, inventoryCount, inventoryBytes,
            blockers.Distinct(StringComparer.Ordinal).ToArray());
    }

    internal static bool Overlap(string first, string second)
    {
        var a = Path.TrimEndingDirectorySeparator(Path.GetFullPath(first));
        var b = Path.TrimEndingDirectorySeparator(Path.GetFullPath(second));
        return a.Equals(b, StringComparison.OrdinalIgnoreCase) ||
            a.StartsWith(b + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
            b.StartsWith(a + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsRoot(string path) =>
        Path.TrimEndingDirectorySeparator(path).Equals(
            Path.TrimEndingDirectorySeparator(Path.GetPathRoot(path)!),
            StringComparison.OrdinalIgnoreCase);

    private static bool HasReparsePointInAncestors(string path)
    {
        for (DirectoryInfo? current = new(Path.GetFullPath(path));
            current is not null; current = current.Parent)
        {
            if (current.Exists &&
                (current.Attributes & FileAttributes.ReparsePoint) != 0)
                return true;
        }
        return false;
    }

    private static bool HasAmd64Header(string path)
    {
        try
        {
            using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            Span<byte> header = stackalloc byte[1024];
            var read = file.Read(header);
            if (read < 512 || header[0] != 'M' || header[1] != 'Z')
                return false;
            var offset = BinaryPrimitives.ReadInt32LittleEndian(header[0x3C..]);
            return offset >= 64 && offset + 6 <= read &&
                header[offset] == 'P' && header[offset + 1] == 'E' &&
                header[offset + 2] == 0 && header[offset + 3] == 0 &&
                BinaryPrimitives.ReadUInt16LittleEndian(header.Slice(offset + 4, 2)) == 0x8664;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
