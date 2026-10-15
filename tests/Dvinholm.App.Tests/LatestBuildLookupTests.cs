using System.Net;
using System.Text;

namespace Dvinholm.App.Tests;

public sealed class LatestBuildLookupTests
{
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
                        { "event": "pull_request", "head_branch": "main", "status": "completed", "conclusion": "success", "head_sha": "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb", "id": 90, "run_number": 90, "created_at": "2026-10-15T00:00:00Z" },
                        { "event": "push", "head_branch": "main", "status": "completed", "conclusion": "failure", "head_sha": "cccccccccccccccccccccccccccccccccccccccc", "id": 89, "run_number": 89, "created_at": "2026-10-14T00:00:00Z" },
                        { "event": "workflow_dispatch", "head_branch": "main", "status": "completed", "conclusion": "success", "head_sha": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", "id": 12345, "run_number": 123, "created_at": "2026-10-15T01:00:00Z" }
                      ]
                    }
                    """);
            }

            return JsonResponse("""
                {
                  "assets": [
                    {
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
        Assert.Equal("https://github.com/vorlocsev/dvinholm/releases/download/ci-123/Dvinholm-Windows-x86.zip", build.DownloadUrl);
        Assert.Equal(2, requests.Count);
        Assert.Contains("/actions/workflows/378992585/runs?per_page=100", requests[0]);
        Assert.EndsWith("/releases/tags/ci-123", requests[1]);
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

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => LatestBuildUpdater.GetLatestSuccessfulBuildAsync(client));
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
