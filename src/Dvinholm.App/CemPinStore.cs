using System.Buffers.Binary;
using System.Diagnostics;
using System.Security.Cryptography;

namespace Dvinholm.App;

internal sealed record CemPinSavedEntry(Guid Id, uint PartNumber, DateTimeOffset SavedAt)
{
    public override string ToString() => "Saved CEM PIN record (contents withheld)";
}
internal sealed record CemPinStoreInventory(IReadOnlyList<CemPinSavedEntry> Entries, int UnavailableCount);

/// <summary>Per-find encrypted records. No VIN, plaintext PIN, or vehicle-match inference.</summary>
internal sealed class CemPinStore
{
    private const int PayloadLength = 23;
    internal const int MaxVisibleRecords = 128;
    private const int MaxProtectedBytes = 8192;
    private readonly string _directory;
    private readonly Action<string, byte[]> _stage;

    internal CemPinStore(string directory, Action<string, byte[]>? stage = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        _directory = Path.GetFullPath(directory);
        _stage = stage ?? ((path, bytes) =>
        {
            using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            file.Write(bytes);
            file.Flush(flushToDisk: true);
        });
    }

    private Mutex AcquireWriteGate(
        CancellationToken cancellationToken = default, Func<bool>? leaseCurrent = null)
    {
        // Shared by Save and Delete so cooperating Dvinholm processes cannot
        // race record creation and deletion. Only the vault path, never a
        // vehicle identifier or PIN, contributes to the kernel mutex name.
        var normalized = Path.TrimEndingDirectorySeparator(_directory).ToUpperInvariant();
        var digest = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(normalized));
        var mutex = new Mutex(false, @"Local\Dvinholm.CemPinVault." + Convert.ToHexString(digest));
        try
        {
            // A single 15-second WaitOne cannot observe a user's cancellation
            // or an invalidated ECU lease until the entire wait completes.
            // Poll at most every 50 ms, preserving the absolute deadline.
            var clock = Stopwatch.StartNew();
            var deadline = TimeSpan.FromSeconds(15);
            var interval = TimeSpan.FromMilliseconds(50);
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (leaseCurrent is not null && !leaseCurrent())
                    throw new InvalidOperationException();

                var remaining = deadline - clock.Elapsed;
                if (remaining <= TimeSpan.Zero) throw new IOException();
                try
                {
                    if (mutex.WaitOne(remaining < interval ? remaining : interval))
                        return mutex;
                }
                catch (AbandonedMutexException) { return mutex; }
            }
        }
        catch
        {
            mutex.Dispose();
            throw;
        }
    }

    internal CemPinSavedEntry Save(
        CemPinAttemptResult result, uint partNumber, CancellationToken cancellationToken = default)
    {
        try
        {
            using var mutex = AcquireWriteGate(cancellationToken, () => result?.HasPin == true);
            try { return SaveWithWriteGate(result, partNumber, cancellationToken); }
            finally { mutex.ReleaseMutex(); }
        }
        catch (Exception)
        {
            throw new InvalidOperationException("The encrypted PIN record could not be saved.");
        }
    }

    private CemPinSavedEntry SaveWithWriteGate(
        CemPinAttemptResult result, uint partNumber, CancellationToken cancellationToken)
    {
        byte[]? plain = null;
        byte[]? encrypted = null;
        string? temporary = null;
        string? committedPath = null;
        try
        {
            ArgumentNullException.ThrowIfNull(result);
            void RequireCurrent()
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!result.HasPin) throw new InvalidOperationException();
            }

            RequireCurrent();
            // Synthetic/metadata-only/expired or revoked results must never
            // populate this store. HasPin revalidates the live cancellation,
            // ECU identity and J2534 lease for an accepted live candidate.
            if (partNumber == 0 || result.Status != CemPinAttemptStatus.CandidateAccepted ||
                result.Origin != CemPinResultOrigin.AdapterUnattested || !result.HasPin)
                throw new InvalidOperationException();
            var entry = new CemPinSavedEntry(Guid.NewGuid(), partNumber, DateTimeOffset.UtcNow);
            plain = new byte[PayloadLength];
            "DCP1"u8.CopyTo(plain);
            plain[4] = (byte)CemPinResultOrigin.AdapterUnattested;
            BinaryPrimitives.WriteUInt32LittleEndian(plain.AsSpan(5), partNumber);
            BinaryPrimitives.WriteInt64LittleEndian(plain.AsSpan(9), entry.SavedAt.UtcTicks);
            if (!result.TryReveal(bytes => bytes.CopyTo(plain.AsSpan(17))))
                throw new InvalidOperationException();
            // The plaintext payload is no longer needed after DPAPI returns.
            // Wipe it now, rather than retaining it throughout staging,
            // filesystem contention and possible rollback.
            encrypted = CemPinWindowsProtection.ProtectAndWipe(plain, entry.Id);
            RequireCurrent();
            if (encrypted.Length is <= 0 or > MaxProtectedBytes)
                throw new IOException("Protected record size rejected.");
            RejectLinkedAncestors();
            Directory.CreateDirectory(_directory);
            RejectLinkedAncestors();
            // A process crash bypasses Save's finally cleanup. Recover only
            // expired, correctly named Dvinholm staging files in a bounded
            // best-effort sweep; never touch fresh or unrelated .tmp files.
            CleanupStaleTemporaryStages();
            RequireCurrent();
            // The inventory deliberately enumerates at most 128 protected
            // records. Do not silently save more entries than the UI can
            // enumerate, including malformed/undecryptable .dpapi files.
            EnsureRecordCapacity();
            temporary = FilePath(entry.Id) + "." + Guid.NewGuid().ToString("N") + ".tmp";
            _stage(temporary, encrypted);
            // Staging may take time or invoke an injected writer in tests.
            // A revoked in-memory candidate or cancelled caller must not
            // reach a final filename after the staging writer completes.
            RequireCurrent();
            var staged = ReadBounded(temporary);
            try
            {
                if (!staged.AsSpan().SequenceEqual(encrypted)) throw new IOException();
            }
            finally { CryptographicOperations.ZeroMemory(staged); }
            RequireCurrent();
            // Recheck external additions after staging. Cooperating Dvinholm
            // writers are serialized until rollback/cleanup finishes.
            EnsureRecordCapacity();
            var destination = FilePath(entry.Id);
            if (File.Exists(destination))
                throw new IOException("PIN record collision.");
            // Recheck immediately before the atomic publication step, not
            // only when the secret was copied into the encryption buffer.
            RequireCurrent();
            File.Move(temporary, destination);
            committedPath = destination;
            // Revoke a newly published record if authorization disappeared
            // during the move. This never removes a pre-existing vault record.
            RequireCurrent();
            return entry;
        }
        catch (Exception)
        {
            if (committedPath is not null)
            {
                try { File.Delete(committedPath); }
                catch (IOException) { /* Never expose a machine-specific path. */ }
                catch (UnauthorizedAccessException) { }
            }
            throw new InvalidOperationException("The encrypted PIN record could not be saved.");
        }
        finally
        {
            if (plain is not null) CryptographicOperations.ZeroMemory(plain);
            if (encrypted is not null) CryptographicOperations.ZeroMemory(encrypted);
            if (temporary is not null)
            {
                try { File.Delete(temporary); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }
    }

    internal CemPinStoreInventory List()
    {
        try
        {
            RejectLinkedAncestors();
            if (!Directory.Exists(_directory)) return new(Array.Empty<CemPinSavedEntry>(), 0);
            var entries = new List<CemPinSavedEntry>();
            var unavailable = 0;
            var inspected = 0;
            foreach (var path in Directory.EnumerateFiles(_directory, "*.dpapi"))
            {
                // A hostile or corrupted vault directory must not trigger an
                // unbounded number of DPAPI operations or consume arbitrary RAM.
                if (++inspected > MaxVisibleRecords)
                {
                    unavailable++;
                    break;
                }
                if (!Guid.TryParseExact(Path.GetFileNameWithoutExtension(path), "N", out var id))
                { unavailable++; continue; }
                byte[]? plain = null;
                try
                {
                    plain = ReadPayload(id);
                    entries.Add(ParseEntry(id, plain));
                }
                catch (Exception) { unavailable++; }
                finally { if (plain is not null) CryptographicOperations.ZeroMemory(plain); }
            }
            return new(entries.OrderByDescending(e => e.SavedAt).ToArray(), unavailable);
        }
        catch (Exception) { throw new InvalidOperationException("Saved PIN records are unavailable."); }
    }

    internal CemPinSecret Load(Guid id)
    {
        byte[]? plain = null;
        try
        {
            plain = ReadPayload(id);
            ParseEntry(id, plain);
            return new CemPinSecret(plain.AsSpan(17, 6).ToArray(), CemPinResultOrigin.AdapterUnattested);
        }
        catch (Exception) { throw new InvalidOperationException("The saved PIN record is unavailable."); }
        finally { if (plain is not null) CryptographicOperations.ZeroMemory(plain); }
    }

    internal void Delete(Guid id)
    {
        try
        {
            using var mutex = AcquireWriteGate();
            try
            {
                RejectLinkedAncestors();
                var path = FilePath(id);
                FileAttributes attributes;
                try
                {
                    // File.Exists returns false for both missing entries and
                    // some access errors, falsely reporting successful deletion.
                    // Only actual absence is an idempotent no-op.
                    attributes = File.GetAttributes(path);
                }
                catch (FileNotFoundException) { return; }
                catch (DirectoryNotFoundException) { return; }

                // Never follow a junction or treat a directory named like a
                // record as a missing file. Corrupt ordinary files remain
                // removable so users can recover vault capacity explicitly.
                if ((attributes & (FileAttributes.Directory | FileAttributes.ReparsePoint)) != 0)
                    throw new IOException("Unsafe PIN vault record target.");
                File.Delete(path);
            }
            finally { mutex.ReleaseMutex(); }
        }
        catch (Exception) { throw new InvalidOperationException("The saved PIN record could not be deleted."); }
    }

    private void EnsureRecordCapacity()
    {
        var count = 0;
        foreach (var _ in Directory.EnumerateFiles(
            _directory, "*.dpapi", SearchOption.TopDirectoryOnly))
        {
            // Fail closed before creating an entry that would be invisible
            // to List's bounded inventory; never delete an existing record
            // automatically to make space.
            if (++count >= MaxVisibleRecords)
                throw new IOException("Encrypted PIN vault is full.");
        }
    }

    private void CleanupStaleTemporaryStages()
    {
        // Every Save uses <record GUID N>.<stage GUID N>.tmp, and files are
        // expected to live only for the duration of that single Save. Do not
        // delete fresh stages from another process, arbitrary names, links or
        // final .dpapi records. This maintenance must not block a new save.
        try
        {
            var olderThan = DateTime.UtcNow - TimeSpan.FromDays(1);
            var inspected = 0;
            foreach (var path in Directory.EnumerateFiles(
                _directory, "*.tmp", SearchOption.TopDirectoryOnly))
            {
                if (++inspected > 256) break; // Bound disk work in a hostile vault.
                var name = Path.GetFileName(path);
                if (name.Length != 69 || name[32] != '.' ||
                    !name.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase) ||
                    !Guid.TryParseExact(name.AsSpan(0, 32), "N", out _) ||
                    !Guid.TryParseExact(name.AsSpan(33, 32), "N", out _))
                    continue;

                try
                {
                    if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0 ||
                        File.GetLastWriteTimeUtc(path) > olderThan)
                        continue;
                    File.Delete(path);
                }
                catch (IOException) { /* An active or locked file is left alone. */ }
                catch (UnauthorizedAccessException) { }
                catch (System.Security.SecurityException) { }
            }
        }
        catch (IOException) { /* Cleanup is best effort, not a save prerequisite. */ }
        catch (UnauthorizedAccessException) { }
        catch (System.Security.SecurityException) { }
    }

    private string FilePath(Guid id) => Path.Combine(_directory, id.ToString("N") + ".dpapi");

    private byte[] ReadPayload(Guid id)
    {
        RejectLinkedAncestors();
        var encrypted = ReadBounded(FilePath(id));
        try { return CemPinWindowsProtection.Transform(encrypted, id, protect: false); }
        finally { CryptographicOperations.ZeroMemory(encrypted); }
    }

    private void RejectLinkedAncestors()
    {
        // Reparse-point directories could redirect PIN records into a user-
        // unrelated tree. Fail closed rather than following a junction.
        for (DirectoryInfo? current = new(_directory);
            current is not null; current = current.Parent)
        {
            if (current.Exists &&
                (current.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Linked PIN vault directory rejected.");
        }
    }

    private static void RejectLinkedRecord(string path)
    {
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            throw new IOException("Linked PIN record rejected.");
    }

    private static byte[] ReadBounded(string path)
    {
        RejectLinkedRecord(path);
        using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (file.Length is <= 0 or > MaxProtectedBytes) throw new IOException();
        var bytes = new byte[(int)file.Length];
        try
        {
            file.ReadExactly(bytes);
            return bytes;
        }
        catch
        {
            // An I/O failure after a partial read must not retain a
            // truncated protected record in a discarded managed buffer.
            CryptographicOperations.ZeroMemory(bytes);
            throw;
        }
    }

    private static CemPinSavedEntry ParseEntry(Guid id, byte[] plain)
    {
        if (plain.Length != PayloadLength || !plain.AsSpan(0, 4).SequenceEqual("DCP1"u8) ||
            plain[4] != (byte)CemPinResultOrigin.AdapterUnattested)
            throw new CryptographicException();
        foreach (var b in plain.AsSpan(17))
            if ((b >> 4) > 9 || (b & 15) > 9) throw new CryptographicException();
        var pn = BinaryPrimitives.ReadUInt32LittleEndian(plain.AsSpan(5));
        if (pn == 0) throw new CryptographicException();
        return new(id, pn, new DateTimeOffset(BinaryPrimitives.ReadInt64LittleEndian(plain.AsSpan(9)), TimeSpan.Zero));
    }
}
