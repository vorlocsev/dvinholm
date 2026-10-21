using System.Text;
using Dvinholm.App;

namespace Dvinholm.App.Tests;

/// <summary>
/// Offline Windows-compatible regression tests for exporting ECU profiles.
/// No UI automation, J2534 driver, physical adapter, or vehicle is required.
/// </summary>
public sealed class EcuProfileJsonExportTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "Dvinholm-json-export-tests", Guid.NewGuid().ToString("N"));
    private readonly string _applicationDirectory;
    private readonly string _documentsDirectory;

    public EcuProfileJsonExportTests()
    {
        _applicationDirectory = Path.Combine(_root, "installed-app");
        _documentsDirectory = Path.Combine(_root, "Documents");
        Directory.CreateDirectory(_applicationDirectory);
        Directory.CreateDirectory(_documentsDirectory);
    }

    private static EcuProfileCatalog Catalog(params EcuProfile[] profiles) =>
        new(profiles.Length > 0 ? profiles :
        [
            new EcuProfile("test.example.ecm", "Example ECM (test only)",
                0x7E0, 0x7E8, false, [0xF187, 0xF190],
                "Synthetic test fixture only; not verified for any vehicle.")
        ]);

    private void AssertNoTemporaryExports() =>
        Assert.Empty(Directory.GetFiles(_root, ".dvinholm-report-*.tmp",
            SearchOption.AllDirectories));

    [Fact]
    public async Task JsonExport_RoundTripsExistingProfileFieldsWithoutChangingCatalog()
    {
        var catalog = Catalog(
            new EcuProfile("test.one", "Standard ECU", 0x7E0, 0x7E8, false,
                [0xF190, 0xF187], "Unverified synthetic source"),
            new EcuProfile("test.two", "Extended ECU", 0x18DA10F1, 0x18DAF110, true,
                [0x1234], "Author note only", isVerified: true));
        var originalJson = catalog.ToJson();
        var output = Path.Combine(_documentsDirectory, "profiles.json");

        await EcuProfileJsonExport.SaveAsync(catalog, output, _applicationDirectory);

        Assert.Equal(originalJson, await File.ReadAllTextAsync(output));
        var imported = EcuProfileCatalog.FromJson(await File.ReadAllTextAsync(output));
        Assert.Equal(2, imported.All.Count);
        Assert.Equal((uint)0x18DAF110, imported.All.Single(p => p.IsExtended).ResponseId);
        Assert.True(imported.All.Single(p => p.IsExtended).IsVerified);
        Assert.Equal(originalJson, catalog.ToJson());
        AssertNoTemporaryExports();
    }

    [Fact]
    public async Task JsonExport_WritesNonemptyUtf8WithoutBom()
    {
        var output = Path.Combine(_documentsDirectory, "ecu-profiles.json");

        await EcuProfileJsonExport.SaveAsync(Catalog(), output, _applicationDirectory);

        var bytes = await File.ReadAllBytesAsync(output);
        Assert.True(bytes.Length > 100);
        Assert.False(bytes.Take(3).SequenceEqual(new byte[] { 0xEF, 0xBB, 0xBF }));
        Assert.Contains("test.example.ecm", Encoding.UTF8.GetString(bytes));
        AssertNoTemporaryExports();
    }

    [Fact]
    public async Task JsonExport_OverwritesExistingOnlyAfterProducingCompleteJson()
    {
        var output = Path.Combine(_documentsDirectory, "existing.json");
        await File.WriteAllTextAsync(output, "old user JSON");

        await EcuProfileJsonExport.SaveAsync(Catalog(), output, _applicationDirectory);

        Assert.Single(EcuProfileCatalog.FromJson(await File.ReadAllTextAsync(output)).All);
        AssertNoTemporaryExports();
    }

    [Fact]
    public async Task JsonExport_EmptyCatalogIsRejectedWithoutCreatingDestination()
    {
        var output = Path.Combine(_documentsDirectory, "empty.json");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            EcuProfileJsonExport.SaveAsync(new EcuProfileCatalog(),
                output, _applicationDirectory));

        Assert.False(File.Exists(output));
        AssertNoTemporaryExports();
    }

    [Fact]
    public async Task JsonExport_AppRootRejectedBeforeCreatingAnyPlaceholder()
    {
        var output = Path.Combine(_applicationDirectory, "ecu-profiles.json");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            EcuProfileJsonExport.SaveAsync(Catalog(), output, _applicationDirectory));

        Assert.Equal(ReportExportDestinationGuard.BlockedMessage, ex.Message);
        Assert.False(File.Exists(output));
        AssertNoTemporaryExports();
    }

    [Fact]
    public async Task JsonExport_AppSubfolderRejectedAndExistingFilePreserved()
    {
        var child = Path.Combine(_applicationDirectory, "backups");
        Directory.CreateDirectory(child);
        var output = Path.Combine(child, "ecu-profiles.json");
        await File.WriteAllTextAsync(output, "existing personal data");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            EcuProfileJsonExport.SaveAsync(Catalog(), output, _applicationDirectory));

        Assert.Equal("existing personal data", await File.ReadAllTextAsync(output));
        AssertNoTemporaryExports();
    }

    [Fact]
    public async Task JsonExport_SiblingPrefixOutsideAppIsAllowed()
    {
        var sibling = _applicationDirectory + "-Reports";
        Directory.CreateDirectory(sibling);
        var output = Path.Combine(sibling, "ecu-profiles.json");

        await EcuProfileJsonExport.SaveAsync(Catalog(), output, _applicationDirectory);

        Assert.True(new FileInfo(output).Length > 0);
        Assert.Single(EcuProfileCatalog.FromJson(await File.ReadAllTextAsync(output)).All);
        AssertNoTemporaryExports();
    }

    [Fact]
    public async Task JsonExport_WriteFailureDoesNotCreateDestinationOrTemporaryFile()
    {
        var output = Path.Combine(_documentsDirectory, "missing-parent", "ecu-profiles.json");
        var catalog = Catalog();
        var originalJson = catalog.ToJson();

        await Assert.ThrowsAsync<DirectoryNotFoundException>(() =>
            EcuProfileJsonExport.SaveAsync(catalog, output, _applicationDirectory));

        Assert.False(File.Exists(output));
        Assert.Equal(originalJson, catalog.ToJson());
        AssertNoTemporaryExports();
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }
}
