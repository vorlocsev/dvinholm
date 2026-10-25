namespace Dvinholm.App;

/// <summary>
/// Persists the preferred CAN bus speed independently of connection state.
/// </summary>
public sealed class CanBaudRateStore
{
    public const uint DefaultBaudRate = 500000;

    private static readonly HashSet<uint> SupportedBaudRates = [500000, 250000, 125000];
    private readonly string _path;
    private readonly Action<string, string> _writeTemporaryFile;

    public CanBaudRateStore(string path, Action<string, string>? writeTemporaryFile = null)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("A settings file path is required.", nameof(path));

        _path = path;
        // Normal saves remain UTF-8 without BOM. An injectable staging writer
        // makes incomplete-write failure paths testable without a J2534 device.
        _writeTemporaryFile = writeTemporaryFile ??
            ((temporaryPath, text) =>
                File.WriteAllText(temporaryPath, text, new System.Text.UTF8Encoding(false)));
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

        var temporaryPath = _path + $".{Guid.NewGuid():N}.tmp";
        try
        {
            var expectedText = baudRate.ToString(System.Globalization.CultureInfo.InvariantCulture);
            _writeTemporaryFile(temporaryPath, expectedText);

            // A staging write can return without error despite writing a
            // truncated value or even a different supported speed. Verify
            // the exact setting before replacing the last known-good file.
            if (!File.Exists(temporaryPath) ||
                !string.Equals(File.ReadAllText(temporaryPath), expectedText, StringComparison.Ordinal))
                throw new IOException("CAN baud-rate staging did not match the intended value.");

            File.Move(temporaryPath, _path, overwrite: true);
        }
        finally
        {
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
