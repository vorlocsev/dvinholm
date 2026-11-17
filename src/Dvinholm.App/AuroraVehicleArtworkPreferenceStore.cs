namespace Dvinholm.App;

/// <summary>
/// Persists only the canonical ID of a manually chosen reference illustration.
/// The value is not vehicle identity, VIN evidence, a J2534 device choice,
/// a CAN address or permission to issue any diagnostic request.
/// </summary>
internal sealed class AuroraVehicleArtworkPreferenceStore
{
    internal const string FileName = "vehicle-model.txt";
    private readonly string _path;
    private readonly Action<string, string> _writeStaging;

    internal AuroraVehicleArtworkPreferenceStore(
        string path, Action<string, string>? writeStaging = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        _path = path;
        _writeStaging = writeStaging ?? ((temporary, id) =>
            File.WriteAllText(temporary, id, new System.Text.UTF8Encoding(false)));
    }

    private static bool IsKnownId(string? id) =>
        id is not null && AuroraVehicleArtworkCatalog.Models.Any(
            model => string.Equals(model.Id, id, StringComparison.Ordinal));

    internal string? Load()
    {
        try
        {
            if (!File.Exists(_path)) return null;
            var file = new FileInfo(_path);
            // No symlinks, oversized values or unknown model IDs are trusted.
            if ((file.Attributes & FileAttributes.ReparsePoint) != 0 ||
                file.Length is < 1 or > 64)
                return null;

            var id = File.ReadAllText(_path).Trim();
            return IsKnownId(id) ? id : null;
        }
        catch (IOException) { return null; }
        catch (UnauthorizedAccessException) { return null; }
    }

    internal void Save(string modelId)
    {
        if (!IsKnownId(modelId))
            throw new ArgumentOutOfRangeException(nameof(modelId),
                "Only an existing manual artwork model ID can be persisted.");

        var folder = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(folder))
            Directory.CreateDirectory(folder);

        var temporary = _path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            _writeStaging(temporary, modelId);
            if (!File.Exists(temporary) ||
                new FileInfo(temporary).Length is < 1 or > 64 ||
                !string.Equals(File.ReadAllText(temporary), modelId,
                    StringComparison.Ordinal))
                throw new IOException("Vehicle artwork preference staging was incomplete.");
            // Same-directory rename, never write partial data to the saved file.
            File.Move(temporary, _path, overwrite: true);
        }
        finally
        {
            try
            {
                if (File.Exists(temporary)) File.Delete(temporary);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
