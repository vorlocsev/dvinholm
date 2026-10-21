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
    private readonly Action<string, string> _writeTemporaryFile;

    internal EcuProfileCatalogStore(
        string path,
        Action<string, string>? writeTemporaryFile = null)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("A profile catalog path is required.", nameof(path));

        _path = Path.GetFullPath(path);
        // Testable staging boundary: real saves always use File.WriteAllText,
        // while tests can simulate a partial or missing temp write without
        // changing global filesystem permissions or racing background threads.
        _writeTemporaryFile = writeTemporaryFile ?? File.WriteAllText;
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
            _writeTemporaryFile(temporaryPath, catalog.ToJson());
            // A failed/short staging operation must never replace the last
            // valid catalog, even if a staging implementation returns early.
            if (!File.Exists(temporaryPath) || new FileInfo(temporaryPath).Length == 0)
                throw new IOException("ECU profile catalog staging produced no data.");
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
