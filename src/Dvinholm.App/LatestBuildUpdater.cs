using System.Diagnostics;
using System.IO.Compression;
using System.Net.Http.Headers;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;

namespace Dvinholm.App;

internal sealed record LatestBuildInfo(
    string CommitSha,
    int RunNumber,
    long RunId,
    string DownloadUrl,
    long ArtifactSize,
    string? ArtifactDigest,
    string CreatedAt,
    string? ReleaseTag = null)
{
    public string ShortSha => CommitSha.Length >= 7 ? CommitSha[..7] : CommitSha;
    public string DisplayVersion => ReleaseTag is null
        ? $"CI #{RunNumber} · {ShortSha}"
        : ReleaseTag[1..];
}

internal static class LatestBuildUpdater
{
    private const string Repository = "vorlocsev/dvinholm";
    private const long WorkflowId = 378992585;
    private const string ArtifactName = "Dvinholm-Windows-x86";
    private const long MaximumDownloadBytes = 500L * 1024 * 1024;
    private const long MaximumExpandedBytes = 1_000L * 1024 * 1024;
    // Aggregate bytes alone do not bound an archive containing thousands
    // of empty files/directories. Limit metadata and filesystem operations.
    internal const int MaximumArchiveEntries = 10_000;
    private static readonly HttpClient Http = CreateHttpClient();

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Dvinholm", "0.2.4"));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
        return client;
    }

    public static string CurrentVersion
    {
        get
        {
            var value = Assembly.GetExecutingAssembly()
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            return string.IsNullOrWhiteSpace(value) ? "version unknown" : value;
        }
    }

    public static string CurrentDisplayVersion => FormatInstalledVersion(CurrentVersion);

    // The full informational version and SHA remain available internally for
    // updater integrity checks. Stable status labels show only the release
    // number; Development keeps its CI number and short commit identifier.
    internal static string FormatInstalledVersion(string informationalVersion)
    {
        if (string.IsNullOrWhiteSpace(informationalVersion))
            return "version unknown";

        var plus = informationalVersion.LastIndexOf('+');
        var sha = GetEmbeddedCommitSha(informationalVersion, plus);
        if (sha is null)
            return informationalVersion;

        if (IsStableInformationalVersion(informationalVersion))
            return informationalVersion[..plus];

        var ciRun = ParseCiRunNumber(informationalVersion);
        return ciRun is int run
            ? $"CI #{run} · {sha[..7]}"
            : $"{informationalVersion[..plus]} · {sha[..7]}";
    }

    internal static bool IsStableInformationalVersion(string informationalVersion) =>
        informationalVersion.EndsWith(".stable", StringComparison.OrdinalIgnoreCase) &&
        ParseCommitSha(informationalVersion) is not null;

    internal static bool IsCurrentStableBuild => IsStableInformationalVersion(CurrentVersion);

    public static string CurrentWindowTitle =>
        CurrentCiRunNumber is int runNumber ? $"Dvinholm CI #{runNumber}" : "Dvinholm";

    public static int? CurrentCiRunNumber => ParseCiRunNumber(CurrentVersion);

    private static int? ParseCiRunNumber(string informationalVersion)
    {
        var plus = informationalVersion.LastIndexOf('+');
        if (plus < 0)
            return null;

        var suffix = informationalVersion[(plus + 1)..];
        var marker = suffix.LastIndexOf(".ci", StringComparison.OrdinalIgnoreCase);
        return marker >= 0 && int.TryParse(suffix[(marker + 3)..], out var runNumber) && runNumber > 0
            ? runNumber
            : null;
    }

    internal static string? ParseCommitSha(string informationalVersion) =>
        GetEmbeddedCommitSha(informationalVersion, informationalVersion.LastIndexOf('+'));

    public static string? CurrentCommitSha => ParseCommitSha(CurrentVersion);

    internal static bool IsUpdateAvailable(string? currentCommitSha, LatestBuildInfo latestBuild) =>
        string.IsNullOrWhiteSpace(currentCommitSha) ||
        !string.Equals(currentCommitSha, latestBuild.CommitSha, StringComparison.OrdinalIgnoreCase);

    // A SHA mismatch is not enough to decide whether the selected Stable
    // version is newer. Confirm any replacement from that channel so a
    // user cannot accidentally install an older release over Development.
    internal static bool RequiresStableReplacementConfirmation(
        bool isStableChannel, string? currentCommitSha, LatestBuildInfo candidate) =>
        isStableChannel &&
        candidate.ReleaseTag is not null &&
        IsUpdateAvailable(currentCommitSha, candidate);

    internal static string FormatUpdateStatus(
        string currentDisplayVersion,
        LatestBuildInfo latestBuild,
        bool updateAvailable) =>
        updateAvailable
            ? $"Current: {currentDisplayVersion} · Available: {latestBuild.DisplayVersion}"
            : $"Current: {currentDisplayVersion} · Up to date (latest: {latestBuild.DisplayVersion})";

    private static string? GetEmbeddedCommitSha(string value, int plus)
    {
        if (plus < 0)
            return null;

        var suffix = value[(plus + 1)..];
        var ciMarker = suffix.IndexOf(".ci", StringComparison.OrdinalIgnoreCase);
        // Stable embeds an exact 40-digit commit followed by ".stable".
        // The old parser treated "SHA.stable" as a 47-character SHA and
        // mistakenly offered another install of the already-current release.
        var sha = ciMarker >= 0
            ? suffix[..ciMarker]
            : suffix.EndsWith(".stable", StringComparison.OrdinalIgnoreCase)
                ? suffix[..^".stable".Length]
                : suffix;
        return sha.Length == 40 && sha.All(Uri.IsHexDigit) ? sha.ToLowerInvariant() : null;
    }

    public static Task<LatestBuildInfo> GetLatestSuccessfulBuildAsync(CancellationToken cancellationToken = default) =>
        GetLatestSuccessfulBuildAsync(Http, cancellationToken);

    internal static async Task<LatestBuildInfo> GetLatestSuccessfulBuildAsync(
        HttpClient http,
        CancellationToken cancellationToken = default)
    {
        // Search all pages in descending GitHub Actions order. Branch pushes may not
        // publish updater assets, so the newest eligible build can be older than page 1.
        for (var page = 1; ; page++)
        {
            var runsUrl = $"https://api.github.com/repos/{Repository}/actions/workflows/{WorkflowId}/runs?per_page=100&page={page}";
            using var runsResponse = await http.GetAsync(runsUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            runsResponse.EnsureSuccessStatusCode();
            await using var runsStream = await runsResponse.Content.ReadAsStreamAsync(cancellationToken);
            using var runsJson = await JsonDocument.ParseAsync(runsStream, cancellationToken: cancellationToken);

            if (!runsJson.RootElement.TryGetProperty("workflow_runs", out var runs) || runs.ValueKind != JsonValueKind.Array)
                throw new InvalidDataException("GitHub did not return the CI workflow run list.");

            foreach (var run in runs.EnumerateArray())
            {
                // Pull-request workflows can build a synthetic merge ref, so their
                // artifact SHA is not necessarily the branch's head_sha. Only successful
                // pushes to main publish the public updater assets.
                var eventName = ReadString(run, "event");
                // The CI workflow publishes public updater release assets only for
                // pushes to main. Feature-branch pushes and workflow_dispatch runs
                // cannot have matching assets, so querying each release wastes the
                // unauthenticated GitHub API rate limit.
                if (eventName != "push" ||
                    ReadString(run, "head_branch") != "main" ||
                    ReadString(run, "status") != "completed" ||
                    ReadString(run, "conclusion") != "success")
                    continue;

                var sha = ReadString(run, "head_sha");
                if (sha.Length != 40 || !sha.All(Uri.IsHexDigit))
                    continue;

                var runId = ReadInt64(run, "id");
                var rawRunNumber = ReadInt64(run, "run_number");
                // Treat malformed numeric metadata as an ineligible run instead of
                // letting a checked cast abort discovery before older builds are tried.
                if (runId <= 0 || rawRunNumber <= 0 || rawRunNumber > int.MaxValue)
                    continue;

                var runNumber = (int)rawRunNumber;
                var createdAt = ReadString(run, "created_at");
                // GitHub Actions artifact ZIP endpoints require authentication, even for
                // public repositories. Successful non-PR CI runs publish a public
                // prerelease asset tagged ci-<run number>; download that asset anonymously.
                var releaseUrl = $"https://api.github.com/repos/{Repository}/releases/tags/ci-{runNumber}";
                using var releaseResponse = await http.GetAsync(releaseUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                // A missing tag is expected when an older successful run did not publish
                // an updater asset. Other HTTP errors (especially 403/429 rate limits)
                // must stop discovery rather than trigger more requests for older runs.
                if (releaseResponse.StatusCode == System.Net.HttpStatusCode.NotFound)
                    continue;
                releaseResponse.EnsureSuccessStatusCode();

                await using var releaseStream = await releaseResponse.Content.ReadAsStreamAsync(cancellationToken);
                using var releaseJson = await JsonDocument.ParseAsync(releaseStream, cancellationToken: cancellationToken);
                if (!releaseJson.RootElement.TryGetProperty("assets", out var assets) || assets.ValueKind != JsonValueKind.Array)
                    continue;

                foreach (var asset in assets.EnumerateArray())
                {
                    if (ReadString(asset, "name") != $"{ArtifactName}.zip")
                        continue;

                    var size = ReadInt64(asset, "size");
                    var downloadUrl = ReadString(asset, "browser_download_url");
                    if (size <= 0 || size > MaximumDownloadBytes || !Uri.TryCreate(downloadUrl, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps || !string.Equals(uri.Host, "github.com", StringComparison.OrdinalIgnoreCase) ||
                        !uri.IsDefaultPort ||
                        !string.IsNullOrEmpty(uri.UserInfo) ||
                        !string.IsNullOrEmpty(uri.Query) ||
                        !string.IsNullOrEmpty(uri.Fragment) ||
                        !string.Equals(
                            uri.AbsolutePath,
                            $"/{Repository}/releases/download/ci-{runNumber}/{ArtifactName}.zip",
                            StringComparison.OrdinalIgnoreCase))
                        continue;

                    string? digest = null;
                    if (asset.TryGetProperty("digest", out var digestElement) &&
                        digestElement.ValueKind != JsonValueKind.Null)
                    {
                        // GitHub may omit the digest or return null for older assets.
                        // If the property is present with any other value, it must be
                        // a non-empty string containing a complete SHA-256 value.
                        if (digestElement.ValueKind != JsonValueKind.String)
                            continue;

                        digest = digestElement.GetString();
                        if (string.IsNullOrEmpty(digest))
                            continue;
                    }

                    string? validatedDigest = null;
                    if (digest is not null)
                    {
                        if (!digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase) ||
                            digest.Length != 71 ||
                            !digest[7..].All(Uri.IsHexDigit))
                            continue;

                        validatedDigest = digest.ToLowerInvariant();
                    }

                    // Prefer GitHub's release-asset API with an explicit octet-stream
                    // request. The browser URL can redirect to a signed asset URL and
                    // may fail with 401 in clients that negotiate the API JSON media type.
                    var assetId = ReadInt64(asset, "id");
                    if (assetId > 0)
                        downloadUrl = $"https://api.github.com/repos/{Repository}/releases/assets/{assetId}";

                    return new LatestBuildInfo(
                        sha.ToLowerInvariant(),
                        runNumber,
                        runId,
                        downloadUrl,
                        size,
                        validatedDigest,
                        createdAt);
                }
            }

            // A short page means GitHub has no further runs to inspect.
            if (runs.GetArrayLength() < 100)
                break;
        }

        throw new InvalidOperationException("No successful main-branch CI run with a published Windows x86 updater asset was found.");
    }

    // Unlike the CI channel, the stable channel resolves the *published* release
    // tag to a Git commit. It never trusts a moving branch or a release's
    // free-form target_commitish as the identity of the archive.
    public static Task<LatestBuildInfo> GetLatestStableReleaseAsync(CancellationToken cancellationToken = default) =>
        GetLatestStableReleaseAsync(Http, cancellationToken);

    internal static async Task<LatestBuildInfo> GetLatestStableReleaseAsync(
        HttpClient http,
        CancellationToken cancellationToken = default)
    {
        var releaseUrl = $"https://api.github.com/repos/{Repository}/releases/latest";
        using var response = await http.GetAsync(releaseUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var release = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var root = release.RootElement;

        if (!root.TryGetProperty("draft", out var draft) || draft.ValueKind != JsonValueKind.False ||
            !root.TryGetProperty("prerelease", out var prerelease) || prerelease.ValueKind != JsonValueKind.False)
            throw new InvalidDataException("The latest GitHub release is not a published stable release.");

        var tag = ReadString(root, "tag_name");
        var components = tag.Length > 1 && tag[0] == 'v' ? tag[1..].Split('.') : [];
        if (components.Length != 3 ||
            components.Any(part => part.Length == 0 || !part.All(char.IsAsciiDigit) ||
                !int.TryParse(part, out _)))
            throw new InvalidDataException("The latest GitHub release has an invalid stable version tag.");

        var sha = await ResolvePublishedTagCommitAsync(http, tag, cancellationToken);

        if (!root.TryGetProperty("assets", out var assets) || assets.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException("The stable release contains no asset list.");

        foreach (var asset in assets.EnumerateArray())
        {
            if (ReadString(asset, "name") != $"{ArtifactName}.zip")
                continue;

            var size = ReadInt64(asset, "size");
            var rawDownloadUrl = ReadString(asset, "browser_download_url");
            if (size <= 0 || size > MaximumDownloadBytes ||
                !Uri.TryCreate(rawDownloadUrl, UriKind.Absolute, out var uri) ||
                uri.Scheme != Uri.UriSchemeHttps ||
                !string.Equals(uri.Host, "github.com", StringComparison.OrdinalIgnoreCase) ||
                !uri.IsDefaultPort || !string.IsNullOrEmpty(uri.UserInfo) ||
                !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment) ||
                !string.Equals(uri.AbsolutePath,
                    $"/{Repository}/releases/download/{tag}/{ArtifactName}.zip",
                    StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("The stable release archive has invalid size or download URL metadata.");

            // All current stable releases publish an authenticated GitHub SHA-256 digest.
            // Do not offer a stable update whose archive cannot be integrity-checked.
            var digest = ReadString(asset, "digest");
            if (!digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase) ||
                digest.Length != 71 || !digest[7..].All(Uri.IsHexDigit))
                throw new InvalidDataException("The stable release archive is missing a valid SHA-256 digest.");

            var assetId = ReadInt64(asset, "id");
            if (assetId <= 0)
                throw new InvalidDataException("The stable release archive has no valid GitHub asset ID.");

            return new LatestBuildInfo(
                sha, 0, 0,
                $"https://api.github.com/repos/{Repository}/releases/assets/{assetId}",
                size, digest.ToLowerInvariant(),
                ReadString(root, "published_at"), tag);
        }

        throw new InvalidOperationException("The latest stable release has no Windows x86 portable ZIP.");
    }

    private static async Task<string> ResolvePublishedTagCommitAsync(
        HttpClient http, string tag, CancellationToken cancellationToken)
    {
        var referenceUrl = $"https://api.github.com/repos/{Repository}/git/ref/tags/{tag}";
        using var response = await http.GetAsync(referenceUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reference = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        if (!reference.RootElement.TryGetProperty("object", out var target))
            throw new InvalidDataException("The release tag has no Git object.");

        // An annotated Git tag points to another tag object, while a lightweight
        // tag points directly to its commit. Limit recursion to prevent cycles.
        for (var depth = 0; depth < 3; depth++)
        {
            var type = ReadString(target, "type");
            var sha = ReadString(target, "sha");
            if (sha.Length != 40 || !sha.All(Uri.IsHexDigit))
                throw new InvalidDataException("The release tag points to an invalid Git object SHA.");
            if (type == "commit")
                return sha.ToLowerInvariant();
            if (type != "tag")
                throw new InvalidDataException("The stable release tag does not point to a commit.");

            var tagObjectUrl = $"https://api.github.com/repos/{Repository}/git/tags/{sha}";
            using var tagResponse = await http.GetAsync(tagObjectUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            tagResponse.EnsureSuccessStatusCode();
            await using var tagStream = await tagResponse.Content.ReadAsStreamAsync(cancellationToken);
            using var tagDocument = await JsonDocument.ParseAsync(tagStream, cancellationToken: cancellationToken);
            if (!tagDocument.RootElement.TryGetProperty("object", out var nestedTarget))
                throw new InvalidDataException("The annotated release tag has no target object.");
            // Clone before tagDocument is disposed.
            target = nestedTarget.Clone();
        }

        throw new InvalidDataException("The stable release tag has too many nested tag objects.");
    }

    public static Task<string> DownloadAndPrepareAsync(
        LatestBuildInfo build,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default) =>
        DownloadAndPrepareAsync(build, Http, progress, cancellationToken);

    internal static async Task<string> DownloadAndPrepareAsync(
        LatestBuildInfo build,
        HttpClient http,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Dvinholm",
            "UpdateStaging");
        Directory.CreateDirectory(root);

        var staging = Path.Combine(root, $"{build.CommitSha}-{Guid.NewGuid():N}");
        var archivePath = Path.Combine(Path.GetTempPath(), $"dvinholm-{build.CommitSha}-{Guid.NewGuid():N}.zip");
        var prepared = false;

        try
        {
            progress?.Report($"Downloading {build.DisplayVersion}…");
            using var downloadRequest = new HttpRequestMessage(HttpMethod.Get, build.DownloadUrl);
            if (Uri.TryCreate(build.DownloadUrl, UriKind.Absolute, out var downloadUri) &&
                string.Equals(downloadUri.Host, "api.github.com", StringComparison.OrdinalIgnoreCase) &&
                downloadUri.AbsolutePath.StartsWith($"/repos/{Repository}/releases/assets/", StringComparison.OrdinalIgnoreCase))
            {
                downloadRequest.Headers.Accept.Clear();
                downloadRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/octet-stream"));
            }

            using (var response = await http.SendAsync(downloadRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken))
            {
                if (!response.IsSuccessStatusCode)
                {
                    // Include only the URL path in diagnostics; never log query strings,
                    // which may contain temporary signed download tokens.
                    var failedPath = response.RequestMessage?.RequestUri?.GetLeftPart(UriPartial.Path)
                        ?? downloadUri?.GetLeftPart(UriPartial.Path)
                        ?? "unknown URL";
                    throw new HttpRequestException(
                        $"Release asset download failed with HTTP {(int)response.StatusCode} ({response.StatusCode}) from {failedPath}.",
                        null,
                        response.StatusCode);
                }
                if (response.Content.Headers.ContentLength is long length && length > MaximumDownloadBytes)
                    throw new InvalidDataException("The downloaded artifact exceeds the configured size limit.");

                await using var input = await response.Content.ReadAsStreamAsync(cancellationToken);
                await using var output = new FileStream(archivePath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true);
                var buffer = new byte[81920];
                long total = 0;
                // Report only newly crossed 10% milestones to avoid flooding
                // the WinUI dispatcher for every network read.
                var downloadProgress = new UpdateDownloadProgress(build.ArtifactSize);
                while (true)
                {
                    var read = await input.ReadAsync(buffer, cancellationToken);
                    if (read == 0) break;
                    total += read;
                    if (total > MaximumDownloadBytes)
                        throw new InvalidDataException("The downloaded artifact exceeds the configured size limit.");
                    await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                    var milestoneMessage = downloadProgress.ReportCopied(total, build.DisplayVersion);
                    if (milestoneMessage is not null)
                        progress?.Report(milestoneMessage);
                }
            }

            var downloadedSize = new FileInfo(archivePath).Length;
            if (downloadedSize != build.ArtifactSize)
                throw new InvalidDataException("The downloaded artifact size does not match GitHub's metadata.");

            if (build.ArtifactDigest is not null)
            {
                progress?.Report("Verifying the downloaded artifact checksum…");
                await using var checksumStream = File.OpenRead(archivePath);
                var checksum = Convert.ToHexString(await SHA256.HashDataAsync(checksumStream, cancellationToken)).ToLowerInvariant();
                if (!string.Equals("sha256:" + checksum, build.ArtifactDigest, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("The downloaded artifact checksum does not match GitHub's digest.");
            }

            progress?.Report("Validating and extracting the build…");
            await ValidateAndExtractArchiveAsync(archivePath, staging, cancellationToken);

            if (!File.Exists(Path.Combine(staging, "Dvinholm.App.exe")))
                throw new InvalidDataException("The downloaded artifact does not contain Dvinholm.App.exe.");

            prepared = true;
            return staging;
        }
        finally
        {
            if (!prepared)
            {
                TryCleanup(() =>
                {
                    if (Directory.Exists(staging))
                        Directory.Delete(staging, recursive: true);
                });
            }

            TryCleanup(() =>
            {
                if (File.Exists(archivePath))
                    File.Delete(archivePath);
            });
        }
    }

    // Extract each file asynchronously on a worker to keep WinUI responsive
    // *and* allow Cancel download to stop a large individual ZIP entry.
    // Validate every entry before writing any file to the private stage.
    internal static Task ValidateAndExtractArchiveAsync(
        string archivePath,
        string staging,
        CancellationToken cancellationToken = default) =>
        Task.Run(async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var archive = ZipFile.OpenRead(archivePath);
            // Reject excessive file/dir entries before creating private
            // staging directories or writing any extracted file.
            ValidateArchiveEntryCount(archive.Entries.Count);
            ValidateExpandedSize(archive.Entries.Select(entry => entry.Length));
            cancellationToken.ThrowIfCancellationRequested();
            Directory.CreateDirectory(staging);
            var fullRoot = Path.GetFullPath(staging) + Path.DirectorySeparatorChar;
            foreach (var entry in archive.Entries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var target = Path.GetFullPath(Path.Combine(staging, entry.FullName));
                if (!target.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("The artifact contains an unsafe archive path.");
            }

            cancellationToken.ThrowIfCancellationRequested();
            foreach (var entry in archive.Entries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var target = Path.GetFullPath(Path.Combine(staging, entry.FullName));
                if (entry.FullName.EndsWith("/", StringComparison.Ordinal))
                {
                    Directory.CreateDirectory(target);
                    continue;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                // CreateNew matches the original ZIP extractor's non-overwrite
                // behavior, even if a crafted ZIP repeats the same file name.
                // Closing the output stream precedes restoring ZIP timestamps.
                using (var input = entry.Open())
                {
                    await using (var output = new FileStream(
                        target, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                        81920, useAsync: true))
                    {
                        // The ZIP's claimed uncompressed entry size is only
                        // metadata. Enforce it against actual streamed output
                        // so a forged length cannot bypass the aggregate cap.
                        await CopyArchiveEntryAsync(input, output, entry.Length, cancellationToken);
                    }
                }
                File.SetLastWriteTime(target, entry.LastWriteTime.DateTime);
            }

            cancellationToken.ThrowIfCancellationRequested();
        }, cancellationToken);

    // Per-entry streaming verifies the *actual* expanded bytes, not only
    // the lengths recorded in ZIP metadata. The existing aggregate bound
    // pre-validates metadata before any extraction starts.
    internal static async Task CopyArchiveEntryAsync(
        Stream input, Stream output, long declaredLength, CancellationToken cancellationToken)
    {
        if (declaredLength < 0 || declaredLength > MaximumExpandedBytes)
            throw new InvalidDataException("The archive contains an invalid expanded entry size.");

        var buffer = new byte[81920];
        long written = 0;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var read = await input.ReadAsync(buffer.AsMemory(), cancellationToken);
            if (read == 0)
                break;

            // Compare with the remaining allowance to avoid overflowing long.
            // Reject this chunk before it is written outside the metadata budget.
            if (read > declaredLength - written)
                throw new InvalidDataException("An archive entry expands beyond its declared size.");

            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            written += read;
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (written != declaredLength)
            throw new InvalidDataException("An archive entry is shorter than its declared size.");
    }

    internal static void TryCleanup(Action cleanup)
    {
        try
        {
            cleanup();
        }
        catch (IOException)
        {
            // Best-effort cleanup must not replace the original download/extraction error.
        }
        catch (UnauthorizedAccessException)
        {
            // A transient ACL/lock issue must not hide the original failure either.
        }
    }

    internal static void ValidateArchiveEntryCount(int count)
    {
        if (count < 0 || count > MaximumArchiveEntries)
            throw new InvalidDataException("The update archive contains too many files or directories.");
    }

    internal static void ValidateExpandedSize(IEnumerable<long> entryLengths)
    {
        long expandedBytes = 0;
        foreach (var entryLength in entryLengths)
        {
            if (entryLength < 0)
                throw new InvalidDataException("The artifact contains an invalid entry size.");

            // Compare against the remaining budget before adding, so hostile ZIP64
            // lengths cannot overflow the aggregate counter.
            if (entryLength > MaximumExpandedBytes - expandedBytes)
                throw new InvalidDataException("The extracted artifact exceeds the configured size limit.");

            expandedBytes += entryLength;
        }
    }

    public static Process StartReplacement(string stagingDirectory, string destinationDirectory, int processId)
    {
        string? scriptPath = null;
        try
        {
            var localRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Dvinholm");
            var destination = Path.GetFullPath(destinationDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            Directory.CreateDirectory(localRoot);

            scriptPath = Path.Combine(Path.GetTempPath(), $"dvinholm-update-{Guid.NewGuid():N}.ps1");
            var backupDirectory = Path.Combine(localRoot, "UpdateBackups", Guid.NewGuid().ToString("N"));
            var script = BuildReplacementScript(stagingDirectory, destination, processId, scriptPath, backupDirectory);

            var startInfo = new ProcessStartInfo
            {
                FileName = Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe"),
                WorkingDirectory = Path.GetTempPath(),
                UseShellExecute = false,
                CreateNoWindow = true
            };
            startInfo.ArgumentList.Add("-NoProfile");
            startInfo.ArgumentList.Add("-ExecutionPolicy");
            startInfo.ArgumentList.Add("Bypass");
            startInfo.ArgumentList.Add("-WindowStyle");
            startInfo.ArgumentList.Add("Hidden");
            startInfo.ArgumentList.Add("-File");
            startInfo.ArgumentList.Add(scriptPath);

            File.WriteAllText(scriptPath, script, new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            return Process.Start(startInfo) ?? throw new InvalidOperationException("Windows could not start the update helper.");
        }
        catch
        {
            CleanupFailedStart(scriptPath, stagingDirectory);
            throw;
        }
    }

    internal static void CleanupFailedStart(string? scriptPath, string stagingDirectory)
    {
        if (!string.IsNullOrEmpty(scriptPath))
        {
            try { File.Delete(scriptPath); } catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        try
        {
            if (Directory.Exists(stagingDirectory))
                Directory.Delete(stagingDirectory, recursive: true);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    // Backup leaf names are not globally unique: independent installations
    // may use the same backup directory name. Derive a durable, case-insensitive
    // identity from its entire canonical Windows path. A running guard and a
    // sign-in recovery generated for the same transaction must agree.
    internal static string RecoveryTransactionId(string backupDirectory)
    {
        var normalized = Path.GetFullPath(backupDirectory)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .ToUpperInvariant();
        var hash = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(normalized));
        return Convert.ToHexString(hash)[..24];
    }

    internal static string BuildReplacementScript(
        string stagingDirectory,
        string destinationDirectory,
        int processId,
        string scriptPath,
        string backupDirectory)
    {
        var source = EscapePowerShellLiteral(Path.GetFullPath(stagingDirectory));
        var destination = EscapePowerShellLiteral(Path.GetFullPath(destinationDirectory));
        var backup = EscapePowerShellLiteral(Path.GetFullPath(backupDirectory));
        var escapedScript = EscapePowerShellLiteral(Path.GetFullPath(scriptPath));
        var recoveryId = RecoveryTransactionId(backupDirectory);
        return $@"
param([switch]$Watch, [int]$HelperPid = 0, [switch]$RecoverOnLogon)
$ErrorActionPreference = 'Stop'
$source = '{source}'
$destination = '{destination}'
$backup = '{backup}'
$processId = {processId}
$scriptPath = '{escapedScript}'
$logPath = Join-Path $env:LOCALAPPDATA 'Dvinholm\update.log'
# These checkpoint files live outside the installed application and
# outside the mirrored old-build backup.
$stateDir = $backup + '.state'
$guardReady = Join-Path $stateDir 'guard.ready'
$existingMarker = Join-Path $stateDir 'previous.pending'
$newMarker = Join-Path $stateDir 'first-install.pending'
# The login recovery file and HKCU Run value survive a reboot. They are
# separate from the directory mirrored by Robocopy and from temporary files.
$recoveryScript = Join-Path $stateDir 'resume.ps1'
$helperIdentity = Join-Path $stateDir 'helper.identity'
$backupManifest = Join-Path $stateDir 'backup.sha256'
$runKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
$runName = 'DvinholmRecovery-{recoveryId}'

# Robocopy classifies files by size and timestamps. Even with /IS /IT,
# identical metadata is not proof of identical content. After the mirror,
# verify every staged file and copy mismatched bytes explicitly. Failure
# here enters the existing exception rollback path before app startup.
function GetSha256File {{
    param([string]$path)
    $sha = [System.Security.Cryptography.SHA256]::Create()
    try {{
        $stream = [System.IO.File]::OpenRead($path)
        try {{
            return [System.BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-', '')
        }}
        finally {{ $stream.Dispose() }}
    }}
    finally {{ $sha.Dispose() }}
}}

# New 'pending-v2' checkpoints bind the backup to a full-tree digest.
# Older 'pending' checkpoints retain their legacy recovery behavior.
function GetBackupTreeHash {{
    param([string]$root)
    if (-not (Test-Path -LiteralPath $root -PathType Container)) {{
        throw 'Recovery backup directory is unavailable; checkpoint preserved.'
    }}
    $entries = [System.Collections.Generic.List[string]]::new()
    $prefix = $root.TrimEnd('\').Length
    foreach ($directory in @(Get-ChildItem -LiteralPath $root -Recurse -Directory -Force -ErrorAction Stop)) {{
        $relative = $directory.FullName.Substring($prefix).TrimStart('\')
        $encoded = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($relative))
        [void]$entries.Add('D:' + $encoded)
    }}
    foreach ($file in @(Get-ChildItem -LiteralPath $root -Recurse -File -Force -ErrorAction Stop)) {{
        $relative = $file.FullName.Substring($prefix).TrimStart('\')
        $encoded = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($relative))
        [void]$entries.Add('F:' + $encoded + ':' + (GetSha256File $file.FullName))
    }}
    $entries.Sort([System.StringComparer]::Ordinal)
    $canonical = [string]::Join([string][char]10, $entries.ToArray())
    $sha = [System.Security.Cryptography.SHA256]::Create()
    try {{
        return [BitConverter]::ToString(
            $sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($canonical))).Replace('-', '')
    }}
    finally {{ $sha.Dispose() }}
}}

function WriteBackupTreeManifest {{
    $digest = GetBackupTreeHash $backup
    $digest | Set-Content -LiteralPath $backupManifest -Encoding ASCII -ErrorAction Stop
    AssertBackupTreeManifest
}}

function AssertBackupTreeManifest {{
    if (-not (Test-Path -LiteralPath $backupManifest -PathType Leaf)) {{
        throw 'Recovery backup SHA-256 manifest is missing; checkpoint preserved.'
    }}
    $expected = (Get-Content -LiteralPath $backupManifest -Raw -ErrorAction Stop).Trim()
    if ($expected -cnotmatch '^[0-9A-F]{{64}}$') {{
        throw 'Recovery backup SHA-256 manifest is invalid; checkpoint preserved.'
    }}
    if ($expected -cne (GetBackupTreeHash $backup)) {{
        throw 'Recovery backup SHA-256 integrity mismatch; checkpoint preserved.'
    }}
}}

function EnsureMirroredBytes {{
    param([string]$expectedRoot, [string]$actualRoot)
    $expectedFiles = @(Get-ChildItem -LiteralPath $expectedRoot -Recurse -File -Force)
    $prefixLength = $expectedRoot.TrimEnd('\').Length
    foreach ($expectedFile in $expectedFiles) {{
        $relative = $expectedFile.FullName.Substring($prefixLength).TrimStart('\')
        $actualFile = Join-Path $actualRoot $relative
        $expectedHash = (GetSha256File $expectedFile.FullName)
        $actualHash = $null
        if (Test-Path -LiteralPath $actualFile -PathType Leaf) {{
            $actualHash = (GetSha256File $actualFile)
        }}
        if ($expectedHash -ne $actualHash) {{
            $actualFolder = Split-Path -Path $actualFile -Parent
            New-Item -ItemType Directory -Path $actualFolder -Force | Out-Null
            Copy-Item -LiteralPath $expectedFile.FullName -Destination $actualFile -Force -ErrorAction Stop
            $actualHash = (GetSha256File $actualFile)
            if ($expectedHash -ne $actualHash) {{
                throw ('The mirrored build failed SHA-256 verification: ' + $relative)
            }}
        }}
    }}
    # Files are not the entire install manifest: empty directories can
    # also be left behind (or missing) after an incomplete mirror. Refuse
    # to commit a tree whose relative directory names differ.
    $actualFiles = @(Get-ChildItem -LiteralPath $actualRoot -Recurse -File -Force)
    if ($actualFiles.Count -ne $expectedFiles.Count) {{
        throw 'The mirrored build contains missing or unexpected files.'
    }}
    $expectedDirectoryNames = [System.Collections.Generic.HashSet[string]]::new(
        [System.StringComparer]::OrdinalIgnoreCase)
    $actualDirectoryNames = [System.Collections.Generic.HashSet[string]]::new(
        [System.StringComparer]::OrdinalIgnoreCase)
    $expectedPrefix = $expectedRoot.TrimEnd('\').Length
    $actualPrefix = $actualRoot.TrimEnd('\').Length
    foreach ($directory in @(Get-ChildItem -LiteralPath $expectedRoot -Recurse -Directory -Force)) {{
        [void]$expectedDirectoryNames.Add(
            $directory.FullName.Substring($expectedPrefix).TrimStart('\'))
    }}
    foreach ($directory in @(Get-ChildItem -LiteralPath $actualRoot -Recurse -Directory -Force)) {{
        [void]$actualDirectoryNames.Add(
            $directory.FullName.Substring($actualPrefix).TrimStart('\'))
    }}
    if (-not $expectedDirectoryNames.SetEquals($actualDirectoryNames)) {{
        throw 'The mirrored build contains missing or unexpected directories.'
    }}
}}

function ClearStartupRecovery {{
    try {{
        if (Get-ItemProperty -Path $runKey -Name $runName -ErrorAction SilentlyContinue) {{
            Remove-ItemProperty -Path $runKey -Name $runName -ErrorAction Stop
        }}
        return $true
    }}
    catch {{
        # If removing the auto-start entry fails, retain the recovery script
        # and checkpoint directory so the next login can retry cleanup.
        try {{
            ('Could not unregister updater login recovery: ' + ($_ | Out-String)) |
                Add-Content -LiteralPath $logPath
        }} catch {{ }}
        return $false
    }}
}}

function FinishRecoveryTransaction {{
    if (ClearStartupRecovery) {{
        Remove-Item -LiteralPath $backup -Recurse -Force -ErrorAction SilentlyContinue
        Remove-Item -LiteralPath $stateDir -Recurse -Force -ErrorAction SilentlyContinue
    }}
}}

if ($Watch -or $RecoverOnLogon) {{
    # An independent PowerShell process watches the update helper. Unlike a
    # normal catch block it still runs if that helper is terminated abruptly.
    try {{
        if ($Watch) {{
            if ($HelperPid -le 0) {{ throw 'Update recovery guard received an invalid process ID.' }}
            New-Item -ItemType Directory -Path $stateDir -Force | Out-Null
            'ready' | Set-Content -LiteralPath $guardReady -Encoding ASCII
            try {{
                if (Get-Process -Id $HelperPid -ErrorAction SilentlyContinue) {{
                    Wait-Process -Id $HelperPid -ErrorAction Stop
                }}
            }}
            catch {{
                if (Get-Process -Id $HelperPid -ErrorAction SilentlyContinue) {{ throw }}
            }}
        }}
        elseif (Test-Path -LiteralPath $helperIdentity) {{
            # A corrupted identity might conceal an active updater helper.
            # Preserve the checkpoint rather than restoring over that helper.
            if (-not (Test-Path -LiteralPath $helperIdentity -PathType Leaf)) {{
                throw 'Recovery helper identity is not a regular file; checkpoint preserved.'
            }}
            $identityText = (Get-Content -LiteralPath $helperIdentity -Raw -ErrorAction Stop).Trim()
            if ($identityText -cnotmatch '^[0-9]+[|][0-9]+$') {{
                throw 'Recovery helper identity is malformed; checkpoint preserved.'
            }}
            $identity = $identityText.Split('|')
            $identityPid = 0
            $identityTicks = [long]0
            if (-not [int]::TryParse($identity[0], [ref]$identityPid) -or
                -not [long]::TryParse($identity[1], [ref]$identityTicks) -or
                $identityPid -le 0 -or $identityTicks -le 0) {{
                throw 'Recovery helper identity is invalid; checkpoint preserved.'
            }}
            # A reused PID after reboot differs by process start time.
            $activeHelper = Get-Process -Id $identityPid -ErrorAction SilentlyContinue
            if ($null -ne $activeHelper -and
                $activeHelper.StartTime.ToUniversalTime().Ticks -eq $identityTicks) {{
                exit 0
            }}
        }}

        # Both the live guard and a login recovery can awaken near the same
        # time. Only one may mirror files or clear the transaction checkpoint.
        $mutexName = 'Local\DvinholmRecovery-{recoveryId}'
        $recoveryMutex = [System.Threading.Mutex]::new($false, $mutexName)
        try {{
            $haveRecoveryLock = $recoveryMutex.WaitOne(0)
        }}
        catch [System.Threading.AbandonedMutexException] {{
            # The previous guard may have been killed mid-restore. Windows
            # grants this mutex to us; retry the pending recovery instead of
            # mistaking the abandoned lock for a permanent failure.
            $haveRecoveryLock = $true
        }}
        if (-not $haveRecoveryLock) {{ exit 0 }}

        # Never infer a recovery mode from conflicting or damaged checkpoint
        # files. Both markers are mutually exclusive; an invalid marker must
        # preserve the backup and destination for inspection or a safe retry.
        $hasPreviousMarker = Test-Path -LiteralPath $existingMarker
        $hasFirstMarker = Test-Path -LiteralPath $newMarker
        if ($hasPreviousMarker -and $hasFirstMarker) {{
            throw 'Recovery checkpoint is ambiguous: both pending markers exist.'
        }}
        if ($hasPreviousMarker -or $hasFirstMarker) {{
            $activeMarker = if ($hasPreviousMarker) {{ $existingMarker }} else {{ $newMarker }}
            if (-not (Test-Path -LiteralPath $activeMarker -PathType Leaf)) {{
                throw 'Recovery checkpoint is not a regular file.'
            }}
            $markerValue = [string](Get-Content -LiteralPath $activeMarker -Raw -ErrorAction Stop)
            $checkpointFormat = $markerValue.Trim()
            if ($checkpointFormat -cne 'pending' -and
                (-not $hasPreviousMarker -or $checkpointFormat -cne 'pending-v2')) {{
                throw 'Recovery checkpoint has invalid contents; preserve it for inspection.'
            }}
        }}
        if ($hasPreviousMarker) {{
            $previousExe = Join-Path $backup 'Dvinholm.App.exe'
            # Reject missing, directory-shaped or truncated backup executables
            # BEFORE Robocopy can delete/overwrite the installed files.
            if (-not (Test-Path -LiteralPath $previousExe -PathType Leaf)) {{
                throw 'Recovery backup is missing a regular Dvinholm.App.exe file; checkpoint preserved.'
            }}
            $backupExeInfo = Get-Item -LiteralPath $previousExe -ErrorAction Stop
            if ($backupExeInfo.Length -le 0) {{
                throw 'Recovery backup Dvinholm.App.exe is empty; checkpoint preserved.'
            }}
            if ($checkpointFormat -ceq 'pending-v2') {{
                AssertBackupTreeManifest
            }}
            New-Item -ItemType Directory -Path $destination -Force | Out-Null
            & (Join-Path $env:SystemRoot 'System32\robocopy.exe') $backup $destination /MIR /IS /IT /R:2 /W:1 /NFL /NDL /NJH /NJS
            if ($LASTEXITCODE -ge 8) {{
                throw ""Recovery mirror failed with robocopy exit code $LASTEXITCODE.""
            }}
            $restoredExe = Join-Path $destination 'Dvinholm.App.exe'
            if (-not (Test-Path -LiteralPath $restoredExe)) {{
                throw 'Recovery did not restore Dvinholm.App.exe.'
            }}
            EnsureMirroredBytes $backup $destination
            $restoredProcess = Start-Process -FilePath $restoredExe -WorkingDirectory $destination -PassThru
            Start-Sleep -Seconds 5
            if ($restoredProcess.HasExited) {{
                throw ""Recovered build exited immediately (exit code $($restoredProcess.ExitCode)).""
            }}
            # This checkpoint is cleared only after the restored app is running.
            Remove-Item -LiteralPath $existingMarker -Force -ErrorAction Stop
            'Updater helper exited with a pending checkpoint; previous build recovered.' |
                Add-Content -LiteralPath $logPath
            FinishRecoveryTransaction
        }}
        elseif ($hasFirstMarker) {{
            # First install has nothing to roll back. Never leave a partial
            # destination directory looking like a completed installation.
            if (Test-Path -LiteralPath $destination) {{
                Remove-Item -LiteralPath $destination -Recurse -Force -ErrorAction Stop
            }}
            Remove-Item -LiteralPath $newMarker -Force -ErrorAction Stop
            'Updater helper exited with a partial first installation; destination removed.' |
                Add-Content -LiteralPath $logPath
            FinishRecoveryTransaction
        }}
        else {{
            # The helper ended before arming a pending marker, so no app files
            # were replaced. Clean only abandoned transaction metadata.
            FinishRecoveryTransaction
        }}
    }}
    catch {{
        # Retain rollback checkpoint and the old build for manual recovery if
        # the independent guard itself cannot complete the restore.
        try {{
            ('Updater recovery guard failed; checkpoint preserved: ' + ($_ | Out-String)) |
                Add-Content -LiteralPath $logPath
        }}
        catch {{ }}
    }}
    finally {{
        if ($haveRecoveryLock) {{ $recoveryMutex.ReleaseMutex() }}
        if ($null -ne $recoveryMutex) {{ $recoveryMutex.Dispose() }}
    }}
    exit
}}

$hadDestination = Test-Path -LiteralPath $destination
$backupReady = $false
$replacementStarted = $false
$rollbackFailed = $false
try {{
    if (Get-Process -Id $processId -ErrorAction SilentlyContinue) {{
        # Do not let a hung app process block the updater helper indefinitely.
        try {{
            Wait-Process -Id $processId -Timeout 60 -ErrorAction Stop
        }}
        catch {{
            # The app may exit between Get-Process and Wait-Process. Continue only
            # if it is now gone; if it still exists, preserve the timeout/error.
            if (Get-Process -Id $processId -ErrorAction SilentlyContinue) {{
                throw
            }}
        }}
    }}
    Start-Sleep -Milliseconds 400
    if (-not (Test-Path -LiteralPath (Join-Path $source 'Dvinholm.App.exe'))) {{
        throw 'The staged update is missing Dvinholm.App.exe.'
    }}
    if ($hadDestination) {{
        New-Item -ItemType Directory -Path $backup -Force | Out-Null
        & (Join-Path $env:SystemRoot 'System32\robocopy.exe') $destination $backup /MIR /R:2 /W:1 /NFL /NDL /NJH /NJS
        if ($LASTEXITCODE -ge 8) {{
            throw ""Could not back up the current build (robocopy exit code $LASTEXITCODE).""
        }}
        $backedUpExe = Join-Path $backup 'Dvinholm.App.exe'
        if (-not (Test-Path -LiteralPath $backedUpExe -PathType Leaf)) {{
            throw 'The backup does not contain a regular Dvinholm.App.exe file; replacement was not started.'
        }}
        if ((Get-Item -LiteralPath $backedUpExe -ErrorAction Stop).Length -le 0) {{
            throw 'The backup executable is empty; replacement was not started.'
        }}
        # Save the trusted old-build tree digest before arming any checkpoint.
        New-Item -ItemType Directory -Path $stateDir -Force | Out-Null
        WriteBackupTreeManifest
        $backupReady = $true
    }}

    # Arm the external recovery guard *before* the first destructive mirror.
    # If the guard cannot start and acknowledge readiness, leave the installed
    # build untouched and fail safely.
    New-Item -ItemType Directory -Path $stateDir -Force | Out-Null
    $guardArguments = @(
        '-NoProfile', '-ExecutionPolicy', 'Bypass',
        '-WindowStyle', 'Hidden', '-File', ('""' + $scriptPath + '""'),
        '-Watch', '-HelperPid', [string]$PID
    )
    $guardProcess = Start-Process -FilePath (Join-Path $PSHOME 'powershell.exe') -ArgumentList $guardArguments -PassThru -WindowStyle Hidden
    if ($null -eq $guardProcess) {{ throw 'Could not start the independent update recovery guard.' }}
    for ($attempt = 0; $attempt -lt 100 -and -not (Test-Path -LiteralPath $guardReady); $attempt++) {{
        if ($guardProcess.HasExited) {{ throw 'Update recovery guard exited before acknowledging readiness.' }}
        Start-Sleep -Milliseconds 100
    }}
    if (-not (Test-Path -LiteralPath $guardReady)) {{
        throw 'Update recovery guard did not acknowledge readiness.'
    }}

    # Persist a new recovery script and register a per-update HKCU Run value
    # BEFORE arming a checkpoint or starting any destructive file replacement.
    # Registration failure aborts the update while the prior build is intact.
    $helperStartTicks = (Get-Process -Id $PID -ErrorAction Stop).StartTime.ToUniversalTime().Ticks
    ""$PID|$helperStartTicks"" | Set-Content -LiteralPath $helperIdentity -Encoding ASCII
    Copy-Item -LiteralPath $scriptPath -Destination $recoveryScript -Force -ErrorAction Stop
    # Verify the durable login helper before arming the pending checkpoint.
    if ((GetSha256File $scriptPath) -ne (GetSha256File $recoveryScript)) {{
        throw 'Recovery script copy failed SHA-256 verification.'
    }}
    $quote = [char]34
    $runCommand = $quote + (Join-Path $PSHOME 'powershell.exe') + $quote +
        ' -NoProfile -NonInteractive -ExecutionPolicy Bypass -WindowStyle Hidden -File ' +
        $quote + $recoveryScript + $quote + ' -RecoverOnLogon'
    New-ItemProperty -Path $runKey -Name $runName -Value $runCommand -PropertyType String -Force -ErrorAction Stop | Out-Null

    $pendingMarker = if ($hadDestination) {{ $existingMarker }} else {{ $newMarker }}
    $markerFormat = if ($hadDestination) {{ 'pending-v2' }} else {{ 'pending' }}
    $markerFormat | Set-Content -LiteralPath $pendingMarker -Encoding ASCII

    New-Item -ItemType Directory -Path $destination -Force | Out-Null
    $replacementStarted = $true
    & (Join-Path $env:SystemRoot 'System32\robocopy.exe') $source $destination /MIR /IS /IT /R:2 /W:1 /NFL /NDL /NJH /NJS
    if ($LASTEXITCODE -ge 8) {{
        throw ""Robocopy failed with exit code $LASTEXITCODE.""
    }}
    EnsureMirroredBytes $source $destination
    $targetExe = Join-Path $destination 'Dvinholm.App.exe'
    if (-not (Test-Path -LiteralPath $targetExe)) {{
        throw 'The updated executable is missing after file replacement.'
    }}
    $updatedProcess = Start-Process -FilePath $targetExe -WorkingDirectory $destination -PassThru
    Start-Sleep -Seconds 5
    if ($updatedProcess.HasExited) {{
        throw ""The updated application exited during startup (exit code $($updatedProcess.ExitCode)).""
    }}
    $oldBuilds = Join-Path $env:LOCALAPPDATA 'Dvinholm\Builds'
    if (Test-Path -LiteralPath $oldBuilds) {{
        Get-ChildItem -LiteralPath $oldBuilds -Directory |
            Where-Object {{ $_.FullName -ne $destination }} |
            Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
    }}
    $stagingRoot = Join-Path $env:LOCALAPPDATA 'Dvinholm\UpdateStaging'
    if (Test-Path -LiteralPath $stagingRoot) {{
        Get-ChildItem -LiteralPath $stagingRoot -Directory |
            Where-Object {{ $_.FullName -ne $source }} |
            Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
    }}
    # Startup succeeded: disarm the guard BEFORE deleting the old build.
    Remove-Item -LiteralPath $pendingMarker -Force -ErrorAction Stop
    FinishRecoveryTransaction
    Remove-Item -LiteralPath $source -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $scriptPath -Force -ErrorAction SilentlyContinue
}}
catch {{
    $failure = ($_ | Out-String)
    if ($replacementStarted -and $hadDestination -and $backupReady) {{
        try {{
            # Do not mirror corrupted backup bytes during exception rollback.
            AssertBackupTreeManifest
            & (Join-Path $env:SystemRoot 'System32\robocopy.exe') $backup $destination /MIR /IS /IT /R:2 /W:1 /NFL /NDL /NJH /NJS
            if ($LASTEXITCODE -ge 8) {{
                throw ""Rollback copy failed with robocopy exit code $LASTEXITCODE.""
            }}
            $restoredExe = Join-Path $destination 'Dvinholm.App.exe'
            if (-not (Test-Path -LiteralPath $restoredExe)) {{
                throw 'Rollback completed without restoring Dvinholm.App.exe.'
            }}
            EnsureMirroredBytes $backup $destination
            $failure += [Environment]::NewLine + 'The previous build was restored and will be restarted.'
            $restoredProcess = Start-Process -FilePath $restoredExe -WorkingDirectory $destination -PassThru
            Start-Sleep -Seconds 5
            if ($restoredProcess.HasExited) {{
                throw ""The restored application exited during startup (exit code $($restoredProcess.ExitCode)).""
            }}
            # Restore is verified running; the guard must not roll it back again.
            Remove-Item -LiteralPath $existingMarker -Force -ErrorAction Stop
            FinishRecoveryTransaction
        }}
        catch {{
            $rollbackFailed = $true
            $failure += [Environment]::NewLine + 'Rollback failed: ' + ($_ | Out-String)
        }}
    }}
    elseif ($replacementStarted -and -not $hadDestination) {{
        # A first install has no previous build to restore. Remove any partial
        # destination so a failed copy cannot leave a seemingly installed app.
        try {{
            if (Test-Path -LiteralPath $destination) {{
                Remove-Item -LiteralPath $destination -Recurse -Force -ErrorAction Stop
            }}
            if (Test-Path -LiteralPath $destination) {{
                throw 'The incomplete destination still exists after cleanup.'
            }}
            # A clean first-install cleanup can also clear its recovery guard.
            if (Test-Path -LiteralPath $newMarker) {{
                Remove-Item -LiteralPath $newMarker -Force -ErrorAction Stop
            }}
            FinishRecoveryTransaction
            $failure += [Environment]::NewLine + 'The incomplete first-install destination was removed.'
        }}
        catch {{
            $failure += [Environment]::NewLine + 'Could not remove the incomplete first-install destination: ' + ($_ | Out-String)
        }}
    }}
    elseif (-not $replacementStarted -and (Test-Path -LiteralPath $backup)) {{
        # The installed app is still untouched. A failed or incomplete backup
        # is not usable for rollback, so remove it instead of leaking partial data.
        try {{
            FinishRecoveryTransaction
            $failure += [Environment]::NewLine + 'The incomplete update backup was removed because replacement had not started.'
        }}
        catch {{
            $failure += [Environment]::NewLine + 'Could not remove the incomplete update backup: ' + ($_ | Out-String)
        }}
    }}
    if (-not $rollbackFailed) {{
        try {{
            if (Test-Path -LiteralPath $source) {{
                Remove-Item -LiteralPath $source -Recurse -Force -ErrorAction Stop
            }}
        }}
        catch {{
            $failure += [Environment]::NewLine + 'Could not remove failed update staging directory: ' + ($_ | Out-String)
        }}
    }}
    try {{
        if (Test-Path -LiteralPath $scriptPath) {{
            Remove-Item -LiteralPath $scriptPath -Force -ErrorAction Stop
        }}
    }}
    catch {{
        $failure += [Environment]::NewLine + 'Could not remove update helper script: ' + ($_ | Out-String)
    }}
    $failure | Set-Content -LiteralPath $logPath -Encoding UTF8
    Start-Process -FilePath 'notepad.exe' -ArgumentList @($logPath)
}}
";
    }

    private static string EscapePowerShellLiteral(string value) => value.Replace("'", "''");

    private static string ReadString(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;

    private static long ReadInt64(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.TryGetInt64(out var result) ? result : 0;

    private static bool ReadBoolean(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.True;
}
