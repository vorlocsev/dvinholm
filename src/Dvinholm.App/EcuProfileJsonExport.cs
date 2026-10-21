namespace Dvinholm.App;

/// <summary>
/// Exports a copy of the local ECU profile catalog without creating an empty
/// Save picker placeholder, truncating existing JSON, or writing reports into
/// the application tree that the in-place updater may replace.
/// This never makes a vehicle connection or changes profile verification.
/// </summary>
internal static class EcuProfileJsonExport
{
    internal static Task SaveAsync(
        EcuProfileCatalog catalog,
        string destinationPath,
        string applicationDirectory)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(applicationDirectory);

        if (catalog.All.Count == 0)
            throw new InvalidOperationException("There are no ECU profiles to export.");

        // The updater mirrors its installation directory. Refuse the chosen
        // path before any temporary or destination file is created.
        if (ReportExportDestinationGuard.IsInsideApplicationDirectory(
                destinationPath, applicationDirectory))
            throw new InvalidOperationException(ReportExportDestinationGuard.BlockedMessage);

        // The existing report writer uses a same-folder temporary file and
        // atomically moves it over the target only after a complete write.
        return ReportExportFileWriter.WriteTextAsync(destinationPath, catalog.ToJson());
    }
}
