using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Dvinholm.App;

/// <summary>
/// FAULT-INJECTION FIXTURE ONLY. Proves durable journal transitions and
/// idempotent rollback against a temporary, explicitly marked scratch tree.
/// This class must never be called for the real installation; there is no
/// production Apply/Restore API and no updater GUI connection.
/// </summary>
internal static class UnifiedX64RecoverySandbox
{
    private const string Marker = ".dvinholm-offline-fixture";
    private const string MarkerValue = "Dvinholm recovery simulation v1";
    private const string JournalName = "fixture-transaction.json";
    private const int MaxJournalBytes = 2 * 1024 * 1024;
    internal const string StateReady = "BackupVerified";
    internal const string StateApplying = "Applying";
    internal const string StateRecovering = "Recovering";
    internal const string StateApplied = "AppliedFixtureOnly";
    internal const string StateRolledBack = "RolledBack";

    internal sealed record Entry(string RelativePath, string OriginalSha256, string CandidateSha256);
    internal sealed record Journal(int Schema, string State, string SnapshotName,
        string SnapshotJournalSha256, Entry[] Files);

    internal static void Prepare(string fixtureRoot, UnifiedX64BackupSnapshot.Receipt receipt)
    {
        var root = RequireScratch(fixtureRoot);
        ArgumentNullException.ThrowIfNull(receipt);
        var journalPath = Path.Combine(root, JournalName);
        if (File.Exists(journalPath))
            throw new InvalidDataException("Recovery journal already exists.");
        var snapshot = SnapshotPath(root, Path.GetFileName(receipt.Directory));
        var verified = UnifiedX64BackupSnapshot.Verify(snapshot);
        if (!string.Equals(receipt.Directory, verified.Directory, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(receipt.JournalSha256, verified.JournalSha256, StringComparison.Ordinal))
            throw new InvalidDataException("Backup receipt mismatch.");

        var originals = Inventory(Path.Combine(snapshot, "payload"));
        var current = Inventory(Path.Combine(root, "installed"));
        var incoming = Inventory(Path.Combine(root, "candidate"));
        if (originals.Count == 0 || originals.Count != incoming.Count ||
            !SameKeys(originals.Keys, current.Keys) ||
            !SameKeys(originals.Keys, incoming.Keys))
            throw new InvalidDataException("Update fixture inventory mismatch.");
        var entries = originals.Keys.OrderBy(x => x, StringComparer.Ordinal)
            .Select(name =>
            {
                if (originals[name] != current[name])
                    throw new InvalidDataException("Installed source changed since backup.");
                return new Entry(name, originals[name], incoming[name]);
            }).ToArray();
        WriteJournal(root, new Journal(1, StateReady,
            Path.GetFileName(snapshot), receipt.JournalSha256, entries));
    }

    /// <summary>
    /// Injected interruption leaves the journal in Applying state; the next
    /// recovery attempt restores the ORIGINAL verified backup, never guesses.
    /// </summary>
    internal static void ApplyFixture(string fixtureRoot, bool interruptAfterFirstFile = false)
    {
        var root = RequireScratch(fixtureRoot);
        var journal = ReadJournal(root);
        if (journal.State != StateReady)
            throw new InvalidDataException("Fixture cannot be applied from current state.");
        AssertBackup(root, journal);
        var candidate = Inventory(Path.Combine(root, "candidate"));
        if (!SameKeys(journal.Files.Select(x => x.RelativePath), candidate.Keys) ||
            journal.Files.Any(x => candidate[x.RelativePath] != x.CandidateSha256))
            throw new InvalidDataException("Candidate changed before applying fixture.");
        AssertInstalled(root, journal, useOriginalHashes: true);

        WriteJournal(root, journal with { State = StateApplying });
        var count = 0;
        foreach (var entry in journal.Files)
        {
            CopyAndFlush(
                Path.Combine(root, "candidate", Relative(entry.RelativePath)),
                Path.Combine(root, "installed", Relative(entry.RelativePath)));
            if (++count == 1 && interruptAfterFirstFile)
                throw new IOException("Injected interrupted fixture apply.");
        }
        AssertInstalled(root, journal, useOriginalHashes: false);
        WriteJournal(root, journal with { State = StateApplied });
    }

    /// <summary>
    /// Replaying recovery after another injected crash is safe: journal state
    /// remains Recovering until every original byte is verified.
    /// </summary>
    internal static void RecoverFixture(string fixtureRoot, bool interruptAfterFirstFile = false)
    {
        var root = RequireScratch(fixtureRoot);
        var journal = ReadJournal(root);
        if (journal.State == StateRolledBack)
        {
            AssertInstalled(root, journal, useOriginalHashes: true);
            return;
        }
        if (journal.State is not (StateApplying or StateRecovering))
            throw new InvalidDataException("No interrupted update to recover.");
        AssertBackup(root, journal);
        WriteJournal(root, journal with { State = StateRecovering });
        var count = 0;
        foreach (var entry in journal.Files)
        {
            CopyAndFlush(
                Path.Combine(SnapshotPath(root, journal.SnapshotName), "payload",
                    Relative(entry.RelativePath)),
                Path.Combine(root, "installed", Relative(entry.RelativePath)));
            if (++count == 1 && interruptAfterFirstFile)
                throw new IOException("Injected interrupted fixture rollback.");
        }
        AssertInstalled(root, journal, useOriginalHashes: true);
        WriteJournal(root, journal with { State = StateRolledBack });
    }

    internal static string ReadState(string fixtureRoot) =>
        ReadJournal(RequireScratch(fixtureRoot)).State;

    private static void AssertInstalled(string root, Journal journal, bool useOriginalHashes)
    {
        var current = Inventory(Path.Combine(root, "installed"));
        if (!SameKeys(journal.Files.Select(x => x.RelativePath), current.Keys))
            throw new InvalidDataException("Installed fixture inventory was modified.");
        foreach (var entry in journal.Files)
        {
            var expected = useOriginalHashes ? entry.OriginalSha256 : entry.CandidateSha256;
            if (current[entry.RelativePath] != expected)
                throw new InvalidDataException("Installed fixture hash mismatch.");
        }
    }

    private static void AssertBackup(string root, Journal journal)
    {
        var verified = UnifiedX64BackupSnapshot.Verify(
            SnapshotPath(root, journal.SnapshotName));
        if (verified.JournalSha256 != journal.SnapshotJournalSha256)
            throw new InvalidDataException("Original backup journal changed.");
        var snapshot = Inventory(Path.Combine(verified.Directory, "payload"));
        if (!SameKeys(journal.Files.Select(x => x.RelativePath), snapshot.Keys) ||
            journal.Files.Any(x => snapshot[x.RelativePath] != x.OriginalSha256))
            throw new InvalidDataException("Original backup was modified.");
    }

    private static string SnapshotPath(string root, string name)
    {
        if (!Regex.IsMatch(name, @"^backup-[a-f0-9]{32}$", RegexOptions.CultureInvariant))
            throw new InvalidDataException("Unsafe snapshot identifier.");
        return Path.Combine(root, "snapshots", name);
    }

    private static Dictionary<string, string> Inventory(string directory)
    {
        if (!Directory.Exists(directory) ||
            (File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidDataException("Missing or linked fixture directory.");
        var found = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var pending = new Stack<string>();
        pending.Push(directory);
        while (pending.Count > 0)
        {
            var current = pending.Pop();
            foreach (var file in Directory.EnumerateFileSystemEntries(current))
            {
                var attrs = File.GetAttributes(file);
                var name = Path.GetRelativePath(directory, file).Replace('\\', '/');
                if ((attrs & FileAttributes.ReparsePoint) != 0 ||
                    !UnifiedX64PackageVerifier.ValidEntryName(name))
                    throw new InvalidDataException("Unsafe fixture entry.");
                if ((attrs & FileAttributes.Directory) != 0)
                    pending.Push(file);
                else
                {
                    if (found.Count >= UnifiedX64RecoveryPreflight.MaximumFiles ||
                        !found.TryAdd(name, Hash(file)))
                        throw new InvalidDataException("Duplicate or excessive fixture files.");
                }
            }
        }
        return found;
    }

    private static bool SameKeys(IEnumerable<string> first, IEnumerable<string> second) =>
        first.ToHashSet(StringComparer.OrdinalIgnoreCase)
            .SetEquals(second);

    private static string RequireScratch(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        var temp = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath()));
        var name = Path.GetFileName(root);
        if (!string.Equals(Path.GetDirectoryName(root), temp, StringComparison.OrdinalIgnoreCase) ||
            !Regex.IsMatch(name, @"^dvinholm-recovery-fixture-[a-f0-9]{32}$",
                RegexOptions.CultureInvariant))
            throw new InvalidOperationException("Only a dedicated temp fixture is writable.");
        for (DirectoryInfo? current = new(root); current is not null; current = current.Parent)
            if (current.Exists && (current.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException("Reparse-point fixture is forbidden.");
        if (!File.Exists(Path.Combine(root, Marker)) ||
            File.ReadAllText(Path.Combine(root, Marker)) != MarkerValue)
            throw new InvalidOperationException("Offline fixture marker missing.");
        return root;
    }

    internal static void MarkFixtureForTests(string fixtureRoot)
    {
        // Creates only the isolated marker, never an installation directory.
        var path = Path.TrimEndingDirectorySeparator(Path.GetFullPath(fixtureRoot));
        var temp = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath()));
        if (!string.Equals(Path.GetDirectoryName(path), temp, StringComparison.OrdinalIgnoreCase) ||
            !Regex.IsMatch(Path.GetFileName(path),
                @"^dvinholm-recovery-fixture-[a-f0-9]{32}$"))
            throw new InvalidOperationException("Invalid fixture name.");
        Directory.CreateDirectory(path);
        File.WriteAllText(Path.Combine(path, Marker), MarkerValue);
    }

    private static string Relative(string name) =>
        name.Replace('/', Path.DirectorySeparatorChar);

    private static string Hash(string path)
    {
        using var stream = new FileStream(path, FileMode.Open,
            FileAccess.Read, FileShare.Read, 64 * 1024, FileOptions.SequentialScan);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private static void CopyAndFlush(string source, string target)
    {
        if ((File.GetAttributes(source) & FileAttributes.ReparsePoint) != 0 ||
            (File.GetAttributes(target) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidDataException("Linked fixture file forbidden.");
        using var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var output = new FileStream(target, FileMode.Create, FileAccess.Write,
            FileShare.None, 64 * 1024, FileOptions.WriteThrough);
        input.CopyTo(output);
        output.Flush(flushToDisk: true);
    }

    private static Journal ReadJournal(string root)
    {
        var path = Path.Combine(root, JournalName);
        var info = new FileInfo(path);
        if (!info.Exists || (info.Attributes & FileAttributes.ReparsePoint) != 0 ||
            info.Length is <= 0 or > MaxJournalBytes)
            throw new InvalidDataException("Fixture journal is missing or unsafe.");
        var journal = JsonSerializer.Deserialize<Journal>(File.ReadAllBytes(path))
            ?? throw new InvalidDataException("Fixture journal is missing.");
        if (journal.Schema != 1 || journal.Files is null ||
            journal.Files.Length is 0 or > UnifiedX64RecoveryPreflight.MaximumFiles ||
            journal.State is not (StateReady or StateApplying or StateRecovering or
                StateApplied or StateRolledBack) ||
            !Regex.IsMatch(journal.SnapshotJournalSha256, @"^[a-f0-9]{64}$") ||
            journal.Files.Any(x =>
                !UnifiedX64PackageVerifier.ValidEntryName(x.RelativePath) ||
                !Regex.IsMatch(x.OriginalSha256, @"^[a-f0-9]{64}$") ||
                !Regex.IsMatch(x.CandidateSha256, @"^[a-f0-9]{64}$")) ||
            journal.Files.Length != journal.Files.Select(x => x.RelativePath)
                .Distinct(StringComparer.OrdinalIgnoreCase).Count())
            throw new InvalidDataException("Invalid fixture transaction journal.");
        SnapshotPath(root, journal.SnapshotName);
        return journal;
    }

    private static void WriteJournal(string root, Journal journal)
    {
        var path = Path.Combine(root, JournalName);
        var tmp = Path.Combine(root, ".fixture-journal-" + Guid.NewGuid().ToString("N"));
        var json = JsonSerializer.SerializeToUtf8Bytes(journal);
        if (json.Length > MaxJournalBytes)
            throw new InvalidDataException("Fixture journal too large.");
        try
        {
            using (var stream = new FileStream(tmp, FileMode.CreateNew,
                FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            {
                stream.Write(json);
                stream.Flush(flushToDisk: true);
            }
            File.Move(tmp, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(tmp)) File.Delete(tmp);
        }
    }
}
