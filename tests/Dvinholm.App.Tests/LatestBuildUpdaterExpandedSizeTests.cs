using System.Buffers.Binary;
using System.IO.Compression;
using System.Net;

namespace Dvinholm.App.Tests;

public sealed class LatestBuildUpdaterExpandedSizeTests
{
    private const uint ExpandedSizeOverLimit = 1_000_000_001;

    [Fact]
    public async Task ExpandedArchiveOverLimit_IsRejectedBeforeExtractionAndCleansTemporaryFiles()
    {
        var bytes = CreateZipWithOversizedDeclaredEntry();
        var build = new LatestBuildInfo(
            Guid.NewGuid().ToString("N").PadRight(40, 'a')[..40],
            1,
            1,
            "https://example.invalid/update.zip",
            bytes.LongLength,
            null,
            DateTimeOffset.UtcNow.ToString("O"));
        var stagingRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Dvinholm",
            "UpdateStaging");
        var tempPattern = $"dvinholm-{build.CommitSha}-*.zip";

        using var client = new HttpClient(new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(bytes)
            }));

        await Assert.ThrowsAsync<InvalidDataException>(
            () => LatestBuildUpdater.DownloadAndPrepareAsync(build, client));

        if (Directory.Exists(stagingRoot))
            Assert.Empty(Directory.EnumerateDirectories(stagingRoot, build.CommitSha + "-*"));
        Assert.Empty(Directory.EnumerateFiles(Path.GetTempPath(), tempPattern));
    }

    private static byte[] CreateZipWithOversizedDeclaredEntry()
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        using (var writer = new StreamWriter(archive.CreateEntry("Dvinholm.App.exe").Open()))
            writer.Write("small payload");

        var bytes = stream.ToArray();
        var localHeaderOffset = FindLastSignature(bytes, 0x04034b50);
        var centralDirectoryOffset = FindLastSignature(bytes, 0x02014b50);
        Assert.True(localHeaderOffset >= 0, "ZIP local file header was not found.");
        Assert.True(centralDirectoryOffset >= 0, "ZIP central directory entry was not found.");

        // Keep the local file header and central-directory metadata consistent.
        // The uncompressed-size field is at offset 22 in the local header and
        // offset 24 in the central-directory header. The payload itself stays tiny.
        BinaryPrimitives.WriteUInt32LittleEndian(
            bytes.AsSpan(localHeaderOffset + 22, sizeof(uint)),
            ExpandedSizeOverLimit);
        BinaryPrimitives.WriteUInt32LittleEndian(
            bytes.AsSpan(centralDirectoryOffset + 24, sizeof(uint)),
            ExpandedSizeOverLimit);
        return bytes;
    }

    private static int FindLastSignature(byte[] bytes, uint signature)
    {
        for (var i = bytes.Length - 4; i >= 0; i--)
        {
            if (BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(i, sizeof(uint))) == signature)
                return i;
        }

        return -1;
    }

    private sealed class StubHttpMessageHandler(
        Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(responseFactory(request));
    }
}
