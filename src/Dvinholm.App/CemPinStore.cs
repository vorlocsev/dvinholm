using System.Buffers.Binary;
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

    internal CemPinSavedEntry Save(CemPinAttemptResult result, uint partNumber)
    {
        byte[]? plain = null;
        byte[]? encrypted = null;
        string? temporary = null;
        string? committedPath = null;
        try
        {
            ArgumentNullException.ThrowIfNull(result);
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
            encrypted = CemPinWindowsProtection.Transform(plain, entry.Id, protect: true);
            if (encrypted.Length is <= 0 or > MaxProtectedBytes)
                throw new IOException("Protected record size rejected.");
            RejectLinkedAncestors();
            Directory.CreateDirectory(_directory);
            RejectLinkedAncestors();
            // A process crash bypasses Save's finally cleanup. Recover only
            // expired, correctly named Dvinholm staging files in a bounded
            // best-effort sweep; never touch fresh or unrelated .tmp files.
            CleanupStaleTemporaryStages();
            // The inventory deliberately enumerates at most 128 protected
            // records. Do not silently save more entries than the UI can
            // enumerate, including malformed/undecryptable .dpapi files.
            EnsureRecordCapacity();
            temporary = FilePath(entry.Id) + "." + Guid.NewGuid().ToString("N") + ".tmp";
            _stage(temporary, encrypted);
            // Staging may take time or invoke an injected writer in tests.
            // A revoked in-memory candidate must not reach its final filename.
            if (!result.HasPin) throw new InvalidOperationException();
            var staged = ReadBounded(temporary);
            try
            {
                if (!staged.AsSpan().SequenceEqual(encrypted)) throw new IOException();
            }
            finally { CryptographicOperations.ZeroMemory(staged); }
            if (!result.HasPin) throw new InvalidOperationException();
            // Another writer may have published a record during staging.
            // Recheck after staging before exposing a new final filename.
            EnsureRecordCapacity();
            var destination = FilePath(entry.Id);
            if (File.Exists(destination))
                throw new IOException("PIN record collision.");
            // Recheck immediately before the atomic publication step, not
            // only when the secret was copied into the encryption buffer.
            if (!result.HasPin) throw new InvalidOperationException();
            File.Move(temporary, destination);
            committedPath = destination;
            // Revoke a newly published record if authorization disappeared
            // during the move. This never removes a pre-existing vault record.
            if (!result.HasPin) throw new InvalidOperationException();
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
            RejectLinkedAncestors();
            var path = FilePath(id);
            if (File.Exists(path))
            {
                RejectLinkedRecord(path);
                File.Delete(path);
            }
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
