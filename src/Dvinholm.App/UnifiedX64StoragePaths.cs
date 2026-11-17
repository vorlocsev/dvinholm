namespace Dvinholm.App;

/// <summary>
/// Update staging and future recovery snapshots must live OUTSIDE both the
/// installation folder and normal per-user Dvinholm settings. In particular,
/// an installation under %LOCALAPPDATA%\Dvinholm must not encompass its own
/// download/backup directories. No folders are created by these properties.
/// </summary>
internal static class UnifiedX64StoragePaths
{
    private static string LocalRoot =>
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

    internal static string StagingRoot =>
        Path.Combine(LocalRoot, "Dvinholm-UpdateCache", "staging");

    internal static string BackupRoot =>
        Path.Combine(LocalRoot, "Dvinholm-Backups");
}
