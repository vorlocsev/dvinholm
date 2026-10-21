namespace Dvinholm.App;

/// <summary>
/// Prevents accidentally writing user-created reports inside the running
/// application's installation tree. In-place updates mirror that tree and
/// may remove files not belonging to the next application build.
/// This is a path-based UX guard, not a filesystem security boundary.
/// </summary>
internal static class ReportExportDestinationGuard
{
    internal const string BlockedMessage =
        "Report not saved: choose a folder outside the Dvinholm application directory. " +
        "Application updates can replace or remove files stored there.";

    internal static bool IsInsideApplicationDirectory(string destinationFilePath, string applicationDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationFilePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(applicationDirectory);

        // Normalize dot-segments and trailing separators before comparison.
        // Windows paths are case-insensitive for the ordinary x86 desktop
        // installation scenario; include a directory boundary so
        // C:\Dvinholm-Reports is not mistaken for C:\Dvinholm.
        var appRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(applicationDirectory));
        var candidate = Path.GetFullPath(destinationFilePath);

        if (string.Equals(candidate, appRoot, StringComparison.OrdinalIgnoreCase))
            return true;

        var prefix = Path.EndsInDirectorySeparator(appRoot)
            ? appRoot
            : appRoot + Path.DirectorySeparatorChar;
        return candidate.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }
}
