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
        // Deliberately do not filter by branch: the user wants the newest successful
        // CI build from any branch, including a feature branch or pull request.
        var runsUrl = $"https://api.github.com/repos/{Repository}/actions/workflows/{WorkflowId}/runs?per_page=100";
        using var runsResponse = await http.GetAsync(runsUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        runsResponse.EnsureSuccessStatusCode();
        await using var runsStream = await runsResponse.Content.ReadAsStreamAsync(cancellationToken);
        using var runsJson = await JsonDocument.ParseAsync(runsStream, cancellationToken: cancellationToken);

        if (!runsJson.RootElement.TryGetProperty("workflow_runs", out var runs) || runs.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException("GitHub did not return the CI workflow run list.");

        foreach (var run in runs.EnumerateArray())
        {
            // Push/workflow_dispatch artifacts are built from the exact head SHA.
            // Pull-request workflows can build a synthetic merge ref, so their
            // artifact SHA is not necessarily the branch's head_sha.
            var eventName = ReadString(run, "event");
            if (eventName is not ("push" or "workflow_dispatch") ||
                ReadString(run, "status") != "completed" ||
                ReadString(run, "conclusion") != "success")
                continue;

            var sha = ReadString(run, "head_sha");
            if (sha.Length != 40 || !sha.All(Uri.IsHexDigit))
                continue;

            var runId = ReadInt64(run, "id");
            var runNumber = checked((int)ReadInt64(run, "run_number"));
            var createdAt = ReadString(run, "created_at");
            // GitHub Actions artifact ZIP endpoints require authentication, even for
            // public repositories. Successful non-PR CI runs publish a public
            // prerelease asset tagged ci-<run number>; download that asset anonymously.
            var releaseUrl = $"https://api.github.com/repos/{Repository}/releases/tags/ci-{runNumber}";
            using var releaseResponse = await http.GetAsync(releaseUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!releaseResponse.IsSuccessStatusCode)
                continue;

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
                if (size <= 0 || size > MaximumDownloadBytes || !Uri.TryCreate(downloadUrl, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
                    continue;

                var digest = ReadString(asset, "digest");
                string? validatedDigest = null;
                if (!string.IsNullOrEmpty(digest))
                {
                    // Older release metadata may omit a digest, but if one is
                    // supplied it must be a complete SHA-256 value. Do not select
                    // an asset whose malformed digest would silently disable verification.
                    if (!digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase) ||
                        digest.Length != 71 ||
                        !digest[7..].All(Uri.IsHexDigit))
                        continue;

                    validatedDigest = digest.ToLowerInvariant();
                }

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

        throw new InvalidOperationException("No successful CI run with a non-expired Windows x86 artifact was found across the repository branches.");
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
            using (var response = await http.GetAsync(build.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken))
            {
                response.EnsureSuccessStatusCode();
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
                long expandedBytes = 0;
                var fullRoot = Path.GetFullPath(staging) + Path.DirectorySeparatorChar;
                foreach (var entry in archive.Entries)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    expandedBytes = checked(expandedBytes + entry.Length);
                    if (expandedBytes > MaximumExpandedBytes)
                        throw new InvalidDataException("The extracted artifact exceeds the configured size limit.");

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
                try { if (Directory.Exists(staging)) Directory.Delete(staging, recursive: true); } catch (IOException) { }
            }
            try { if (File.Exists(archivePath)) File.Delete(archivePath); } catch (IOException) { }
        }
    }

    public static Process StartReplacement(string stagingDirectory, string destinationDirectory, int processId)
    {
        var localRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Dvinholm");
        var destination = Path.GetFullPath(destinationDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        Directory.CreateDirectory(localRoot);

        var scriptPath = Path.Combine(Path.GetTempPath(), $"dvinholm-update-{Guid.NewGuid():N}.ps1");
        var script = $@"
$ErrorActionPreference = 'Stop'
$source = '{EscapePowerShellLiteral(Path.GetFullPath(stagingDirectory))}'
$destination = '{EscapePowerShellLiteral(Path.GetFullPath(destination))}'
$processId = {processId}
$scriptPath = $MyInvocation.MyCommand.Path
$logPath = Join-Path $env:LOCALAPPDATA 'Dvinholm\update.log'
try {{
    Wait-Process -Id $processId -ErrorAction SilentlyContinue
    Start-Sleep -Milliseconds 400
    if (-not (Test-Path -LiteralPath (Join-Path $source 'Dvinholm.App.exe'))) {{
        throw 'The staged update is missing Dvinholm.App.exe.'
    }}
    New-Item -ItemType Directory -Path $destination -Force | Out-Null
    & (Join-Path $env:SystemRoot 'System32\robocopy.exe') $source $destination /MIR /R:2 /W:1 /NFL /NDL /NJH /NJS
    if ($LASTEXITCODE -ge 8) {{
        throw ""Robocopy failed with exit code $LASTEXITCODE.""
    }}
    $targetExe = Join-Path $destination 'Dvinholm.App.exe'
    if (-not (Test-Path -LiteralPath $targetExe)) {{
        throw 'The updated executable is missing after file replacement.'
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
    Start-Process -FilePath $targetExe -WorkingDirectory $destination
    Remove-Item -LiteralPath $source -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $scriptPath -Force -ErrorAction SilentlyContinue
}}
catch {{
    ($_ | Out-String) | Set-Content -LiteralPath $logPath -Encoding UTF8
    Start-Process -FilePath 'notepad.exe' -ArgumentList @($logPath)
}}
";
        File.WriteAllText(scriptPath, script, new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

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

        try
        {
            return Process.Start(startInfo) ?? throw new InvalidOperationException("Windows could not start the update helper.");
        }
        catch
        {
            try { File.Delete(scriptPath); } catch (IOException) { }
            throw;
        }
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
