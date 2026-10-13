namespace Dvinholm.App;

/// <summary>
/// Persists an ECU profile catalog to a user-selected file path.
/// The temporary file is created beside the destination so the final move
/// remains on the same volume and a failed write does not truncate the last
/// valid catalog.
/// </summary>
internal sealed class EcuProfileCatalogStore
{
    private readonly string _path;

    internal EcuProfileCatalogStore(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("A profile catalog path is required.", nameof(path));

        _path = Path.GetFullPath(path);
    }

    internal EcuProfileCatalog Load()
    {
        if (!File.Exists(_path))
            return new EcuProfileCatalog();

        return EcuProfileCatalog.FromJson(File.ReadAllText(_path));
    }

    internal void Save(EcuProfileCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);

        var directory = Path.GetDirectoryName(_path)
            ?? throw new InvalidOperationException("Could not determine the profile catalog directory.");
        Directory.CreateDirectory(directory);

        var temporaryPath = _path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporaryPath, catalog.ToJson());
            File.Move(temporaryPath, _path, overwrite: true);
        }
        finally
        {
            // Best-effort cleanup must not hide the original write/move error.
            try
            {
                if (File.Exists(temporaryPath))
                    File.Delete(temporaryPath);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
