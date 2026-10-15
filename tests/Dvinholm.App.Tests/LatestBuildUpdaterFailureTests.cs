using System.IO.Compression;
using System.Net;

namespace Dvinholm.App.Tests;

public sealed class LatestBuildUpdaterFailureTests
{
    [Fact]
    public async Task DownloadFailure_ThrowsAndCleansStagingDirectory()
    {
        using var client = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized));
        var build = BuildInfo(new byte[] { 1, 2, 3 });
        var stagingRoot = GetStagingRoot();

        var exception = await Assert.ThrowsAsync<HttpRequestException>(
            () => LatestBuildUpdater.DownloadAndPrepareAsync(build, client));

        Assert.NotNull(exception);
        AssertNoStagingDirectory(stagingRoot, build.CommitSha);
    }

    [Fact]
    public async Task SizeMismatch_ThrowsAndCleansStagingDirectory()
    {
        var bytes = CreateZip(("Dvinholm.App.exe", "fake executable"));
        using var client = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(bytes)
        });
        var build = BuildInfo(bytes, sizeOverride: bytes.Length + 1);
        var stagingRoot = GetStagingRoot();

        await Assert.ThrowsAsync<InvalidDataException>(
            () => LatestBuildUpdater.DownloadAndPrepareAsync(build, client));

        AssertNoStagingDirectory(stagingRoot, build.CommitSha);
    }

    [Fact]
    public async Task OversizedDeclaredResponse_ThrowsAndCleansStagingDirectory()
    {
        using var client = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(Array.Empty<byte>())
            {
                Headers = { ContentLength = 500L * 1024 * 1024 + 1 }
            }
        });
        var build = BuildInfo(Array.Empty<byte>());
        var stagingRoot = GetStagingRoot();

        await Assert.ThrowsAsync<InvalidDataException>(
            () => LatestBuildUpdater.DownloadAndPrepareAsync(build, client));

        AssertNoStagingDirectory(stagingRoot, build.CommitSha);
    }

    [Fact]
    public async Task CanceledDownload_ThrowsAndCleansStagingDirectory()
    {
        using var client = new HttpClient(new CancelingHttpMessageHandler());
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));
        var build = BuildInfo(new byte[] { 1, 2, 3 });
        var stagingRoot = GetStagingRoot();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => LatestBuildUpdater.DownloadAndPrepareAsync(build, client, cancellationToken: cancellation.Token));

        AssertNoStagingDirectory(stagingRoot, build.CommitSha);
    }

    [Fact]
    public async Task InvalidSha256_ThrowsAndCleansStagingDirectory()
    {
        var bytes = CreateZip(("Dvinholm.App.exe", "fake executable"));
        using var client = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(bytes)
        });
        var build = BuildInfo(bytes, digest: "sha256:" + new string('0', 64));
        var stagingRoot = GetStagingRoot();

        await Assert.ThrowsAsync<InvalidDataException>(
            () => LatestBuildUpdater.DownloadAndPrepareAsync(build, client));

        AssertNoStagingDirectory(stagingRoot, build.CommitSha);
    }


    [Fact]
    public async Task MatchingSha256_PreparesBuildAndKeepsExtractedExecutable()
    {
        var bytes = CreateZip(("Dvinholm.App.exe", "fake executable"));
        var digest = "sha256:" + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant();
        using var client = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(bytes)
        });
        var build = BuildInfo(bytes, digest: digest);
        string? staging = null;

        try
        {
            staging = await LatestBuildUpdater.DownloadAndPrepareAsync(build, client);

            var executable = Path.Combine(staging, "Dvinholm.App.exe");
            Assert.True(File.Exists(executable));
            Assert.Equal("fake executable", await File.ReadAllTextAsync(executable));
        }
        finally
        {
            if (staging is not null && Directory.Exists(staging))
                Directory.Delete(staging, recursive: true);
        }
    }

    [Fact]
    public async Task CorruptZip_ThrowsAndCleansStagingDirectory()
    {
        var bytes = new byte[] { 0x44, 0x56, 0x49, 0x4E, 0x48, 0x4F, 0x4C, 0x4D };
        using var client = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(bytes)
        });
        var build = BuildInfo(bytes);
        var stagingRoot = GetStagingRoot();

        await Assert.ThrowsAnyAsync<Exception>(
            () => LatestBuildUpdater.DownloadAndPrepareAsync(build, client));

        AssertNoStagingDirectory(stagingRoot, build.CommitSha);
    }

    [Fact]
    public async Task UnsafeArchivePath_ThrowsAndCleansStagingDirectory()
    {
        var bytes = CreateZip(("../escape.txt", "unsafe"), ("Dvinholm.App.exe", "fake executable"));
        using var client = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(bytes)
        });
        var build = BuildInfo(bytes);
        var stagingRoot = GetStagingRoot();

        await Assert.ThrowsAsync<InvalidDataException>(
            () => LatestBuildUpdater.DownloadAndPrepareAsync(build, client));

        AssertNoStagingDirectory(stagingRoot, build.CommitSha);
    }

    [Fact]
    public async Task MissingExecutable_ThrowsAndCleansStagingDirectory()
    {
        var bytes = CreateZip(("readme.txt", "not an app"));
        using var client = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(bytes)
        });
        var build = BuildInfo(bytes);
        var stagingRoot = GetStagingRoot();

        await Assert.ThrowsAsync<InvalidDataException>(
            () => LatestBuildUpdater.DownloadAndPrepareAsync(build, client));

        AssertNoStagingDirectory(stagingRoot, build.CommitSha);
    }

    private static HttpClient CreateClient(Func<HttpRequestMessage, HttpResponseMessage> responseFactory) =>
        new(new StubHttpMessageHandler(responseFactory));

    private static LatestBuildInfo BuildInfo(byte[] bytes, long? sizeOverride = null, string? digest = null)
    {
        var sha = Guid.NewGuid().ToString("N").PadRight(40, 'a')[..40];
        return new LatestBuildInfo(
            sha,
            1,
            1,
            "https://example.invalid/update.zip",
            sizeOverride ?? bytes.LongLength,
            digest,
            DateTimeOffset.UtcNow.ToString("O"));
    }

    private static byte[] CreateZip(params (string Name, string Content)[] files)
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var file in files)
            {
                var entry = archive.CreateEntry(file.Name);
                using var writer = new StreamWriter(entry.Open());
                writer.Write(file.Content);
            }
        }

        return stream.ToArray();
    }

    private static string GetStagingRoot() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Dvinholm",
        "UpdateStaging");

    private static void AssertNoStagingDirectory(string root, string commitSha)
    {
        if (!Directory.Exists(root))
            return;

        Assert.Empty(Directory.EnumerateDirectories(root, commitSha + "-*"));
    }

    private sealed class CancelingHttpMessageHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new InvalidOperationException("The canceled request should not return a response.");
        }
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
