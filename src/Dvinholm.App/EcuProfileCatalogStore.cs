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
    private readonly Action<string, string> _copyCommittedCatalogToBackup;

    internal EcuProfileCatalogStore(
        string path,
        Action<string, string>? writeTemporaryFile = null,
        Action<string, string>? copyCommittedCatalogToBackup = null)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("A profile catalog path is required.", nameof(path));

        _path = Path.GetFullPath(path);
        // Testable staging boundary: real saves always use File.WriteAllText,
        // while tests can simulate a partial or missing temp write without
        // changing global filesystem permissions or racing background threads.
        _writeTemporaryFile = writeTemporaryFile ?? File.WriteAllText;
        // Inject only the backup staging operation in offline regression tests.
        // Normal saves use the same filesystem copy as before.
        _copyCommittedCatalogToBackup = copyCommittedCatalogToBackup ?? File.Copy;
    }

    internal EcuProfileCatalog Load()
    {
        if (!File.Exists(_path))
            return new EcuProfileCatalog();

        return EcuProfileCatalog.FromJson(File.ReadAllText(_path));
    }

    /// <summary>
    /// Prefer the primary catalog. If its contents are invalid, a known-good
    /// backup can restore the in-memory view without modifying either file.
    /// Missing primary remains empty, not an implicit recovery request.
    /// </summary>
    internal (EcuProfileCatalog Catalog, bool RecoveredFromBackup) LoadWithRecovery()
    {
        try
        {
            return (Load(), false);
        }
        catch (Exception ex) when (ex is InvalidDataException or ArgumentException)
        {
            // Do not hide a corrupted main catalog if no usable backup exists.
            // The original is left untouched so it can be examined manually.
            if (!File.Exists(BackupPath))
                throw;

            try
            {
                return (EcuProfileCatalog.FromJson(File.ReadAllText(BackupPath)), true);
            }
            catch (Exception backupError) when (
                backupError is InvalidDataException or ArgumentException)
            {
                throw new InvalidDataException(
                    "The saved ECU profile catalog and its backup are both invalid.",
                    backupError);
            }
        }
    }

    private string BackupPath => _path + ".bak";

    internal void Save(EcuProfileCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);

        var directory = Path.GetDirectoryName(_path)
            ?? throw new InvalidOperationException("Could not determine the profile catalog directory.");
        Directory.CreateDirectory(directory);

        // When an already-corrupted primary is replaced, preserve the actual
        // bytes first. Never silently overwrite the only copy of damaged data.
        // If archiving fails, refuse the save and leave the primary untouched.
        if (File.Exists(_path))
        {
            try
            {
                EcuProfileCatalog.FromJson(File.ReadAllText(_path));
            }
            catch (Exception ex) when (ex is InvalidDataException or ArgumentException)
            {
                var archivedPath = _path + ".corrupt-" + Guid.NewGuid().ToString("N") + ".json";
                File.Copy(_path, archivedPath, overwrite: false);
            }
        }

        var temporaryPath = _path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            var expectedJson = catalog.ToJson();
            _writeTemporaryFile(temporaryPath, expectedJson);

            // A faulty writer can return successfully after emitting only
            // part of the JSON, or different but still valid JSON. Verify the
            // entire staged catalog, not merely a nonzero file length, before
            // atomically replacing the last known-good primary.
            if (!File.Exists(temporaryPath) ||
                !string.Equals(File.ReadAllText(temporaryPath), expectedJson, StringComparison.Ordinal))
                throw new IOException("ECU profile catalog staging did not match the intended catalog.");

            File.Move(temporaryPath, _path, overwrite: true);
        }
        finally
        {
            TryDeleteTemporary(temporaryPath);
        }

        // A crash-safe, same-folder snapshot of the *committed* catalog.
        // A backup failure must not turn a successful primary commit into a
        // reported save failure (which could cause the UI to show stale data).
        // Retain the prior valid backup if staging this snapshot fails.
        var backupTemporaryPath = BackupPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            _copyCommittedCatalogToBackup(_path, backupTemporaryPath);

            // A staging API can unexpectedly return after writing no bytes,
            // fewer bytes, or bytes different from the committed catalog.
            // Never promote that incomplete snapshot over a valid .bak file.
            // A comparison is deliberately byte-exact, including metadata
            // fields that must survive an application update or restart.
            if (!File.Exists(backupTemporaryPath) ||
                !File.ReadAllBytes(_path).AsSpan().SequenceEqual(
                    File.ReadAllBytes(backupTemporaryPath)))
                throw new IOException("ECU profile backup staging did not match the committed catalog.");

            File.Move(backupTemporaryPath, BackupPath, overwrite: true);
        }
        catch (IOException) { /* Primary is committed; keep prior backup. */ }
        catch (UnauthorizedAccessException) { /* Keep prior backup. */ }
        finally
        {
            TryDeleteTemporary(backupTemporaryPath);
        }
    }

    private static void TryDeleteTemporary(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
