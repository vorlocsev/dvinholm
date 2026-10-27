using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;

namespace Dvinholm.App.Tests;

/// <summary>
/// Eight offline checks of the extracted app executable, with local ZIP and
/// HTTP fixtures; no installed updater, Windows UI or J2534 hardware.
/// </summary>
public sealed class UpdateStagedExecutableGuardTests
{
    [Fact]
    public void NonemptyRootExecutableIsAccepted()
    {
        using var folder = new TestFolder();
        File.WriteAllText(Path.Combine(folder.Path, "Dvinholm.App.exe"), "fixture");
        LatestBuildUpdater.ValidateStagedExecutable(folder.Path);
    }

    [Fact]
    public void MissingRootExecutableIsRejected()
    {
        using var folder = new TestFolder();
        Assert.Throws<InvalidDataException>(() =>
            LatestBuildUpdater.ValidateStagedExecutable(folder.Path));
    }

    [Fact]
    public void ZeroByteRootExecutableIsRejected()
    {
        using var folder = new TestFolder();
        File.WriteAllBytes(Path.Combine(folder.Path, "Dvinholm.App.exe"), []);
        var ex = Assert.Throws<InvalidDataException>(() =>
            LatestBuildUpdater.ValidateStagedExecutable(folder.Path));
        Assert.Contains("empty", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DirectoryNamedLikeExecutableIsRejected()
    {
        using var folder = new TestFolder();
        Directory.CreateDirectory(Path.Combine(folder.Path, "Dvinholm.App.exe"));
        Assert.Throws<InvalidDataException>(() =>
            LatestBuildUpdater.ValidateStagedExecutable(folder.Path));
    }

    [Fact]
    public void ExecutableOnlyInNestedFolderIsRejected()
    {
        using var folder = new TestFolder();
        var nested = Directory.CreateDirectory(Path.Combine(folder.Path, "Assets"));
        File.WriteAllText(Path.Combine(nested.FullName, "Dvinholm.App.exe"), "fixture");
        Assert.Throws<InvalidDataException>(() =>
            LatestBuildUpdater.ValidateStagedExecutable(folder.Path));
    }

    [Fact]
    public async Task ValidZipDownloadWithMatchingDigestIsPrepared()
    {
        var zip = CreateZip("prepared");
        var build = NewBuild(zip);
        using var http = new HttpClient(new ZipResponseHandler(zip));
        string? staging = null;
        try
        {
            staging = await LatestBuildUpdater.DownloadAndPrepareAsync(build, http);
            Assert.Equal("prepared", File.ReadAllText(Path.Combine(staging, "Dvinholm.App.exe")));
        }
        finally
        {
            if (staging is not null && Directory.Exists(staging))
                Directory.Delete(staging, recursive: true);
        }
    }

    [Fact]
    public async Task EmptyExecutableInValidZipCannotPrepareOrLeaveStage()
    {
        var zip = CreateZip("");
        var build = NewBuild(zip);
        using var http = new HttpClient(new ZipResponseHandler(zip));
        var ex = await Assert.ThrowsAsync<InvalidDataException>(() =>
            LatestBuildUpdater.DownloadAndPrepareAsync(build, http));
        Assert.Contains("empty", ex.Message, StringComparison.OrdinalIgnoreCase);

        var stagingRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Dvinholm", "UpdateStaging");
        if (Directory.Exists(stagingRoot))
            Assert.Empty(Directory.EnumerateDirectories(stagingRoot, build.CommitSha + "-*"));
        Assert.Empty(Directory.EnumerateFiles(
            Path.GetTempPath(), "dvinholm-" + build.CommitSha + "-*.zip"));
    }

    [Fact]
    public void ExecutableGuardIsBetweenExtractionAndReadyState()
    {
        var code = ReadUpdaterSource();
        var start = code.IndexOf("internal static async Task<string> DownloadAndPrepareAsync(", StringComparison.Ordinal);
        var extract = code.IndexOf("await ValidateAndExtractArchiveAsync(archivePath, staging, cancellationToken);", start, StringComparison.Ordinal);
        var guard = code.IndexOf("ValidateStagedExecutable(staging);", extract, StringComparison.Ordinal);
        var ready = code.IndexOf("prepared = true;", guard, StringComparison.Ordinal);
        Assert.True(start >= 0 && extract > start && guard > extract && ready > guard);
        Assert.Contains("file.Attributes & FileAttributes.ReparsePoint", code);
        Assert.Contains("if (file.Length == 0)", code);
    }

    private static byte[] CreateZip(string executableContent)
    {
        using var buffer = new MemoryStream();
        using (var archive = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            using (var writer = new StreamWriter(archive.CreateEntry("Dvinholm.App.exe").Open()))
                writer.Write(executableContent);
            using (var writer = new StreamWriter(archive.CreateEntry("Assets/readme.txt").Open()))
                writer.Write("fixture");
        }
        return buffer.ToArray();
    }

    private static LatestBuildInfo NewBuild(byte[] zip)
    {
        var sha = Guid.NewGuid().ToString("N");
        var digest = "sha256:" + Convert.ToHexString(SHA256.HashData(zip)).ToLowerInvariant();
        return new LatestBuildInfo(sha, 1, 1, "https://example.invalid/local-update.zip",
            zip.LongLength, digest, "2026-10-27T00:00:00Z");
    }

    private static string ReadUpdaterSource()
    {
        for (DirectoryInfo? dir = new(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var path = Path.Combine(dir.FullName, "src", "Dvinholm.App", "LatestBuildUpdater.cs");
            if (File.Exists(path))
                return File.ReadAllText(path);
        }
        throw new FileNotFoundException("LatestBuildUpdater.cs not found.");
    }

    private sealed class ZipResponseHandler(byte[] zip) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(zip)
            });
    }

    private sealed class TestFolder : IDisposable
    {
        internal string Path { get; } = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), "dvinholm-executable-" + Guid.NewGuid().ToString("N"));
        internal TestFolder() => Directory.CreateDirectory(Path);
        public void Dispose()
        {
            if (Directory.Exists(Path))
                Directory.Delete(Path, recursive: true);
        }
    }
}
