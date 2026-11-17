using System.Security.Cryptography;

namespace Dvinholm.App;

/// <summary>
/// Strictly offline bridge between verified ZIP extraction, immutable backup
/// snapshot and the fault-injection recovery journal. ONLY named + marked
/// temporary fixtures are accepted; this has no production update entrypoint.
/// </summary>
internal static class UnifiedX64FixtureTransaction
{
    internal static void Prepare(
        string extractionRoot, UnifiedX64StagedPackage staged,
        UnifiedX64ExtractionSandbox.Receipt expanded,
        string recoveryRoot, UnifiedX64BackupSnapshot.Receipt backup)
    {
        ArgumentNullException.ThrowIfNull(staged);
        ArgumentNullException.ThrowIfNull(expanded);
        ArgumentNullException.ThrowIfNull(backup);
        UnifiedX64RecoverySandbox.ValidateFixtureRootForTests(recoveryRoot);
        if (!UnifiedX64ExtractionSandbox.VerifyFixture(expanded, staged, extractionRoot))
            throw new InvalidDataException("Extracted x64 fixture cannot be verified.");

        var root = Path.GetFullPath(recoveryRoot);
        var installed = Path.Combine(root, "installed");
        var backups = Path.Combine(root, "snapshots");
        var backupPath = Path.GetFullPath(backup.Directory);
        if (!string.Equals(Path.GetDirectoryName(backupPath), backups,
                StringComparison.OrdinalIgnoreCase) ||
            !Path.GetFileName(backupPath).StartsWith("backup-", StringComparison.Ordinal) ||
            UnifiedX64BackupSnapshot.Verify(backupPath) != backup ||
            !UnifiedX64BackupSnapshot.MatchesInstalledContents(backupPath, installed))
            throw new InvalidDataException("Untrusted or stale fixture backup.");

        var candidate = Path.Combine(root, "candidate");
        // Never overwrite an existing candidate or transaction. A prepared
        // journal is immutable evidence until a test finishes/rolls back.
        if (Directory.Exists(candidate) || File.Exists(candidate) ||
            File.Exists(Path.Combine(root, "fixture-transaction.json")))
            throw new InvalidDataException("Fixture candidate or journal already exists.");

        var source = Path.GetFullPath(expanded.Directory);
        var bytes = 0L;
        var count = 0;
        Directory.CreateDirectory(candidate);
        try
        {
            foreach (var file in Directory.EnumerateFiles(source, "*",
                         SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(source, file).Replace('\\', '/');
                if (!UnifiedX64PackageVerifier.ValidEntryName(relative) ||
                    (File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidDataException("Unsafe expanded fixture file.");

                var target = Path.Combine(candidate,
                    relative.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                using (var input = new FileStream(file, FileMode.Open,
                    FileAccess.Read, FileShare.Read))
                using (var output = new FileStream(target, FileMode.CreateNew,
                    FileAccess.Write, FileShare.None, 64 * 1024, FileOptions.WriteThrough))
                {
                    input.CopyTo(output);
                    output.Flush(flushToDisk: true);
                }
                if (++count > expanded.Files ||
                    bytes > expanded.Bytes - new FileInfo(target).Length ||
                    !MatchesHash(file, target))
                    throw new InvalidDataException("Candidate copy failed integrity audit.");
                bytes += new FileInfo(target).Length;
            }

            if (count != expanded.Files || bytes != expanded.Bytes ||
                !UnifiedX64ExtractionSandbox.VerifyFixture(expanded, staged, extractionRoot))
                throw new InvalidDataException("Extracted payload changed during transfer.");
            UnifiedX64RecoverySandbox.Prepare(root, backup);
            if (UnifiedX64RecoverySandbox.ReadState(root) !=
                UnifiedX64RecoverySandbox.StateReady)
                throw new InvalidDataException("Prepared transaction journal is invalid.");
        }
        catch
        {
            // Only clean the candidate newly created by this invocation.
            // The installed fixture and completed backup are never removed.
            if (Directory.Exists(candidate))
            {
                try { Directory.Delete(candidate, recursive: true); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
            throw;
        }
    }

    private static bool MatchesHash(string source, string copied)
    {
        using var input = new FileStream(source, FileMode.Open,
            FileAccess.Read, FileShare.Read);
        using var output = new FileStream(copied, FileMode.Open,
            FileAccess.Read, FileShare.Read);
        return CryptographicOperations.FixedTimeEquals(
            SHA256.HashData(input), SHA256.HashData(output));
    }
}
