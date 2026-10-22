namespace Dvinholm.App.Tests;

/// <summary>
/// Removes disposable test directories without failing passing tests on
/// transient Windows file sharing locks from AV/indexers or child processes.
/// Non-filesystem errors are intentionally not suppressed.
/// </summary>
internal static class WindowsTestFixtureCleanup
{
    internal static async Task<bool> RemoveBestEffortAsync(
        string directory,
        Action<string>? erase = null,
        int maxAttempts = 60,
        int retryDelayMilliseconds = 200,
        Action<string>? logWarning = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxAttempts);
        ArgumentOutOfRangeException.ThrowIfNegative(retryDelayMilliseconds);
        erase ??= static path => Directory.Delete(path, recursive: true);
        logWarning ??= static message => Console.Error.WriteLine(message);

        Exception? lastLock = null;
        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            if (!Directory.Exists(directory))
                return true;

            try
            {
                erase(directory);
                if (!Directory.Exists(directory))
                    return true;
            }
            catch (IOException ex)
            {
                lastLock = ex;
            }
            catch (UnauthorizedAccessException ex)
            {
                lastLock = ex;
            }

            if (attempt + 1 < maxAttempts)
                await Task.Delay(retryDelayMilliseconds);
        }

        logWarning($"Test fixture cleanup deferred: {directory}. " +
                   $"Attempts: {maxAttempts}. {lastLock?.Message ?? "Directory still exists."}");
        return false;
    }
}
