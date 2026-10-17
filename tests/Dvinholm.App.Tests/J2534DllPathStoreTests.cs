namespace Dvinholm.App.Tests;

public sealed class J2534DllPathStoreTests
{
    [Fact]
    public void Load_WhenSettingsFileIsMissing_ReturnsEmptyPath()
    {
        using var directory = new TemporaryDirectory();
        var store = new J2534DllPathStore(Path.Combine(directory.Path, "nested", "j2534-dll-path.txt"));

        Assert.Equal(string.Empty, store.Load());
    }

    [Fact]
    public void SaveAndLoad_RoundTripPathAndCreateParentDirectory()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "nested", "j2534-dll-path.txt");
        var store = new J2534DllPathStore(path);

        store.Save(@"  C:\Drivers\VendorJ2534.dll  ");

        Assert.Equal(@"C:\Drivers\VendorJ2534.dll", store.Load());
        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(path)!, "*.tmp"));
    }

    [Fact]
    public void Save_EmptyPathClearsPreviouslySavedValue()
    {
        using var directory = new TemporaryDirectory();
        var store = new J2534DllPathStore(Path.Combine(directory.Path, "j2534-dll-path.txt"));
        store.Save(@"C:\Drivers\VendorJ2534.dll");

        store.Save(string.Empty);

        Assert.Equal(string.Empty, store.Load());
    }

    [Fact]
    public void Constructor_RejectsBlankSettingsPath()
    {
        Assert.Throws<ArgumentException>(() => new J2534DllPathStore(" "));
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
