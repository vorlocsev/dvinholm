namespace Dvinholm.App;

/// <summary>
/// Persists the user's preferred J2534 DLL path independently of connection state.
/// </summary>
public sealed class J2534DllPathStore
{
    private readonly string _path;

    public J2534DllPathStore(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("A settings file path is required.", nameof(path));

        _path = path;
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

        var temporaryPath = _path + ".tmp";
        File.WriteAllText(temporaryPath, value, new System.Text.UTF8Encoding(false));
        File.Move(temporaryPath, _path, overwrite: true);
    }
}
