using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Dvinholm.App;

/// <summary>
/// Offline snapshot preparation ONLY. Creates a copy of installed files under a
/// separate backup root, verifies every byte, and writes a non-installable
/// journal. It never changes the installed application or performs rollback.
/// </summary>
internal static class UnifiedX64BackupSnapshot
{
    private const string JournalFile = "snapshot-journal.json";
    private const string PayloadFolder = "payload";
    private const int JournalLimit = 2 * 1024 * 1024;

    internal sealed record FileEntry(string RelativePath, long Bytes, string Sha256);
    internal sealed record Journal(int Schema, string State, FileEntry[] Files);
    internal sealed record Receipt(string Directory, string JournalSha256, int Files, long Bytes);

    internal static Receipt Create(UnifiedX64RecoveryPlan plan, UnifiedX64StagedPackage staged)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(staged);
        // Recheck the live installation and staged bytes immediately before
        // reading source files; old preflight observations may be stale.
        var root = Path.GetDirectoryName(Path.GetFullPath(plan.BackupDirectory))
            ?? throw new InvalidDataException("Backup root missing.");
        var target = Path.GetFullPath(plan.BackupDirectory);
        var leaf = Path.GetFileName(target);
        if (!plan.CanPlanBackup || !Regex.IsMatch(leaf,
                @"^backup-[a-f0-9]{32}$", RegexOptions.CultureInvariant) ||
            UnifiedX64RecoveryPreflight.Overlap(root, plan.InstallDirectory) ||
            UnifiedX64RecoveryPreflight.Overlap(root, staged.Directory) ||
            Directory.Exists(target) || File.Exists(target))
            throw new InvalidDataException("Snapshot plan is unsafe or already used.");

        var fresh = UnifiedX64RecoveryPreflight.Inspect(
            plan.InstallDirectory, staged, root);
        if (!fresh.CanPlanBackup ||
            fresh.FilesToBackUp != plan.FilesToBackUp ||
            fresh.BytesToBackUp != plan.BytesToBackUp ||
            !string.Equals(fresh.InstallDirectory, plan.InstallDirectory,
                StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(fresh.StagingDirectory, plan.StagingDirectory,
                StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Snapshot preflight changed.");

        Directory.CreateDirectory(root);
        // Exclusive, randomized temporary directory is the only workspace the
        // routine may remove on failure. Never delete an existing backup.
        var temporary = Path.Combine(root, ".snapshot-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temporary);
        try
        {
            var payload = Path.Combine(temporary, PayloadFolder);
            Directory.CreateDirectory(payload);
            var entries = new List<FileEntry>();
            long bytes = 0;
            foreach (var (name, source) in EnumerateSafeFiles(plan.InstallDirectory))
            {
                if (entries.Count >= UnifiedX64RecoveryPreflight.MaximumFiles)
                    throw new InvalidDataException("Too many files in snapshot.");

                var destination = Path.Combine(payload, name.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);

                // Files may be in use: refuse to capture a moving target rather
                // than inventing a consistent rollback point.
                using (var input = new FileStream(source, FileMode.Open, FileAccess.Read,
                    FileShare.Read, 64 * 1024, FileOptions.SequentialScan))
                using (var output = new FileStream(destination, FileMode.CreateNew,
                    FileAccess.Write, FileShare.None, 64 * 1024, FileOptions.WriteThrough))
                {
                    input.CopyTo(output);
                    output.Flush(flushToDisk: true);
                }
                var fileLength = new FileInfo(destination).Length;
                if (fileLength > UnifiedX64RecoveryPreflight.MaximumBackupBytes - bytes)
                    throw new InvalidDataException("Snapshot capacity exceeded.");
                bytes += fileLength;
                var digest = HashFile(destination);
                if (!string.Equals(HashFile(source), digest, StringComparison.Ordinal))
                    throw new InvalidDataException("Source changed while backup was captured.");
                entries.Add(new FileEntry(name, fileLength, digest));
            }
            if (entries.Count != plan.FilesToBackUp || bytes != plan.BytesToBackUp)
                throw new InvalidDataException("Source inventory changed during backup.");

            var journal = new Journal(1, "BackupVerified_NoInstall", entries.ToArray());
            var journalPath = Path.Combine(temporary, JournalFile);
            var json = JsonSerializer.SerializeToUtf8Bytes(journal);
            if (json.Length > JournalLimit)
                throw new InvalidDataException("Snapshot journal is too large.");
            using (var stream = new FileStream(journalPath, FileMode.CreateNew,
                FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            {
                stream.Write(json);
                stream.Flush(flushToDisk: true);
            }
            // Validate the pending copy before the atomic name transition.
            Verify(temporary);
            Directory.Move(temporary, target);
            return Verify(target);
        }
        catch
        {
            // Only a directory created by this call can be cleaned up.
            if (Directory.Exists(temporary))
            {
                try { Directory.Delete(temporary, recursive: true); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
            throw;
        }
    }

    /// <summary>
    /// Verify a backup snapshot without modifying the installed program.
    /// A matching journal is local integrity evidence, NOT a signed release
    /// or permission to restore/replace executable files.
    /// </summary>
    internal static Receipt Verify(string snapshotDirectory)
    {
        var path = Path.GetFullPath(snapshotDirectory);
        var attributes = File.GetAttributes(path);
        if ((attributes & FileAttributes.ReparsePoint) != 0 ||
            (attributes & FileAttributes.Directory) == 0)
            throw new InvalidDataException("Unsafe snapshot folder.");

        // Reject untracked root files and directory junctions before reading
        // journal data. The root must contain only payload/ and the journal.
        var rootEntries = Directory.EnumerateFileSystemEntries(path).ToArray();
        if (rootEntries.Length != 2 ||
            rootEntries.Any(entry =>
                (File.GetAttributes(entry) & FileAttributes.ReparsePoint) != 0 ||
                (Path.GetFileName(entry) != JournalFile &&
                 Path.GetFileName(entry) != PayloadFolder)))
            throw new InvalidDataException("Unexpected backup snapshot root entries.");
        var journalPath = Path.Combine(path, JournalFile);
        var journalInfo = new FileInfo(journalPath);
        if (!journalInfo.Exists || journalInfo.Length is 0 or > JournalLimit)
            throw new InvalidDataException("Invalid snapshot journal length.");
        var bytes = File.ReadAllBytes(journalPath);
        var journal = JsonSerializer.Deserialize<Journal>(bytes)
            ?? throw new InvalidDataException("Invalid snapshot journal.");
        if (journal.Schema != 1 || journal.State != "BackupVerified_NoInstall" ||
            journal.Files is null || journal.Files.Length is 0 or > UnifiedX64RecoveryPreflight.MaximumFiles)
            throw new InvalidDataException("Invalid backup journal state.");

        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        long total = 0;
        foreach (var record in journal.Files)
        {
            if (!UnifiedX64PackageVerifier.ValidEntryName(record.RelativePath) ||
                !names.Add(record.RelativePath) ||
                record.Bytes < 0 ||
                !Regex.IsMatch(record.Sha256, @"^[a-f0-9]{64}$", RegexOptions.CultureInvariant))
                throw new InvalidDataException("Unsafe backup record.");
            var file = Path.Combine(path, PayloadFolder,
                record.RelativePath.Replace('/', Path.DirectorySeparatorChar));
            var info = new FileInfo(file);
            if (!info.Exists || (info.Attributes & FileAttributes.ReparsePoint) != 0 ||
                info.Length != record.Bytes ||
                !string.Equals(HashFile(file), record.Sha256, StringComparison.Ordinal))
                throw new InvalidDataException("Backup file failed SHA-256 verification.");
            if (record.Bytes > UnifiedX64RecoveryPreflight.MaximumBackupBytes - total)
                throw new InvalidDataException("Backup size limit exceeded.");
            total += record.Bytes;
        }
        var actual = EnumerateSafeFiles(Path.Combine(path, PayloadFolder)).Select(e => e.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!actual.SetEquals(names))
            throw new InvalidDataException("Unexpected backup file inventory.");

        return new Receipt(path,
            Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(),
            names.Count, total);
    }

    private static IEnumerable<(string Name, string FullPath)> EnumerateSafeFiles(string root)
    {
        var pending = new Stack<string>();
        pending.Push(Path.GetFullPath(root));
        while (pending.Count > 0)
        {
            var directory = pending.Pop();
            foreach (var entry in Directory.EnumerateFileSystemEntries(directory))
            {
                var attrs = File.GetAttributes(entry);
                var relative = Path.GetRelativePath(root, entry).Replace('\\', '/');
                if (!UnifiedX64PackageVerifier.ValidEntryName(relative) ||
                    (attrs & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidDataException("Unsafe snapshot filesystem entry.");
                if ((attrs & FileAttributes.Directory) != 0)
                    pending.Push(entry);
                else
                    yield return (relative, entry);
            }
        }
    }

    private static string HashFile(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read,
            FileShare.Read, 64 * 1024, FileOptions.SequentialScan);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }
}
