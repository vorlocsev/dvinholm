using System.Text;

namespace Dvinholm.App;

/// <summary>
/// Bounded, strict UTF-8 reader shared by selected profile imports and the
/// saved primary/backup catalog. Reject excessive input before allocating a
/// string, with no J2534 access or profile state changes.
/// </summary>
internal static class EcuProfileCatalogInput
{
    internal const int MaximumJsonBytes = 2 * 1024 * 1024;
    internal const int MaximumProfiles = 512;
    internal const int MaximumJsonDepth = 8;

    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    internal static string ReadFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read,
            FileShare.Read, 4096, FileOptions.SequentialScan);
        var bytes = Allocate(stream);
        stream.ReadExactly(bytes);
        if (stream.ReadByte() != -1)
            throw new InvalidDataException("ECU profile JSON changed during inspection.");
        return Decode(bytes);
    }

    internal static async Task<string> ReadFileAsync(
        string path, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        cancellationToken.ThrowIfCancellationRequested();
        await using var stream = new FileStream(path, FileMode.Open,
            FileAccess.Read, FileShare.Read, 4096,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        var bytes = Allocate(stream);
        await stream.ReadExactlyAsync(bytes, cancellationToken);
        var extra = new byte[1];
        if (await stream.ReadAsync(extra, cancellationToken) != 0)
            throw new InvalidDataException("ECU profile JSON changed during inspection.");
        cancellationToken.ThrowIfCancellationRequested();
        return Decode(bytes);
    }

    private static byte[] Allocate(FileStream stream)
    {
        if (stream.Length is < 1 or > MaximumJsonBytes)
            throw new InvalidDataException(
                "ECU profile JSON is empty or exceeds the bounded import limit.");
        return new byte[(int)stream.Length];
    }

    private static string Decode(byte[] bytes)
    {
        try
        {
            // Keep compatibility with existing UTF-8 BOM profile catalogs
            // previously read through File.ReadAllText.
            var contents = StrictUtf8.GetString(bytes);
            return contents.Length > 0 && contents[0] == '\uFEFF'
                ? contents[1..] : contents;
        }
        catch (DecoderFallbackException ex)
        {
            throw new InvalidDataException("ECU profile JSON must be valid UTF-8.", ex);
        }
    }
}
