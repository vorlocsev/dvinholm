namespace Dvinholm.App.Tests;

public sealed class CanBaudRateStoreTests
{
    [Fact]
    public void Load_WhenSettingsFileIsMissing_ReturnsDefaultBaudRate()
    {
        using var directory = new TemporaryDirectory();
        var store = new CanBaudRateStore(Path.Combine(directory.Path, "nested", "can-baud-rate.txt"));

        Assert.Equal(CanBaudRateStore.DefaultBaudRate, store.Load());
    }

    [Theory]
    [InlineData(500000u)]
    [InlineData(250000u)]
    [InlineData(125000u)]
    public void SaveAndLoad_RoundTripsSupportedBaudRate(uint baudRate)
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "nested", "can-baud-rate.txt");
        var store = new CanBaudRateStore(path);

        store.Save(baudRate);

        Assert.Equal(baudRate, store.Load());
        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(path)!, "*.tmp"));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("115200")]
    [InlineData("not-a-number")]
    [InlineData("")]
    public void Load_WhenSavedValueIsInvalid_ReturnsDefaultBaudRate(string value)
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "can-baud-rate.txt");
        File.WriteAllText(path, value);
        var store = new CanBaudRateStore(path);

        Assert.Equal(CanBaudRateStore.DefaultBaudRate, store.Load());
    }

    [Fact]
    public void Save_RejectsUnsupportedBaudRate()
    {
        using var directory = new TemporaryDirectory();
        var store = new CanBaudRateStore(Path.Combine(directory.Path, "can-baud-rate.txt"));

        Assert.Throws<ArgumentOutOfRangeException>(() => store.Save(115200));
    }

    [Fact]
    public void Save_WhenReplacementFails_CleansTemporaryFile()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "can-baud-rate.txt");
        Directory.CreateDirectory(path);
        var store = new CanBaudRateStore(path);

        var exception = Record.Exception(() => store.Save(250000));
        Assert.True(exception is IOException or UnauthorizedAccessException,
            $"Expected a file-system replacement failure, but got {exception?.GetType().Name ?? "no exception"}.");

        Assert.True(Directory.Exists(path));
        Assert.Empty(Directory.GetFiles(directory.Path, "can-baud-rate.txt.*.tmp"));
    }

    [Fact]
    public void Constructor_RejectsBlankSettingsPath()
    {
        Assert.Throws<ArgumentException>(() => new CanBaudRateStore(" "));
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        internal TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "Dvinholm.Tests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        internal string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
                Directory.Delete(Path, recursive: true);
        }
    }
}
