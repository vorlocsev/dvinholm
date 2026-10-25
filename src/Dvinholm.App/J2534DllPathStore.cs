namespace Dvinholm.App;

/// <summary>
/// Persists the user's preferred J2534 DLL path independently of connection state.
/// </summary>
public sealed class J2534DllPathStore
{
    private readonly string _path;
    private readonly Action<string, string> _writeTemporaryFile;

    public J2534DllPathStore(string path, Action<string, string>? writeTemporaryFile = null)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("A settings file path is required.", nameof(path));

        _path = path;
        // Injectable staging boundary for deterministic tests of interrupted
        // writes without touching permissions or a vendor's actual DLL.
        _writeTemporaryFile = writeTemporaryFile ??
            ((temporaryPath, value) =>
                File.WriteAllText(temporaryPath, value, new System.Text.UTF8Encoding(false)));
    }

    public string Load()
    {
        if (!File.Exists(_path))
            return string.Empty;

        return File.ReadAllText(_path).Trim();
    }

    public void Save(string? dllPath)
    {
        var value = dllPath?.Trim() ?? string.Empty;
        var directory = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        // A fixed .tmp name can collide with an interrupted earlier save or
        // another concurrent Save. Keep staging beside the destination for
        // same-volume atomic replacement, but give each attempt its own name.
        var temporaryPath = _path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            _writeTemporaryFile(temporaryPath, value);

            // An external writer may return without reporting a partial write.
            // Never replace a valid DLL path with truncated or altered data.
            if (!File.Exists(temporaryPath) ||
                !string.Equals(File.ReadAllText(temporaryPath), value, StringComparison.Ordinal))
                throw new IOException("J2534 DLL path staging did not match the intended value.");

            File.Move(temporaryPath, _path, overwrite: true);
        }
        finally
        {
            try
            {
                if (File.Exists(temporaryPath))
                    File.Delete(temporaryPath);
            }
            catch (IOException) { /* Preserve the original save result or error. */ }
            catch (UnauthorizedAccessException) { /* A later attempt uses a fresh name. */ }
        }
    }
}
