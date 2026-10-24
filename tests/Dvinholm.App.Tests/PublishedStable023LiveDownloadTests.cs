using System.Net.Http.Headers;
using System.Reflection;
using Dvinholm.App;

namespace Dvinholm.App.Tests;

/// <summary>
/// Opt-in, read-only LIVE GitHub API regression for the published Stable 0.2.3
/// updater path. Runs only on a disposable GitHub Actions Windows runner.
/// It resolves the real tag, streams the public asset through the production
/// updater, checks ZIP digest and extracts it. It does NOT replace files or
/// launch the WinUI application, and does not simulate a manual GUI update.
/// </summary>
public sealed class PublishedStable023LiveDownloadTests
{
    private const string ApprovedSha = "91ecc343608c931eb033523e3db6012a6c5dbd58";
    private const string ApprovedDigest =
        "sha256:fae05958bf1ca4ef92f915508b66da5acb85b4c975c6eb84a950cb8da707ed44";

    [Fact]
    public async Task LiveStableLookupDownloadAndExtract_UsesRealPublishedV023()
    {
        if (!OperatingSystem.IsWindows() ||
            Environment.GetEnvironmentVariable("DVINHOLM_LIVE_STABLE_DOWNLOAD") != "1")
            return; // Normal unit CI is completely offline.

        Assert.Equal("true",
            Environment.GetEnvironmentVariable("GITHUB_ACTIONS")?.ToLowerInvariant());

        using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(8) };
        http.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue("DvinholmLiveStableAudit", "0.2.3"));
        http.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        http.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");

        // 1. Real published release, not an Actions artifact or draft.
        // 2. Identity comes from the immutable Git tag ref, not branch HEAD.
        var build = await LatestBuildUpdater.GetLatestStableReleaseAsync(http);
        Assert.Equal("v0.2.3", build.ReleaseTag);
        Assert.Equal(ApprovedSha, build.CommitSha);
        // 3. Real archive metadata and pinned GitHub SHA-256.
        Assert.Equal(62245250L, build.ArtifactSize);
        Assert.Equal(ApprovedDigest, build.ArtifactDigest);
        // 4. Stable uses the release-asset endpoint, not a private CI artifact URL.
        Assert.StartsWith(
            "https://api.github.com/repos/vorlocsev/dvinholm/releases/assets/",
            build.DownloadUrl, StringComparison.Ordinal);

        string? prepared = null;
        try
        {
            // 5. Exercise the real updater's HTTP binary download, size and
            // checksum verification, ZIP path defense, and extraction logic.
            prepared = await LatestBuildUpdater.DownloadAndPrepareAsync(build, http);
            Assert.True(Directory.Exists(prepared));
            Assert.Contains(ApprovedSha, prepared, StringComparison.Ordinal);

            // 6. The extracted bytes contain the approved x86 Stable assembly.
            var exe = Path.Combine(prepared, "Dvinholm.App.exe");
            var dll = Path.Combine(prepared, "Dvinholm.App.dll");
            Assert.True(File.Exists(exe), "Missing real Stable executable.");
            Assert.True(File.Exists(dll), "Missing real Stable assembly.");
            Assert.Equal("0.2.3",
                AssemblyName.GetAssemblyName(dll).Version!.ToString(3));
            // 7. Ordinary acceptance-only data marker cannot leak into Stable.
            Assert.False(File.Exists(Path.Combine(prepared, "ACCEPTANCE-0.2.3.txt")));
            // 8. The unique staging directory will be deleted, not installed.
        }
        finally
        {
            if (prepared is not null)
            {
                var allowedParent = Path.GetFullPath(Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Dvinholm", "UpdateStaging"));
                Assert.Equal(allowedParent, Path.GetDirectoryName(Path.GetFullPath(prepared)));
                if (Directory.Exists(prepared))
                    Directory.Delete(prepared, recursive: true);
                Assert.False(Directory.Exists(prepared));
            }
        }
    }
}
