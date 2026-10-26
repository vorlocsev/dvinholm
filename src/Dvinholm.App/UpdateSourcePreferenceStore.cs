namespace Dvinholm.App;

/// <summary>
/// User-selected updater channel; independent of which build is installed.
/// Production and Development use the same per-user settings directory,
/// while verified acceptance copies use their private settings directory.
/// </summary>
internal enum UpdateSourceChannel
{
    Development = 0,
    Stable = 1
}

internal sealed class UpdateSourcePreferenceStore
{
    internal const string FileName = "update-source.txt";
    private readonly string _path;
    private readonly Action<string, string> _writeStaging;

    internal UpdateSourcePreferenceStore(string path, Action<string, string>? writeStaging = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        _path = path;
        _writeStaging = writeStaging ?? ((temporary, text) =>
            File.WriteAllText(temporary, text, new System.Text.UTF8Encoding(false)));
    }

    internal UpdateSourceChannel Load(UpdateSourceChannel fallback = UpdateSourceChannel.Development)
    {
        if (!Enum.IsDefined(fallback))
            throw new ArgumentOutOfRangeException(nameof(fallback));
        if (!File.Exists(_path))
            return fallback;

        return File.ReadAllText(_path).Trim() switch
        {
            "development" => UpdateSourceChannel.Development,
            "stable" => UpdateSourceChannel.Stable,
            _ => fallback
        };
    }

    internal void Save(UpdateSourceChannel channel)
    {
        var value = channel switch
        {
            UpdateSourceChannel.Development => "development",
            UpdateSourceChannel.Stable => "stable",
            _ => throw new ArgumentOutOfRangeException(nameof(channel))
        };

        var folder = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(folder))
            Directory.CreateDirectory(folder);

        var temporary = _path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            _writeStaging(temporary, value);
            // A failed or truncated temporary write must never destroy the
            // last channel choice, especially during updater restarts.
            if (!File.Exists(temporary) ||
                !string.Equals(File.ReadAllText(temporary), value, StringComparison.Ordinal))
                throw new IOException("Update source preference staging was incomplete.");

            File.Move(temporary, _path, overwrite: true);
        }
        finally
        {
            try
            {
                if (File.Exists(temporary))
                    File.Delete(temporary);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
