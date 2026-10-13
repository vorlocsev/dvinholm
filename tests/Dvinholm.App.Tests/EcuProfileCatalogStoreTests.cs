namespace Dvinholm.App.Tests;

public sealed class EcuProfileCatalogStoreTests
{
    [Fact]
    public void Load_WhenFileIsMissing_ReturnsEmptyCatalog()
    {
        using var directory = new TemporaryDirectory();
        var store = new Dvinholm.App.EcuProfileCatalogStore(
            Path.Combine(directory.Path, "nested", "ecu-profiles.json"));

        var catalog = store.Load();

        Assert.Empty(catalog.All);
        Assert.Empty(catalog.Verified);
    }

    [Fact]
    public void SaveAndLoad_RoundTripCatalogAndCreateParentDirectory()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "nested", "ecu-profiles.json");
        var store = new Dvinholm.App.EcuProfileCatalogStore(path);
        var expected = new Dvinholm.App.EcuProfileCatalog([
            new Dvinholm.App.EcuProfile(
                "volvo.example.ecm",
                "Example ECM",
                0x7E0,
                0x7E8,
                false,
                [0xF190, 0xF187],
                "Test fixture; not a vehicle-specific profile.",
                isVerified: true)
        ]);

        store.Save(expected);
        var loaded = store.Load();

        var profile = Assert.Single(loaded.All);
        Assert.Equal("volvo.example.ecm", profile.Id);
        Assert.Equal("Example ECM", profile.Name);
        Assert.Equal((uint)0x7E0, profile.RequestId);
        Assert.Equal((uint)0x7E8, profile.ResponseId);
        Assert.True(profile.IsVerified);
        Assert.Equal(new ushort[] { 0xF187, 0xF190 }, profile.ReadableDataIdentifiers.Order().ToArray());
        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(path)!, "*.tmp"));
    }

    [Fact]
    public void Save_ReplacesPreviousCatalogInsteadOfAppendingToIt()
    {
        using var directory = new TemporaryDirectory();
        var store = new Dvinholm.App.EcuProfileCatalogStore(
            Path.Combine(directory.Path, "ecu-profiles.json"));
        store.Save(new Dvinholm.App.EcuProfileCatalog([
            CreateProfile("first.profile", "First")
        ]));

        store.Save(new Dvinholm.App.EcuProfileCatalog([
            CreateProfile("second.profile", "Second")
        ]));

        var loaded = store.Load();
        var profile = Assert.Single(loaded.All);
        Assert.Equal("second.profile", profile.Id);
        Assert.Equal("Second", profile.Name);
    }

    [Fact]
    public void Load_WhenCatalogIsMalformed_ReportsValidationError()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "ecu-profiles.json");
        File.WriteAllText(path, "{not json}");
        var store = new Dvinholm.App.EcuProfileCatalogStore(path);

        var exception = Assert.Throws<InvalidDataException>(() => store.Load());

        Assert.Contains("malformed", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Constructor_RejectsBlankPath()
    {
        Assert.Throws<ArgumentException>(() => new Dvinholm.App.EcuProfileCatalogStore(" "));
    }

    private static Dvinholm.App.EcuProfile CreateProfile(string id, string name) =>
        new(id, name, 0x7E0, 0x7E8, false, null,
            "Test fixture; not a vehicle-specific profile.");

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
