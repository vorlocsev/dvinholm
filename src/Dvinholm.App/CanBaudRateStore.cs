namespace Dvinholm.App;

/// <summary>
/// Persists the preferred CAN bus speed independently of connection state.
/// </summary>
public sealed class CanBaudRateStore
{
    public const uint DefaultBaudRate = 500000;

    private static readonly HashSet<uint> SupportedBaudRates = [500000, 250000, 125000];
    private readonly string _path;

    public CanBaudRateStore(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("A settings file path is required.", nameof(path));

        _path = path;
    }

    public uint Load()
    {
        if (!File.Exists(_path))
            return DefaultBaudRate;

        var text = File.ReadAllText(_path).Trim();
        return uint.TryParse(text, out var baudRate) && SupportedBaudRates.Contains(baudRate)
            ? baudRate
            : DefaultBaudRate;
    }

    public void Save(uint baudRate)
    {
        if (!SupportedBaudRates.Contains(baudRate))
            throw new ArgumentOutOfRangeException(nameof(baudRate), "Unsupported CAN baud rate.");

        var directory = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        var temporaryPath = _path + ".tmp";
        File.WriteAllText(temporaryPath, baudRate.ToString(System.Globalization.CultureInfo.InvariantCulture),
            new System.Text.UTF8Encoding(false));
        File.Move(temporaryPath, _path, overwrite: true);
    }
}
