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
        try
        {
            // Synthetic/metadata-only/expired results must never populate this store.
            if (partNumber == 0 || result.Status != CemPinAttemptStatus.CandidateAccepted ||
                result.Origin != CemPinResultOrigin.AdapterUnattested)
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
            Directory.CreateDirectory(_directory);
            temporary = FilePath(entry.Id) + "." + Guid.NewGuid().ToString("N") + ".tmp";
            _stage(temporary, encrypted);
            var staged = ReadBounded(temporary);
            try
            {
                if (!staged.AsSpan().SequenceEqual(encrypted)) throw new IOException();
            }
            finally { CryptographicOperations.ZeroMemory(staged); }
            File.Move(temporary, FilePath(entry.Id));
            return entry;
        }
        catch (Exception)
        {
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
            if (!Directory.Exists(_directory)) return new(Array.Empty<CemPinSavedEntry>(), 0);
            var entries = new List<CemPinSavedEntry>();
            var unavailable = 0;
            foreach (var path in Directory.EnumerateFiles(_directory, "*.dpapi"))
            {
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
        try { File.Delete(FilePath(id)); }
        catch (Exception) { throw new InvalidOperationException("The saved PIN record could not be deleted."); }
    }

    private string FilePath(Guid id) => Path.Combine(_directory, id.ToString("N") + ".dpapi");

    private byte[] ReadPayload(Guid id)
    {
        var encrypted = ReadBounded(FilePath(id));
        try { return CemPinWindowsProtection.Transform(encrypted, id, protect: false); }
        finally { CryptographicOperations.ZeroMemory(encrypted); }
    }

    private static byte[] ReadBounded(string path)
    {
        using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (file.Length is <= 0 or > 8192) throw new IOException();
        var bytes = new byte[(int)file.Length];
        file.ReadExactly(bytes);
        return bytes;
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
