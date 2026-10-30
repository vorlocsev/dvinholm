using System.Net;
using System.Text;
using System.Text.Json;

namespace Dvinholm.App.Tests;

public sealed class LatestBuildLookupTests
{
    [Fact]
    public void SameCommitSha_DoesNotOfferReplaceWhenInstalledBuildNumberIsUnavailable()
    {
        const string sha = "6fd0da7034abe616f756dfa206a00ea242b74e58";
        var latest = new LatestBuildInfo(
            sha, 1751, 38830468262,
            "https://github.com/vorlocsev/dvinholm/releases/download/ci-1751/Dvinholm-Windows-x86.zip",
            1234, null, "2026-10-16T00:42:51Z");

        Assert.False(LatestBuildUpdater.IsUpdateAvailable(sha, latest));
    }

    [Fact]
    public void SameCommitSha_StatusSaysUpToDateInsteadOfAvailable()
    {
        const string sha = "6fd0da7034abe616f756dfa206a00ea242b74e58";
        var latest = new LatestBuildInfo(
            sha, 1751, 38830468262,
            "https://github.com/vorlocsev/dvinholm/releases/download/ci-1751/Dvinholm-Windows-x86.zip",
            1234, null, "2026-10-16T00:42:51Z");

        var status = LatestBuildUpdater.FormatUpdateStatus("CI #1751 · 6fd0da7", latest, updateAvailable: false);

        Assert.Contains("Up to date", status);
        Assert.DoesNotContain("Available:", status);
        Assert.Contains("CI #1751 · 6fd0da7", status);
    }

    [Fact]
    public void DifferentCommitSha_StatusShowsAvailableBuild()
    {
        const string latestSha = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
        var latest = new LatestBuildInfo(
            latestSha, 1752, 38830474549,
            "https://github.com/vorlocsev/dvinholm/releases/download/ci-1752/Dvinholm-Windows-x86.zip",
            1234, null, "2026-10-16T00:42:51Z");

        var status = LatestBuildUpdater.FormatUpdateStatus("CI #1751 · aaaaaaa", latest, updateAvailable: true);

        Assert.Contains("Available: CI #1752 · bbbbbbb", status);
        Assert.DoesNotContain("Up to date", status);
    }

    [Fact]
    public void DifferentCommitSha_OffersReplace()
    {
        const string currentSha = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        const string latestSha = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
        var latest = new LatestBuildInfo(
            latestSha, 1752, 38830474549,
            "https://github.com/vorlocsev/dvinholm/releases/download/ci-1752/Dvinholm-Windows-x86.zip",
            1234, null, "2026-10-16T00:42:51Z");

        Assert.True(LatestBuildUpdater.IsUpdateAvailable(currentSha, latest));
    }

    [Fact]
    public async Task LatestSuccessfulNonPrRun_ReturnsMatchingReleaseAssetMetadata()
    {
        const string sha = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        var requests = new List<string>();
        using var client = new HttpClient(new QueueHttpMessageHandler(request =>
        {
            requests.Add(request.RequestUri!.AbsoluteUri);
            if (request.RequestUri.AbsolutePath.EndsWith("/runs", StringComparison.Ordinal))
            {
                return JsonResponse("""
                    {
                      "workflow_runs": [
                        { "event": "pull_request", "status": "completed", "conclusion": "success", "head_sha": "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb", "id": 90, "run_number": 90, "created_at": "2026-10-15T00:00:00Z" },
                        { "event": "push", "head_branch": "main", "status": "completed", "conclusion": "failure", "head_sha": "cccccccccccccccccccccccccccccccccccccccc", "id": 89, "run_number": 89, "created_at": "2026-10-14T00:00:00Z" },
                        { "event": "push", "head_branch": "main", "status": "completed", "conclusion": "success", "head_sha": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", "id": 12345, "run_number": 123, "created_at": "2026-10-15T01:00:00Z" }
                      ]
                    }
                    """);
            }

            return JsonResponse("""
                {
                  "assets": [
                    {
                      "id": 987654,
                      "name": "Dvinholm-Windows-x86.zip",
                      "size": 1234,
                      "browser_download_url": "https://github.com/vorlocsev/dvinholm/releases/download/ci-123/Dvinholm-Windows-x86.zip",
                      "digest": "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"
                    }
                  ]
                }
                """);
        }));

        var build = await LatestBuildUpdater.GetLatestSuccessfulBuildAsync(client);

        Assert.Equal(sha, build.CommitSha);
        Assert.Equal(123, build.RunNumber);
        Assert.Equal(12345, build.RunId);
        Assert.Equal(1234, build.ArtifactSize);
        Assert.Equal("sha256:" + new string('a', 64), build.ArtifactDigest);
        Assert.Equal("https://api.github.com/repos/vorlocsev/dvinholm/releases/assets/987654", build.DownloadUrl);
        Assert.Equal(2, requests.Count);
        Assert.Contains("/actions/workflows/378992585/runs?per_page=100", requests[0]);
        Assert.EndsWith("/releases/tags/ci-123", requests[1]);
    }

    [Fact]
    public async Task FeatureBranchAndManualRuns_AreSkippedWithoutReleaseLookups()
    {
        using var client = new HttpClient(new QueueHttpMessageHandler(request =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/runs", StringComparison.Ordinal))
            {
                return JsonResponse("""
                    {
                      "workflow_runs": [
                        { "event": "push", "head_branch": "feature/example", "status": "completed", "conclusion": "success", "head_sha": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", "id": 100, "run_number": 100, "created_at": "2026-10-18T00:00:00Z" },
                        { "event": "workflow_dispatch", "head_branch": "main", "status": "completed", "conclusion": "success", "head_sha": "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb", "id": 99, "run_number": 99, "created_at": "2026-10-17T00:00:00Z" }
                      ]
                    }
                    """);
            }

            throw new InvalidOperationException("No release lookup should be made for runs that cannot publish updater assets.");
        }));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => LatestBuildUpdater.GetLatestSuccessfulBuildAsync(client));
    }

    [Fact]
    public async Task RunWithOutOfRangeRunNumber_IsSkippedAndOlderBuildIsUsed()
    {
        var requestedReleasePaths = new List<string>();
        using var client = new HttpClient(new QueueHttpMessageHandler(request =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/runs", StringComparison.Ordinal))
            {
                return JsonResponse("""
                    {
                      "workflow_runs": [
                        { "event": "push", "head_branch": "main", "status": "completed", "conclusion": "success", "head_sha": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", "id": 100, "run_number": 2147483648, "created_at": "2026-10-15T02:00:00Z" },
                        { "event": "push", "head_branch": "main", "status": "completed", "conclusion": "success", "head_sha": "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb", "id": 99, "run_number": 39, "created_at": "2026-10-15T01:00:00Z" }
                      ]
                    }
                    """);
            }

            requestedReleasePaths.Add(request.RequestUri!.AbsolutePath);
            if (!request.RequestUri.AbsolutePath.EndsWith("/ci-39", StringComparison.Ordinal))
                throw new InvalidOperationException("An out-of-range run number must not trigger a release lookup.");

            return JsonResponse("""
                {
                  "assets": [
                    {
                      "name": "Dvinholm-Windows-x86.zip",
                      "size": 4096,
                      "browser_download_url": "https://github.com/vorlocsev/dvinholm/releases/download/ci-39/Dvinholm-Windows-x86.zip",
                      "digest": "sha256:bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb"
                    }
                  ]
                }
                """);
        }));

        var build = await LatestBuildUpdater.GetLatestSuccessfulBuildAsync(client);

        Assert.Equal(39, build.RunNumber);
        Assert.Equal("bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb", build.CommitSha);
        Assert.Single(requestedReleasePaths);
    }

    [Fact]
    public async Task MissingWorkflowRunsArray_ThrowsInvalidDataException()
    {
        using var client = new HttpClient(new QueueHttpMessageHandler(_ =>
            JsonResponse("""{"message":"unexpected response"}""")));

        await Assert.ThrowsAsync<InvalidDataException>(
            () => LatestBuildUpdater.GetLatestSuccessfulBuildAsync(client));
    }

    [Fact]
    public async Task NoSuccessfulRunWithValidReleaseAsset_ThrowsInvalidOperationException()
    {
        using var client = new HttpClient(new QueueHttpMessageHandler(request =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/runs", StringComparison.Ordinal))
            {
                return JsonResponse("""
                    {
                      "workflow_runs": [
                        { "event": "push", "head_branch": "main", "status": "completed", "conclusion": "success", "head_sha": "not-a-valid-sha", "id": 12, "run_number": 12, "created_at": "2026-10-15T00:00:00Z" },
                        { "event": "push", "head_branch": "main", "status": "completed", "conclusion": "failure", "head_sha": "dddddddddddddddddddddddddddddddddddddddd", "id": 11, "run_number": 11, "created_at": "2026-10-14T00:00:00Z" }
                      ]
                    }
                    """);
            }

            throw new InvalidOperationException("No release request should be made for invalid or failed runs.");
        }));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => LatestBuildUpdater.GetLatestSuccessfulBuildAsync(client));
        Assert.Contains("main-branch CI run", exception.Message);
        Assert.Contains("published Windows x86 updater asset", exception.Message);
    }


    [Fact]
    public async Task ReleaseLookupForbidden_StopsDiscoveryWithoutQueryingOlderRuns()
    {
        var releaseRequests = 0;
        using var client = new HttpClient(new QueueHttpMessageHandler(request =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/runs", StringComparison.Ordinal))
            {
                return JsonResponse("""
                    {
                      "workflow_runs": [
                        { "event": "push", "head_branch": "main", "status": "completed", "conclusion": "success", "head_sha": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", "id": 50, "run_number": 50, "created_at": "2026-10-18T02:00:00Z" },
                        { "event": "push", "head_branch": "main", "status": "completed", "conclusion": "success", "head_sha": "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb", "id": 49, "run_number": 49, "created_at": "2026-10-18T01:00:00Z" }
                      ]
                    }
                    """);
            }

            releaseRequests++;
            return new HttpResponseMessage(HttpStatusCode.Forbidden);
        }));

        var exception = await Assert.ThrowsAsync<HttpRequestException>(
            () => LatestBuildUpdater.GetLatestSuccessfulBuildAsync(client));

        Assert.Equal(HttpStatusCode.Forbidden, exception.StatusCode);
        Assert.Equal(1, releaseRequests);
    }


    [Fact]
    public async Task MissingReleaseForNewestRun_FallsBackToOlderPublishedBuild()
    {
        var releaseRequests = new List<string>();
        using var client = new HttpClient(new QueueHttpMessageHandler(request =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/runs", StringComparison.Ordinal))
            {
                return JsonResponse("""
                    {
                      "workflow_runs": [
                        { "event": "push", "head_branch": "main", "status": "completed", "conclusion": "success", "head_sha": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", "id": 30, "run_number": 30, "created_at": "2026-10-15T02:00:00Z" },
                        { "event": "push", "head_branch": "main", "status": "completed", "conclusion": "success", "head_sha": "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb", "id": 29, "run_number": 29, "created_at": "2026-10-15T01:00:00Z" }
                      ]
                    }
                    """);
            }

            releaseRequests.Add(request.RequestUri!.AbsoluteUri);
            if (request.RequestUri.AbsolutePath.EndsWith("/ci-30", StringComparison.Ordinal))
                return new HttpResponseMessage(HttpStatusCode.NotFound);

            return JsonResponse("""
                {
                  "assets": [
                    {
                      "name": "Dvinholm-Windows-x86.zip",
                      "size": 2048,
                      "browser_download_url": "https://github.com/vorlocsev/dvinholm/releases/download/ci-29/Dvinholm-Windows-x86.zip",
                      "digest": "sha256:bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb"
                    }
                  ]
                }
                """);
        }));

        var build = await LatestBuildUpdater.GetLatestSuccessfulBuildAsync(client);

        Assert.Equal(29, build.RunNumber);
        Assert.Equal("bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb", build.CommitSha);
        Assert.Equal(2, releaseRequests.Count);
    }

    [Fact]
    public async Task ReleaseWithoutExpectedAsset_FallsBackToOlderPublishedBuild()
    {
        using var client = new HttpClient(new QueueHttpMessageHandler(request =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/runs", StringComparison.Ordinal))
            {
                return JsonResponse("""
                    {
                      "workflow_runs": [
                        { "event": "push", "head_branch": "main", "status": "completed", "conclusion": "success", "head_sha": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", "id": 40, "run_number": 40, "created_at": "2026-10-15T02:00:00Z" },
                        { "event": "push", "head_branch": "main", "status": "completed", "conclusion": "success", "head_sha": "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb", "id": 39, "run_number": 39, "created_at": "2026-10-15T01:00:00Z" }
                      ]
                    }
                    """);
            }

            if (request.RequestUri!.AbsolutePath.EndsWith("/ci-40", StringComparison.Ordinal))
            {
                return JsonResponse("""
                    {
                      "assets": [
                        {
                          "name": "other-file.zip",
                          "size": 2048,
                          "browser_download_url": "https://github.com/vorlocsev/dvinholm/releases/download/ci-40/other-file.zip",
                          "digest": "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"
                        }
                      ]
                    }
                    """);
            }

            return JsonResponse("""
                {
                  "assets": [
                    {
                      "name": "Dvinholm-Windows-x86.zip",
                      "size": 4096,
                      "browser_download_url": "https://github.com/vorlocsev/dvinholm/releases/download/ci-39/Dvinholm-Windows-x86.zip",
                      "digest": "sha256:bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb"
                    }
                  ]
                }
                """);
        }));

        var build = await LatestBuildUpdater.GetLatestSuccessfulBuildAsync(client);

        Assert.Equal(39, build.RunNumber);
        Assert.Equal("bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb", build.CommitSha);
    }


    [Fact]
    public async Task ReleaseWithMismatchedReleaseTag_FallsBackToOlderPublishedBuild()
    {
        using var client = new HttpClient(new QueueHttpMessageHandler(request =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/runs", StringComparison.Ordinal))
            {
                return JsonResponse("""
                    {
                      "workflow_runs": [
                        { "event": "push", "head_branch": "main", "status": "completed", "conclusion": "success", "head_sha": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", "id": 80, "run_number": 80, "created_at": "2026-10-18T06:00:00Z" },
                        { "event": "push", "head_branch": "main", "status": "completed", "conclusion": "success", "head_sha": "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb", "id": 79, "run_number": 79, "created_at": "2026-10-18T05:00:00Z" }
                      ]
                    }
                    """);
            }

            if (request.RequestUri!.AbsolutePath.EndsWith("/ci-80", StringComparison.Ordinal))
            {
                return JsonResponse("""
                    {
                      "assets": [
                        {
                          "name": "Dvinholm-Windows-x86.zip",
                          "size": 2048,
                          "browser_download_url": "https://github.com/vorlocsev/dvinholm/releases/download/ci-79/Dvinholm-Windows-x86.zip",
                          "digest": "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"
                        }
                      ]
                    }
                    """);
            }

            return JsonResponse("""
                {
                  "assets": [
                    {
                      "name": "Dvinholm-Windows-x86.zip",
                      "size": 4096,
                      "browser_download_url": "https://github.com/vorlocsev/dvinholm/releases/download/ci-79/Dvinholm-Windows-x86.zip",
                      "digest": "sha256:bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb"
                    }
                  ]
                }
                """);
        }));

        var build = await LatestBuildUpdater.GetLatestSuccessfulBuildAsync(client);

        Assert.Equal(79, build.RunNumber);
        Assert.Equal("bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb", build.CommitSha);
        Assert.Equal("https://github.com/vorlocsev/dvinholm/releases/download/ci-79/Dvinholm-Windows-x86.zip", build.DownloadUrl);
    }

    [Fact]
    public async Task ReleaseWithUnsafeDownloadUrl_FallsBackToOlderPublishedBuild()
    {
        using var client = new HttpClient(new QueueHttpMessageHandler(request =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/runs", StringComparison.Ordinal))
            {
                return JsonResponse("""
                    {
                      "workflow_runs": [
                        { "event": "push", "head_branch": "main", "status": "completed", "conclusion": "success", "head_sha": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", "id": 50, "run_number": 50, "created_at": "2026-10-15T03:00:00Z" },
                        { "event": "push", "head_branch": "main", "status": "completed", "conclusion": "success", "head_sha": "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb", "id": 49, "run_number": 49, "created_at": "2026-10-15T02:00:00Z" }
                      ]
                    }
                    """);
            }

            if (request.RequestUri!.AbsolutePath.EndsWith("/ci-50", StringComparison.Ordinal))
            {
                return JsonResponse("""
                    {
                      "assets": [
                        {
                          "name": "Dvinholm-Windows-x86.zip",
                          "size": 2048,
                          "browser_download_url": "http://example.invalid/Dvinholm-Windows-x86.zip",
                          "digest": "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"
                        }
                      ]
                    }
                    """);
            }

            return JsonResponse("""
                {
                  "assets": [
                    {
                      "name": "Dvinholm-Windows-x86.zip",
                      "size": 4096,
                      "browser_download_url": "https://github.com/vorlocsev/dvinholm/releases/download/ci-49/Dvinholm-Windows-x86.zip",
                      "digest": "sha256:bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb"
                    }
                  ]
                }
                """);
        }));

        var build = await LatestBuildUpdater.GetLatestSuccessfulBuildAsync(client);

        Assert.Equal(49, build.RunNumber);
        Assert.Equal("https://github.com/vorlocsev/dvinholm/releases/download/ci-49/Dvinholm-Windows-x86.zip", build.DownloadUrl);
    }

    [Theory]
    [InlineData("md5:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    [InlineData("sha256:abc")]
    [InlineData("sha256:gggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggg")]
    [InlineData("sha256:" + "a" + "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    public async Task ReleaseWithMalformedDigest_FallsBackToOlderPublishedBuild(string malformedDigest)
    {
        using var client = new HttpClient(new QueueHttpMessageHandler(request =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/runs", StringComparison.Ordinal))
            {
                return JsonResponse("""
                    {
                      "workflow_runs": [
                        { "event": "push", "head_branch": "main", "status": "completed", "conclusion": "success", "head_sha": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", "id": 60, "run_number": 60, "created_at": "2026-10-15T04:00:00Z" },
                        { "event": "push", "head_branch": "main", "status": "completed", "conclusion": "success", "head_sha": "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb", "id": 59, "run_number": 59, "created_at": "2026-10-15T03:00:00Z" }
                      ]
                    }
                    """);
            }

            if (request.RequestUri!.AbsolutePath.EndsWith("/ci-60", StringComparison.Ordinal))
            {
                var release = """
                    {
                      "assets": [
                        {
                          "name": "Dvinholm-Windows-x86.zip",
                          "size": 2048,
                          "browser_download_url": "https://github.com/vorlocsev/dvinholm/releases/download/ci-60/Dvinholm-Windows-x86.zip",
                          "digest": "__DIGEST__"
                        }
                      ]
                    }
                    """.Replace("__DIGEST__", malformedDigest, StringComparison.Ordinal);
                return JsonResponse(release);
            }

            return JsonResponse("""
                {
                  "assets": [
                    {
                      "name": "Dvinholm-Windows-x86.zip",
                      "size": 4096,
                      "browser_download_url": "https://github.com/vorlocsev/dvinholm/releases/download/ci-59/Dvinholm-Windows-x86.zip",
                      "digest": "sha256:bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb"
                    }
                  ]
                }
                """);
        }));

        var build = await LatestBuildUpdater.GetLatestSuccessfulBuildAsync(client);

        Assert.Equal(59, build.RunNumber);
        Assert.Equal("sha256:" + new string('b', 64), build.ArtifactDigest);
    }

    [Theory]
    [InlineData("123")]
    [InlineData("true")]
    [InlineData("[]")]
    [InlineData("\"\"")]
    public async Task ReleaseWithNonStringOrEmptyDigest_FallsBackToOlderPublishedBuild(string digestJson)
    {
        using var client = new HttpClient(new QueueHttpMessageHandler(request =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/runs", StringComparison.Ordinal))
            {
                return JsonResponse("""
                    {
                      "workflow_runs": [
                        { "event": "push", "head_branch": "main", "status": "completed", "conclusion": "success", "head_sha": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", "id": 61, "run_number": 61, "created_at": "2026-10-15T04:30:00Z" },
                        { "event": "push", "head_branch": "main", "status": "completed", "conclusion": "success", "head_sha": "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb", "id": 59, "run_number": 59, "created_at": "2026-10-15T03:00:00Z" }
                      ]
                    }
                    """);
            }

            if (request.RequestUri!.AbsolutePath.EndsWith("/ci-61", StringComparison.Ordinal))
            {
                var release = """
                    {
                      "assets": [
                        {
                          "name": "Dvinholm-Windows-x86.zip",
                          "size": 2048,
                          "browser_download_url": "https://github.com/vorlocsev/dvinholm/releases/download/ci-61/Dvinholm-Windows-x86.zip",
                          "digest": __DIGEST__
                        }
                      ]
                    }
                    """.Replace("__DIGEST__", digestJson, StringComparison.Ordinal);
                return JsonResponse(release);
            }

            return JsonResponse("""
                {
                  "assets": [
                    {
                      "name": "Dvinholm-Windows-x86.zip",
                      "size": 4096,
                      "browser_download_url": "https://github.com/vorlocsev/dvinholm/releases/download/ci-59/Dvinholm-Windows-x86.zip",
                      "digest": "sha256:bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb"
                    }
                  ]
                }
                """);
        }));

        var build = await LatestBuildUpdater.GetLatestSuccessfulBuildAsync(client);

        Assert.Equal(59, build.RunNumber);
        Assert.Equal("sha256:" + new string('b', 64), build.ArtifactDigest);
    }

    [Fact]
    public async Task ReleaseWithExternalHttpsDownloadUrl_FallsBackToOlderPublishedBuild()
    {
        using var client = new HttpClient(new QueueHttpMessageHandler(request =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/runs", StringComparison.Ordinal))
            {
                return JsonResponse("""
                    {
                      "workflow_runs": [
                        { "event": "push", "head_branch": "main", "status": "completed", "conclusion": "success", "head_sha": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", "id": 70, "run_number": 70, "created_at": "2026-10-15T05:00:00Z" },
                        { "event": "push", "head_branch": "main", "status": "completed", "conclusion": "success", "head_sha": "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb", "id": 69, "run_number": 69, "created_at": "2026-10-15T04:00:00Z" }
                      ]
                    }
                    """);
            }

            if (request.RequestUri!.AbsolutePath.EndsWith("/ci-70", StringComparison.Ordinal))
            {
                return JsonResponse("""
                    {
                      "assets": [
                        {
                          "name": "Dvinholm-Windows-x86.zip",
                          "size": 2048,
                          "browser_download_url": "https://example.invalid/Dvinholm-Windows-x86.zip",
                          "digest": "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"
                        }
                      ]
                    }
                    """);
            }

            return JsonResponse("""
                {
                  "assets": [
                    {
                      "name": "Dvinholm-Windows-x86.zip",
                      "size": 4096,
                      "browser_download_url": "https://github.com/vorlocsev/dvinholm/releases/download/ci-69/Dvinholm-Windows-x86.zip",
                      "digest": "sha256:bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb"
                    }
                  ]
                }
                """);
        }));

        var build = await LatestBuildUpdater.GetLatestSuccessfulBuildAsync(client);

        Assert.Equal(69, build.RunNumber);
        Assert.Equal("https://github.com/vorlocsev/dvinholm/releases/download/ci-69/Dvinholm-Windows-x86.zip", build.DownloadUrl);
    }

    [Fact]
    public async Task ReleaseWithUnexpectedGithubPath_FallsBackToOlderPublishedBuild()
    {
        using var client = new HttpClient(new QueueHttpMessageHandler(request =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/runs", StringComparison.Ordinal))
            {
                return JsonResponse("""
                    {
                      "workflow_runs": [
                        { "event": "push", "head_branch": "main", "status": "completed", "conclusion": "success", "head_sha": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", "id": 80, "run_number": 80, "created_at": "2026-10-15T06:00:00Z" },
                        { "event": "push", "head_branch": "main", "status": "completed", "conclusion": "success", "head_sha": "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb", "id": 79, "run_number": 79, "created_at": "2026-10-15T05:00:00Z" }
                      ]
                    }
                    """);
            }

            if (request.RequestUri!.AbsolutePath.EndsWith("/ci-80", StringComparison.Ordinal))
            {
                return JsonResponse("""
                    {
                      "assets": [
                        {
                          "name": "Dvinholm-Windows-x86.zip",
                          "size": 2048,
                          "browser_download_url": "https://github.com/attacker/repo/releases/download/ci-80/Dvinholm-Windows-x86.zip",
                          "digest": "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"
                        }
                      ]
                    }
                    """);
            }

            return JsonResponse("""
                {
                  "assets": [
                    {
                      "name": "Dvinholm-Windows-x86.zip",
                      "size": 4096,
                      "browser_download_url": "https://github.com/vorlocsev/dvinholm/releases/download/ci-79/Dvinholm-Windows-x86.zip",
                      "digest": "sha256:bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb"
                    }
                  ]
                }
                """);
        }));

        var build = await LatestBuildUpdater.GetLatestSuccessfulBuildAsync(client);

        Assert.Equal(79, build.RunNumber);
        Assert.Equal("https://github.com/vorlocsev/dvinholm/releases/download/ci-79/Dvinholm-Windows-x86.zip", build.DownloadUrl);
    }

    [Fact]
    public async Task ReleaseWithCredentialBearingDownloadUrl_FallsBackToOlderPublishedBuild()
    {
        using var client = new HttpClient(new QueueHttpMessageHandler(request =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/runs", StringComparison.Ordinal))
            {
                return JsonResponse("""
                    {
                      "workflow_runs": [
                        { "event": "push", "head_branch": "main", "status": "completed", "conclusion": "success", "head_sha": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", "id": 90, "run_number": 90, "created_at": "2026-10-15T07:00:00Z" },
                        { "event": "push", "head_branch": "main", "status": "completed", "conclusion": "success", "head_sha": "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb", "id": 89, "run_number": 89, "created_at": "2026-10-15T06:00:00Z" }
                      ]
                    }
                    """);
            }

            if (request.RequestUri!.AbsolutePath.EndsWith("/ci-90", StringComparison.Ordinal))
            {
                return JsonResponse("""
                    {
                      "assets": [
                        {
                          "name": "Dvinholm-Windows-x86.zip",
                          "size": 2048,
                          "browser_download_url": "https://user:password@github.com/vorlocsev/dvinholm/releases/download/ci-90/Dvinholm-Windows-x86.zip",
                          "digest": "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"
                        }
                      ]
                    }
                    """);
            }

            return JsonResponse("""
                {
                  "assets": [
                    {
                      "name": "Dvinholm-Windows-x86.zip",
                      "size": 4096,
                      "browser_download_url": "https://github.com/vorlocsev/dvinholm/releases/download/ci-89/Dvinholm-Windows-x86.zip",
                      "digest": "sha256:bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb"
                    }
                  ]
                }
                """);
        }));

        var build = await LatestBuildUpdater.GetLatestSuccessfulBuildAsync(client);

        Assert.Equal(89, build.RunNumber);
        Assert.Equal("https://github.com/vorlocsev/dvinholm/releases/download/ci-89/Dvinholm-Windows-x86.zip", build.DownloadUrl);
    }

    [Theory]
    [InlineData("https://github.com/vorlocsev/dvinholm/releases/download/ci-90/Dvinholm-Windows-x86.zip?token=secret")]
    [InlineData("https://github.com/vorlocsev/dvinholm/releases/download/ci-90/Dvinholm-Windows-x86.zip#fragment")]
    [InlineData("https://github.com:8443/vorlocsev/dvinholm/releases/download/ci-90/Dvinholm-Windows-x86.zip")]
    public async Task ReleaseWithAmbiguousDownloadUrl_FallsBackToOlderPublishedBuild(string unsafeUrl)
    {
        using var client = new HttpClient(new QueueHttpMessageHandler(request =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/runs", StringComparison.Ordinal))
            {
                return JsonResponse("""
                    {
                      "workflow_runs": [
                        { "event": "push", "head_branch": "main", "status": "completed", "conclusion": "success", "head_sha": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", "id": 90, "run_number": 90, "created_at": "2026-10-15T07:00:00Z" },
                        { "event": "push", "head_branch": "main", "status": "completed", "conclusion": "success", "head_sha": "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb", "id": 89, "run_number": 89, "created_at": "2026-10-15T06:00:00Z" }
                      ]
                    }
                    """);
            }

            if (request.RequestUri!.AbsolutePath.EndsWith("/ci-90", StringComparison.Ordinal))
            {
                var json = JsonSerializer.Serialize(new
                {
                    assets = new[]
                    {
                        new
                        {
                            name = "Dvinholm-Windows-x86.zip",
                            size = 2048,
                            browser_download_url = unsafeUrl,
                            digest = "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"
                        }
                    }
                });
                return JsonResponse(json);
            }

            return JsonResponse("""
                {
                  "assets": [
                    {
                      "name": "Dvinholm-Windows-x86.zip",
                      "size": 4096,
                      "browser_download_url": "https://github.com/vorlocsev/dvinholm/releases/download/ci-89/Dvinholm-Windows-x86.zip",
                      "digest": "sha256:bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb"
                    }
                  ]
                }
                """);
        }));

        var build = await LatestBuildUpdater.GetLatestSuccessfulBuildAsync(client);

        Assert.Equal(89, build.RunNumber);
        Assert.Equal("https://github.com/vorlocsev/dvinholm/releases/download/ci-89/Dvinholm-Windows-x86.zip", build.DownloadUrl);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReleaseWithMissingOrNullDigest_RemainsEligible(bool includeNullDigest)
    {
        using var client = new HttpClient(new QueueHttpMessageHandler(request =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/runs", StringComparison.Ordinal))
            {
                return JsonResponse("""
                    {
                      "workflow_runs": [
                        { "event": "push", "head_branch": "main", "status": "completed", "conclusion": "success", "head_sha": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", "id": 92, "run_number": 92, "created_at": "2026-10-15T09:00:00Z" }
                      ]
                    }
                    """);
            }

            var digestProperty = includeNullDigest ? ", \"digest\": null" : string.Empty;
            var json = "{\"assets\":[{\"name\":\"Dvinholm-Windows-x86.zip\",\"size\":2048," +
                "\"browser_download_url\":\"https://github.com/vorlocsev/dvinholm/releases/download/ci-92/Dvinholm-Windows-x86.zip\"" +
                digestProperty + "}]}";
            return JsonResponse(json);
        }));

        var build = await LatestBuildUpdater.GetLatestSuccessfulBuildAsync(client);

        Assert.Equal(92, build.RunNumber);
        Assert.Null(build.ArtifactDigest);
    }

    [Fact]
    public async Task ReleaseWithUppercaseDigest_NormalizesDigest()
    {
        using var client = new HttpClient(new QueueHttpMessageHandler(request =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/runs", StringComparison.Ordinal))
            {
                return JsonResponse("""
                    {
                      "workflow_runs": [
                        { "event": "push", "head_branch": "main", "status": "completed", "conclusion": "success", "head_sha": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", "id": 91, "run_number": 91, "created_at": "2026-10-15T08:00:00Z" }
                      ]
                    }
                    """);
            }

            return JsonResponse("""
                {
                  "assets": [
                    {
                      "name": "Dvinholm-Windows-x86.zip",
                      "size": 2048,
                      "browser_download_url": "https://github.com/vorlocsev/dvinholm/releases/download/ci-91/Dvinholm-Windows-x86.zip",
                      "digest": "SHA256:AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA"
                    }
                  ]
                }
                """);
        }));

        var build = await LatestBuildUpdater.GetLatestSuccessfulBuildAsync(client);

        Assert.Equal("sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", build.ArtifactDigest);
    }


    [Fact]
    public async Task LookupContinuesToSecondPageWhenFirstPageHasNoEligibleBuild()
    {
        var requests = new List<string>();
        using var client = new HttpClient(new QueueHttpMessageHandler(request =>
        {
            requests.Add(request.RequestUri!.AbsoluteUri);
            if (request.RequestUri.AbsolutePath.EndsWith("/runs", StringComparison.Ordinal))
            {
                var queryParameters = request.RequestUri.Query
                    .TrimStart('?')
                    .Split('&', StringSplitOptions.RemoveEmptyEntries)
                    .Select(parameter => parameter.Split('=', 2))
                    .Where(parts => parts.Length == 2)
                    .ToDictionary(parts => Uri.UnescapeDataString(parts[0]), parts => Uri.UnescapeDataString(parts[1]), StringComparer.OrdinalIgnoreCase);
                var page = queryParameters.TryGetValue("page", out var pageValue) ? pageValue : null;
                if (page == "1")
                {
                    var firstPage = string.Join(",", Enumerable.Range(1, 100).Select(i =>
                        "{\"event\":\"pull_request\",\"status\":\"completed\",\"conclusion\":\"success\",\"head_sha\":\"" +
                        new string('a', 40) + "\",\"id\":" + i + ",\"run_number\":" + i +
                        ",\"created_at\":\"2026-10-15T00:00:00Z\"}"));
                    return JsonResponse("{\"workflow_runs\":[" + firstPage + "]}");
                }

                Assert.Equal("2", page);
                return JsonResponse("""
                    {
                      "workflow_runs": [
                        { "event": "push", "head_branch": "main", "status": "completed", "conclusion": "success", "head_sha": "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb", "id": 201, "run_number": 201, "created_at": "2026-10-16T00:00:00Z" }
                      ]
                    }
                    """);
            }

            Assert.EndsWith("/releases/tags/ci-201", request.RequestUri.AbsolutePath);
            return JsonResponse("""
                {
                  "assets": [
                    {
                      "id": 987,
                      "name": "Dvinholm-Windows-x86.zip",
                      "size": 2048,
                      "browser_download_url": "https://github.com/vorlocsev/dvinholm/releases/download/ci-201/Dvinholm-Windows-x86.zip",
                      "digest": "sha256:bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb"
                    }
                  ]
                }
                """);
        }));

        var build = await LatestBuildUpdater.GetLatestSuccessfulBuildAsync(client);

        Assert.Equal(201, build.RunNumber);
        Assert.Equal("bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb", build.CommitSha);
        Assert.Contains(requests, url => url.Contains("page=1", StringComparison.Ordinal));
        Assert.Contains(requests, url => url.Contains("page=2", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DevelopmentCi_PrefersLiteAndRequiresHostPrerequisites(
        bool includeHistoricalPortable)
    {
        const string sha = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        using var http = new HttpClient(new QueueHttpMessageHandler(request =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/runs", StringComparison.Ordinal))
            {
                return JsonResponse("""
                    {
                      "workflow_runs": [
                        { "event": "push", "head_branch": "main",
                          "status": "completed", "conclusion": "success",
                          "head_sha": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
                          "id": 120, "run_number": 120,
                          "created_at": "2026-10-28T11:00:00Z" }
                      ]
                    }
                    """);
            }
            const string lite = """
                { "id": 500, "name": "Dvinholm-Windows-x86-Lite.zip",
                  "size": 10240,
                  "browser_download_url": "https://github.com/vorlocsev/dvinholm/releases/download/ci-120/Dvinholm-Windows-x86-Lite.zip",
                  "digest": "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa" }
                """;
            const string portable = """
                { "id": 499, "name": "Dvinholm-Windows-x86.zip",
                  "size": 65536,
                  "browser_download_url": "https://github.com/vorlocsev/dvinholm/releases/download/ci-120/Dvinholm-Windows-x86.zip",
                  "digest": "sha256:bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb" }
                """;
            // Intentionally place legacy Portable FIRST to prove Lite selection
            // is not accidentally determined by GitHub asset array order.
            return JsonResponse("{\"assets\":[" +
                (includeHistoricalPortable ? portable + "," : "") + lite + "]}");
        }));

        var build = await LatestBuildUpdater.GetLatestSuccessfulBuildAsync(http);
        Assert.Equal(sha, build.CommitSha);
        Assert.True(build.RequiresLiteRuntime);
        Assert.Equal(10240, build.ArtifactSize);
        Assert.Equal("https://api.github.com/repos/vorlocsev/dvinholm/releases/assets/500",
            build.DownloadUrl);
    }

    [Fact]
    public async Task HistoricalCiPortableStillResolvesWithoutLiteDependencyGate()
    {
        using var http = new HttpClient(new QueueHttpMessageHandler(request =>
            request.RequestUri!.AbsolutePath.EndsWith("/runs", StringComparison.Ordinal)
                ? JsonResponse("""
                    { "workflow_runs": [
                      { "event": "push", "head_branch": "main",
                        "status": "completed", "conclusion": "success",
                        "head_sha": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
                        "id": 119, "run_number": 119, "created_at": "2026-10-15T11:00:00Z" }
                    ] }
                    """)
                : JsonResponse("""
                    { "assets": [
                      { "id": 498, "name": "Dvinholm-Windows-x86.zip",
                        "size": 65536,
                        "browser_download_url": "https://github.com/vorlocsev/dvinholm/releases/download/ci-119/Dvinholm-Windows-x86.zip",
                        "digest": "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa" }
                    ] }
                    """)
        ));
        var build = await LatestBuildUpdater.GetLatestSuccessfulBuildAsync(http);
        Assert.False(build.RequiresLiteRuntime);
        Assert.Equal(119, build.RunNumber);
    }

    private static HttpResponseMessage JsonResponse(string json) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json")
    };

    private sealed class QueueHttpMessageHandler(
        Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(responseFactory(request));
        }
    }
}
