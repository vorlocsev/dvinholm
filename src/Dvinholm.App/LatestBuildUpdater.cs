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
    string CreatedAt)
{
    public string ShortSha => CommitSha.Length >= 7 ? CommitSha[..7] : CommitSha;
    public string DisplayVersion => $"CI #{RunNumber} · {ShortSha}";
}

internal static class LatestBuildUpdater
{
    private const string Repository = "vorlocsev/dvinholm";
    private const long WorkflowId = 378992585;
    private const string ArtifactName = "Dvinholm-Windows-x86";
    private const long MaximumDownloadBytes = 500L * 1024 * 1024;
    private const long MaximumExpandedBytes = 1_000L * 1024 * 1024;
    private static readonly HttpClient Http = CreateHttpClient();

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Dvinholm", "0.1.0"));
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

    public static string CurrentDisplayVersion
    {
        get
        {
            var value = CurrentVersion;
            var plus = value.LastIndexOf('+');
            var sha = GetEmbeddedCommitSha(value, plus);
            var runNumber = CurrentCiRunNumber;
            if (sha is not null && runNumber is int ciRun)
                return $"CI #{ciRun} · {sha[..7]}";

            return sha is not null
                ? $"{value[..plus]} · {sha[..7]}"
                : value;
        }
    }

    public static string CurrentWindowTitle =>
        CurrentCiRunNumber is int runNumber ? $"Dvinholm CI #{runNumber}" : "Dvinholm";

    public static int? CurrentCiRunNumber
    {
        get
        {
            var value = CurrentVersion;
            var plus = value.LastIndexOf('+');
            if (plus < 0)
                return null;

            var suffix = value[(plus + 1)..];
            var marker = suffix.LastIndexOf(".ci", StringComparison.OrdinalIgnoreCase);
            return marker >= 0 && int.TryParse(suffix[(marker + 3)..], out var runNumber) && runNumber > 0
                ? runNumber
                : null;
        }
    }

    public static string? CurrentCommitSha => GetEmbeddedCommitSha(CurrentVersion, CurrentVersion.LastIndexOf('+'));

    internal static bool IsUpdateAvailable(string? currentCommitSha, LatestBuildInfo latestBuild) =>
        string.IsNullOrWhiteSpace(currentCommitSha) ||
        !string.Equals(currentCommitSha, latestBuild.CommitSha, StringComparison.OrdinalIgnoreCase);

    private static string? GetEmbeddedCommitSha(string value, int plus)
    {
        if (plus < 0)
            return null;

        var suffix = value[(plus + 1)..];
        var marker = suffix.IndexOf(".ci", StringComparison.OrdinalIgnoreCase);
        var sha = marker >= 0 ? suffix[..marker] : suffix;
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
                        !uri.AbsolutePath.StartsWith($"/{Repository}/releases/download/", StringComparison.OrdinalIgnoreCase))
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
            progress?.Report($"Downloading CI build #{build.RunNumber}…");
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
                while (true)
                {
                    var read = await input.ReadAsync(buffer, cancellationToken);
                    if (read == 0) break;
                    total += read;
                    if (total > MaximumDownloadBytes)
                        throw new InvalidDataException("The downloaded artifact exceeds the configured size limit.");
                    await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
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
            Directory.CreateDirectory(staging);
            using (var archive = ZipFile.OpenRead(archivePath))
            {
                ValidateExpandedSize(archive.Entries.Select(entry => entry.Length));
                var fullRoot = Path.GetFullPath(staging) + Path.DirectorySeparatorChar;
                foreach (var entry in archive.Entries)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var target = Path.GetFullPath(Path.Combine(staging, entry.FullName));
                    if (!target.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("The artifact contains an unsafe archive path.");
                }

                archive.ExtractToDirectory(staging);
            }

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
        return $@"
$ErrorActionPreference = 'Stop'
$source = '{source}'
$destination = '{destination}'
$backup = '{backup}'
$processId = {processId}
$scriptPath = '{escapedScript}'
$logPath = Join-Path $env:LOCALAPPDATA 'Dvinholm\update.log'
$hadDestination = Test-Path -LiteralPath $destination
$backupReady = $false
$replacementStarted = $false
try {{
    Wait-Process -Id $processId -ErrorAction SilentlyContinue
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
        if (-not (Test-Path -LiteralPath (Join-Path $backup 'Dvinholm.App.exe'))) {{
            throw 'The backup does not contain Dvinholm.App.exe; replacement was not started.'
        }}
        $backupReady = $true
    }}
    New-Item -ItemType Directory -Path $destination -Force | Out-Null
    $replacementStarted = $true
    & (Join-Path $env:SystemRoot 'System32\robocopy.exe') $source $destination /MIR /R:2 /W:1 /NFL /NDL /NJH /NJS
    if ($LASTEXITCODE -ge 8) {{
        throw ""Robocopy failed with exit code $LASTEXITCODE.""
    }}
    $targetExe = Join-Path $destination 'Dvinholm.App.exe'
    if (-not (Test-Path -LiteralPath $targetExe)) {{
        throw 'The updated executable is missing after file replacement.'
    }}
    $updatedProcess = Start-Process -FilePath $targetExe -WorkingDirectory $destination -PassThru
    Start-Sleep -Seconds 3
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
    Remove-Item -LiteralPath $backup -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $source -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $scriptPath -Force -ErrorAction SilentlyContinue
}}
catch {{
    $failure = ($_ | Out-String)
    if ($replacementStarted -and $hadDestination -and $backupReady) {{
        try {{
            & (Join-Path $env:SystemRoot 'System32\robocopy.exe') $backup $destination /MIR /R:2 /W:1 /NFL /NDL /NJH /NJS
            if ($LASTEXITCODE -ge 8) {{
                throw ""Rollback copy failed with robocopy exit code $LASTEXITCODE.""
            }}
            $restoredExe = Join-Path $destination 'Dvinholm.App.exe'
            if (-not (Test-Path -LiteralPath $restoredExe)) {{
                throw 'Rollback completed without restoring Dvinholm.App.exe.'
            }}
            $failure += [Environment]::NewLine + 'The previous build was restored and will be restarted.'
            $restoredProcess = Start-Process -FilePath $restoredExe -WorkingDirectory $destination -PassThru
            Start-Sleep -Seconds 3
            if ($restoredProcess.HasExited) {{
                throw ""The restored application exited during startup (exit code $($restoredProcess.ExitCode)).""
            }}
            Remove-Item -LiteralPath $backup -Recurse -Force -ErrorAction SilentlyContinue
        }}
        catch {{
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
            $failure += [Environment]::NewLine + 'The incomplete first-install destination was removed.'
        }}
        catch {{
            $failure += [Environment]::NewLine + 'Could not remove the incomplete first-install destination: ' + ($_ | Out-String)
        }}
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
