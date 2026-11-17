namespace Dvinholm.App.Tests;

/// <summary>
/// Offline preference gates: a remembered illustration must remain a manual
/// display choice, never an inferred vehicle identity or diagnostic input.
/// </summary>
public sealed class AuroraVehicleArtworkPreferenceStoreTests
{
    private sealed class Fixture : IDisposable
    {
        internal readonly string Folder = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "DvinholmArtworkPreferences", Guid.NewGuid().ToString("N"));
        internal string Path => System.IO.Path.Combine(Folder,
            AuroraVehicleArtworkPreferenceStore.FileName);
        internal AuroraVehicleArtworkPreferenceStore Store =>
            new(Path);
        public void Dispose()
        {
            if (Directory.Exists(Folder))
                Directory.Delete(Folder, recursive: true);
        }
    }

    private static string Source(string relative)
    {
        for (DirectoryInfo? dir = new(AppContext.BaseDirectory);
             dir is not null; dir = dir.Parent)
        {
            var path = System.IO.Path.Combine(dir.FullName,
                relative.Replace('/', System.IO.Path.DirectorySeparatorChar));
            if (File.Exists(path)) return File.ReadAllText(path);
        }
        throw new FileNotFoundException(relative);
    }

    [Fact]
    public void FreshInstallDoesNotCreatePreferencesOrInventVehicleIdentity()
    {
        using var f = new Fixture();
        Assert.Null(f.Store.Load());
        Assert.False(Directory.Exists(f.Folder));
    }

    [Fact]
    public void AllNineKnownManualModelsRoundTripAcrossStoreInstances()
    {
        using var f = new Fixture();
        foreach (var model in AuroraVehicleArtworkCatalog.Models)
        {
            f.Store.Save(model.Id);
            Assert.Equal(model.Id, f.Store.Load());
            Assert.Equal(model.Id, File.ReadAllText(f.Path));
        }
        Assert.Single(Directory.GetFiles(f.Folder));
    }

    [Fact]
    public void CorruptedUnknownOrNoncanonicalIdsAreIgnoredWithoutRepair()
    {
        using var f = new Fixture();
        Directory.CreateDirectory(f.Folder);
        foreach (var id in new[] { "", "XC90-P2", "xc90-p3", "s80-p2/../../VIN",
            "7E0", "xc90-p2\nxc70-p3", "../xc90-p2" })
        {
            File.WriteAllText(f.Path, id);
            Assert.Null(f.Store.Load());
            Assert.Equal(id, File.ReadAllText(f.Path));
        }
    }

    [Fact]
    public void OversizedSavedSelectionIsRejectedWithoutReadingIntoModel()
    {
        using var f = new Fixture();
        Directory.CreateDirectory(f.Folder);
        File.WriteAllText(f.Path, new string('x', 512));
        Assert.Null(f.Store.Load());
        Assert.Equal(512L, new FileInfo(f.Path).Length);
    }

    [Fact]
    public void InvalidSaveNeverCreatesFilesOrDirectories()
    {
        using var f = new Fixture();
        Assert.Throws<ArgumentOutOfRangeException>(() => f.Store.Save("../xc90-p2"));
        Assert.Throws<ArgumentOutOfRangeException>(() => f.Store.Save("XC90-P2"));
        Assert.Throws<ArgumentOutOfRangeException>(() => f.Store.Save(""));
        Assert.False(Directory.Exists(f.Folder));
    }

    [Fact]
    public void InterruptedAtomicStagingPreservesPreviousSelection()
    {
        using var f = new Fixture();
        f.Store.Save("s80-p2");
        var failing = new AuroraVehicleArtworkPreferenceStore(f.Path,
            (tmp, _) => File.WriteAllText(tmp, "xc90-"));
        Assert.Throws<IOException>(() => failing.Save("xc90-p2"));
        Assert.Equal("s80-p2", f.Store.Load());
        Assert.Empty(Directory.GetFiles(f.Folder, "*.tmp"));
    }

    [Fact]
    public void IndependentAppDataRootsDoNotLeakManualSelections()
    {
        using var a = new Fixture();
        using var b = new Fixture();
        a.Store.Save("v70-p3");
        b.Store.Save("xc90-spa");
        Assert.Equal("v70-p3", a.Store.Load());
        Assert.Equal("xc90-spa", b.Store.Load());
        Assert.NotEqual(a.Path, b.Path);
    }

    [Fact]
    public void WinUiRestoresAfterXamlAndNeverUsesArtworkAsVehicleIdentification()
    {
        var startup = Source("src/Dvinholm.App/MainWindow.xaml.cs");
        var ui = Source("src/Dvinholm.App/MainWindow.AuroraReference.cs");
        var locale = Source("src/Dvinholm.App/MainWindow.AuroraScreenLocalization.cs");
        Assert.True(startup.IndexOf("InitializeComponent();", StringComparison.Ordinal) <
                    startup.IndexOf("InitializeAuroraVehicleArtworkPreference();", StringComparison.Ordinal));
        Assert.Contains("DvinholmUserDataDirectory.Current", ui);
        Assert.Contains("EcuVehicleModel.SelectedItem = savedOption;", ui);
        Assert.Contains("_vehicleArtworkPreferenceReady = true;", ui);
        Assert.Contains("if (!_vehicleArtworkPreferenceReady", ui);
        Assert.Contains("RefreshAuroraVehicleArtwork();", ui);
        Assert.Contains("model.Id", ui);
        Assert.DoesNotContain("EcuVehicleModel.SelectedIndex =", locale);
        Assert.DoesNotContain("_vehicleArtworkPreference.Save(", locale);
    }
}
