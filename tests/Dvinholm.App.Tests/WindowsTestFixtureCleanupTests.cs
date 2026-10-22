namespace Dvinholm.App.Tests;

/// <summary>
/// Eight deterministic regression scenarios for best-effort Windows
/// test-directory cleanup. Injected locks avoid flaky timing assumptions.
/// </summary>
public sealed class WindowsTestFixtureCleanupTests
{
    [Theory]
    [InlineData("already-missing")]
    [InlineData("empty-directory")]
    [InlineData("nested-files")]
    [InlineData("transient-io")]
    [InlineData("transient-access-denied")]
    [InlineData("persistent-io")]
    [InlineData("persistent-access-denied")]
    [InlineData("unexpected-exception")]
    public async Task Cleanup_HandlesDisposableFixtureFilesSafely(string scenario)
    {
        var root = Path.Combine(Path.GetTempPath(),
            "Dvinholm-test-cleanup", Guid.NewGuid().ToString("N"));
        if (scenario != "already-missing") Directory.CreateDirectory(root);
        try
        {
            if (scenario == "nested-files")
            {
                Directory.CreateDirectory(Path.Combine(root, "nested"));
                File.WriteAllText(Path.Combine(root, "nested", "data.txt"), "fixture");
            }

            var attempts = 0;
            var warnings = new List<string>();
            Action<string> erase = path =>
            {
                attempts++;
                switch (scenario)
                {
                    case "transient-io" when attempts == 1:
                    case "persistent-io":
                        throw new IOException("simulated transient file lock");
                    case "transient-access-denied" when attempts == 1:
                    case "persistent-access-denied":
                        throw new UnauthorizedAccessException("simulated file sharing conflict");
                    case "unexpected-exception":
                        throw new InvalidOperationException("unexpected cleanup failure");
                    default:
                        Directory.Delete(path, recursive: true);
                        break;
                }
            };

            if (scenario == "unexpected-exception")
            {
                await Assert.ThrowsAsync<InvalidOperationException>(() =>
                    WindowsTestFixtureCleanup.RemoveBestEffortAsync(root,
                        erase, 3, 0, warnings.Add));
                Assert.Empty(warnings);
                return;
            }

            var clean = await WindowsTestFixtureCleanup.RemoveBestEffortAsync(
                root, erase, 3, 0, warnings.Add);
            var persistent = scenario is "persistent-io" or "persistent-access-denied";
            Assert.Equal(!persistent, clean);
            Assert.Equal(persistent, Directory.Exists(root));
            Assert.Equal(persistent ? 3 : scenario.StartsWith("transient-") ? 2 :
                scenario == "already-missing" ? 0 : 1, attempts);
            if (persistent)
            {
                Assert.Single(warnings);
                Assert.Contains("cleanup deferred", warnings[0]);
            }
            else
                Assert.Empty(warnings);
        }
        finally
        {
            // The scenarios with injected permanent errors use the actual
            // deleter here; they do not leave test data behind.
            await WindowsTestFixtureCleanup.RemoveBestEffortAsync(
                root, maxAttempts: 5, retryDelayMilliseconds: 10);
        }
    }
}
