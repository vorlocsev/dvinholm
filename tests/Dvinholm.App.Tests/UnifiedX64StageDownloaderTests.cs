using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace Dvinholm.App.Tests;

public sealed class UnifiedX64StageDownloaderTests
{
    private const string Tag = "v0.3.0";
    private const string Base = "https://github.com/vorlocsev/dvinholm/releases/download/v0.3.0/";
    private sealed class Handler(byte[] zip, byte[] sums) : HttpMessageHandler
    {
        internal int Calls;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, CancellationToken ct)
        {
            Calls++;
            var data = req.RequestUri!.AbsoluteUri == Base + "SHA256SUMS.txt" ? sums : zip;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(data) });
        }
    }

    private static byte[] Zip(ushort machine = 0x8664)
    {
        using var data = new MemoryStream();
        using (var archive = new ZipArchive(data, ZipArchiveMode.Create, leaveOpen: true))
        {
            var exe = new byte[512];
            exe[0] = 77; exe[1] = 90;
            BitConverter.GetBytes(128).CopyTo(exe, 60);
            exe[128] = 80; exe[129] = 69;
            BitConverter.GetBytes(machine).CopyTo(exe, 132);
            foreach (var (name, bytes) in new[] {
                ("Dvinholm.exe", exe), ("coreclr.dll", new byte[]{1}),
                ("hostfxr.dll", new byte[]{2}), ("Microsoft.UI.Xaml.dll", new byte[]{3}) })
            {
                var entry = archive.CreateEntry(name);
                using var output = entry.Open();
                output.Write(bytes);
            }
        }
        return data.ToArray();
    }

    private static string Root() => Path.Combine(Path.GetTempPath(),
        "dvinholm-stage-" + Guid.NewGuid().ToString("N"));

    private static UnifiedX64UpdateCandidate Candidate(byte[] zip) => new(
        Tag, "Dvinholm-Windows-x64.zip", zip.Length,
        Base + "Dvinholm-Windows-x64.zip", Base + "SHA256SUMS.txt");

    private static byte[] Checksums(byte[] zip, bool valid = true) =>
        Encoding.ASCII.GetBytes((valid
            ? Convert.ToHexString(SHA256.HashData(zip)).ToLowerInvariant()
            : new string('0', 64)) + "  Dvinholm-Windows-x64.zip\n");

    private static async Task InRoot(Func<string,Task> task)
    {
        var root = Root();
        try { await task(root); }
        finally { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task ValidX64IsStagedAndNotExtracted() => await InRoot(async root =>
    {
        var zip = Zip(); using var http = new HttpClient(new Handler(zip, Checksums(zip)));
        var found = await UnifiedX64StageDownloader.StageAsync(Candidate(zip), root, http);
        Assert.Equal(4, found.EntryCount);
        Assert.Equal(zip, File.ReadAllBytes(found.ZipPath));
        Assert.Single(Directory.EnumerateFiles(root, "*.zip", SearchOption.AllDirectories));
        Assert.DoesNotContain(Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories),
            p => p.EndsWith(".exe", StringComparison.OrdinalIgnoreCase));
    });

    [Fact]
    public async Task DigestMismatchDeletesStaging() => await InRoot(async root =>
    {
        var zip = Zip(); using var http = new HttpClient(new Handler(zip, Checksums(zip, false)));
        await Assert.ThrowsAsync<InvalidDataException>(() =>
            UnifiedX64StageDownloader.StageAsync(Candidate(zip), root, http));
        Assert.Empty(Directory.EnumerateFileSystemEntries(root));
    });

    [Fact]
    public async Task WrongBitnessIsRejectedAfterDownload() => await InRoot(async root =>
    {
        var zip = Zip(0x14c); using var http = new HttpClient(new Handler(zip, Checksums(zip)));
        await Assert.ThrowsAsync<InvalidDataException>(() =>
            UnifiedX64StageDownloader.StageAsync(Candidate(zip), root, http));
        Assert.Empty(Directory.EnumerateFileSystemEntries(root));
    });

    [Fact]
    public async Task WrongLengthCannotBeStaged() => await InRoot(async root =>
    {
        var zip = Zip(); using var http = new HttpClient(new Handler(zip, Checksums(zip)));
        await Assert.ThrowsAsync<InvalidDataException>(() =>
            UnifiedX64StageDownloader.StageAsync(Candidate(zip) with
                { PackageBytes = zip.Length + 1 }, root, http));
        Assert.Empty(Directory.EnumerateFileSystemEntries(root));
    });

    [Fact]
    public async Task BadManifestStopsBeforeZipRequest() => await InRoot(async root =>
    {
        var zip = Zip(); var handler = new Handler(zip, Encoding.ASCII.GetBytes("bad"));
        using var http = new HttpClient(handler);
        await Assert.ThrowsAsync<InvalidDataException>(() =>
            UnifiedX64StageDownloader.StageAsync(Candidate(zip), root, http));
        Assert.Equal(1, handler.Calls);
        Assert.Empty(Directory.EnumerateFileSystemEntries(root));
    });

    [Fact]
    public async Task OversizedChecksumStopsBeforeZipRequest() => await InRoot(async root =>
    {
        var zip = Zip(); var handler = new Handler(zip, new byte[65 * 1024]);
        using var http = new HttpClient(handler);
        await Assert.ThrowsAsync<InvalidDataException>(() =>
            UnifiedX64StageDownloader.StageAsync(Candidate(zip), root, http));
        Assert.Equal(1, handler.Calls);
        Assert.Empty(Directory.EnumerateFileSystemEntries(root));
    });

    [Fact]
    public async Task CancellationLeavesNoPartialDirectory() => await InRoot(async root =>
    {
        var zip = Zip(); using var http = new HttpClient(new Handler(zip, Checksums(zip)));
        using var cts = new CancellationTokenSource(); cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            UnifiedX64StageDownloader.StageAsync(Candidate(zip), root, http, cts.Token));
        Assert.Empty(Directory.EnumerateFileSystemEntries(root));
    });

    [Fact]
    public async Task ForeignUrlIsRejectedBeforeNetwork() => await InRoot(async root =>
    {
        var zip = Zip(); var handler = new Handler(zip, Checksums(zip));
        using var http = new HttpClient(handler);
        await Assert.ThrowsAsync<InvalidDataException>(() =>
            UnifiedX64StageDownloader.StageAsync(Candidate(zip) with
            { PackageUrl = "https://example.org/evil.zip" }, root, http));
        Assert.Equal(0, handler.Calls);
        Assert.False(Directory.Exists(root));
    });
}
