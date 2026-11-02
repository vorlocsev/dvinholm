using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace Dvinholm.App;

/// <summary>
/// A downloaded, integrity-checked local archive. This is NOT installation
/// approval: verified publisher provenance, backup, restart and rollback
/// require separate acceptance gates before any executable can be replaced.
/// </summary>
internal sealed record UnifiedX64StagedPackage(
    string Directory, string ZipPath, string Sha256, long ZipBytes,
    int EntryCount, long ExpandedBytes);

/// <summary>
/// Downloads a selected GitHub release to a fresh, isolated per-request
/// directory; validates bytes without extracting, executing, or installing.
/// Caller is responsible for retention/deletion of successful staging.
/// </summary>
internal static class UnifiedX64StageDownloader
{
    private const int BufferSize = 64 * 1024;
    private const int MaxChecksumBytes = 64 * 1024;

    internal static async Task<UnifiedX64StagedPackage> StageAsync(
        UnifiedX64UpdateCandidate candidate,
        string stagingRoot,
        HttpClient http,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(http);
        ArgumentException.ThrowIfNullOrWhiteSpace(stagingRoot);
        if (!ValidateCandidate(candidate))
            throw new InvalidDataException("Untrusted x64 update metadata.");

        // Callers must pass a dedicated staging root (not an installation dir).
        // The path is intentionally never derived from untrusted GitHub tags,
        // archive filenames or HTTP response headers.
        var basePath = Path.GetFullPath(stagingRoot);
        Directory.CreateDirectory(basePath);
        var directory = Path.Combine(basePath, "pending-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var zipPath = Path.Combine(directory, UnifiedX64ReleaseFeed.PackageAsset);
        try
        {
            var manifest = await ReadBoundedTextAsync(
                http, candidate.ChecksumUrl, MaxChecksumBytes, cancellationToken)
                .ConfigureAwait(false);
            // Fail fast on a missing, malformed or duplicated x64 digest,
            // before downloading a potentially very large ZIP.
            UnifiedX64PackageVerifier.GetExpectedDigest(manifest);

            await DownloadExactFileAsync(
                http, candidate.PackageUrl, zipPath, candidate.PackageBytes,
                cancellationToken).ConfigureAwait(false);

            cancellationToken.ThrowIfCancellationRequested();
            var verified = UnifiedX64PackageVerifier.Verify(
                zipPath, manifest, candidate.PackageBytes);
            cancellationToken.ThrowIfCancellationRequested();
            return new UnifiedX64StagedPackage(
                directory, zipPath, verified.Sha256, candidate.PackageBytes,
                verified.EntryCount, verified.ExpandedBytes);
        }
        catch
        {
            // Partial/cancelled/tampered updates must never persist or be reused.
            try { Directory.Delete(directory, recursive: true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            throw;
        }
    }

    internal static bool ValidateCandidate(UnifiedX64UpdateCandidate value) =>
        value.PackageName == UnifiedX64ReleaseFeed.PackageAsset &&
        value.PackageBytes is > 0 and <= UnifiedX64PackageVerifier.MaximumZipBytes &&
        (UnifiedX64ReleaseFeed.MatchesChannel(
            value.Tag, prerelease: false, UpdateSourceChannel.Stable) ||
         UnifiedX64ReleaseFeed.MatchesChannel(
            value.Tag, prerelease: true, UpdateSourceChannel.Development)) &&
        UnifiedX64ReleaseFeed.TrustedAssetUrl(
            value.PackageUrl, value.Tag, UnifiedX64ReleaseFeed.PackageAsset) &&
        UnifiedX64ReleaseFeed.TrustedAssetUrl(
            value.ChecksumUrl, value.Tag, UnifiedX64ReleaseFeed.ChecksumsAsset);

    private static async Task<string> ReadBoundedTextAsync(
        HttpClient client, string url, int maxBytes, CancellationToken token)
    {
        using var response = await client.GetAsync(url,
            HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength is long advertised &&
            advertised > maxBytes)
            throw new InvalidDataException("Checksum asset is too large.");

        await using var source = await response.Content.ReadAsStreamAsync(token)
            .ConfigureAwait(false);
        using var memory = new MemoryStream();
        var buffer = new byte[BufferSize];
        while (true)
        {
            var read = await source.ReadAsync(buffer.AsMemory(0, Math.Min(
                buffer.Length, maxBytes + 1 - (int)memory.Length)), token)
                .ConfigureAwait(false);
            if (read == 0) break;
            if (memory.Length + read > maxBytes)
                throw new InvalidDataException("Checksum body exceeded size limit.");
            memory.Write(buffer, 0, read);
        }
        // SHA256SUMS format is ASCII; rejecting non-ASCII prevents alternate
        // Unicode spellings from masquerading as verified filenames.
        if (memory.GetBuffer().AsSpan(0, (int)memory.Length)
            .IndexOfAnyExceptInRange((byte)0x0A, (byte)0x7E) >= 0)
        {
            // Permit CR as well, which is below LF in ASCII order.
            foreach (var b in memory.GetBuffer().AsSpan(0, (int)memory.Length))
                if (b != (byte)'\r' && b != (byte)'\n' &&
                    (b < 0x20 || b > 0x7E))
                    throw new InvalidDataException("Non-ASCII checksum body.");
        }
        return Encoding.ASCII.GetString(memory.GetBuffer(), 0, (int)memory.Length);
    }

    private static async Task DownloadExactFileAsync(
        HttpClient client, string url, string destination,
        long expectedBytes, CancellationToken token)
    {
        using var response = await client.GetAsync(url,
            HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength is long advertised &&
            advertised != expectedBytes)
            throw new InvalidDataException("Advertised ZIP byte size differs from release.");

        await using var input = await response.Content.ReadAsStreamAsync(token)
            .ConfigureAwait(false);
        await using var file = new FileStream(destination, FileMode.CreateNew,
            FileAccess.Write, FileShare.None, BufferSize, useAsync: true);

        var buffer = new byte[BufferSize];
        long total = 0;
        while (true)
        {
            var remaining = expectedBytes - total;
            var limit = (int)Math.Min(buffer.Length, remaining + 1);
            var read = await input.ReadAsync(buffer.AsMemory(0, limit), token)
                .ConfigureAwait(false);
            if (read == 0) break;
            if (read > remaining)
                throw new InvalidDataException("ZIP download exceeds declared length.");
            await file.WriteAsync(buffer.AsMemory(0, read), token).ConfigureAwait(false);
            total += read;
        }
        if (total != expectedBytes)
            throw new InvalidDataException("ZIP download is truncated.");
        await file.FlushAsync(token).ConfigureAwait(false);
    }
}
